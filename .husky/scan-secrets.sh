#!/usr/bin/env sh
# ─────────────────────────────────────────────────────────────────────────────
# Staged-content secret scanner.
#
# Why this exists: GitHub's own secret scanning and push protection are not
# available here — they are free for public repositories only, and this is a
# private repo on a free-tier org ("Secret scanning is not available for this
# repository", HTTP 422). This hook is the substitute, in the same spirit as the
# pre-push guard in the sibling repositories.
#
# What it is guarding against specifically: this repository's whole subject is a
# password vault. A `bw list items` dump is every password, TOTP seed and note in
# PLAINTEXT, and git keeps it in history even after a later delete. That is the
# accident worth spending a hook on.
#
# Design rule: NO NOISE. A hook that cries wolf is a hook everyone bypasses, and
# a habitually bypassed hook is worse than none. Every pattern below is either an
# exact vendor token shape or a structure that has no innocent explanation.
# Entropy heuristics are deliberately absent — they are the usual source of the
# false positives that kill these things.
#
# Bypass once (and think about why):  git commit --no-verify
# ─────────────────────────────────────────────────────────────────────────────
set -u

RED='\033[0;31m'; YELLOW='\033[0;33m'; DIM='\033[2m'; OFF='\033[0m'
findings=0

report() {
  findings=$((findings + 1))
  printf "${RED}✋ %s${OFF}\n   %s\n" "$1" "$2" >&2
}

# Staged files, added/copied/modified only — renames and deletions carry no new content.
staged=$(git diff --cached --name-only --diff-filter=ACM)
[ -z "$staged" ] && exit 0

for file in $staged; do
  # The scanner and its documentation necessarily contain the patterns themselves.
  case "$file" in
    .husky/scan-secrets.sh|.gitignore|docs/security*|*.md) continue ;;
  esac

  # ── 1. Filenames that are vault dumps by convention ──────────────────────
  case "$(basename "$file")" in
    items.json|folders.json|report.json|merge-log.json|REVIEW.md)
      report "Vault dump staged: $file" \
        "This is vault data, not source. Remove it: git restore --staged '$file'"
      continue
      ;;
  esac

  # Binary files have no text to scan.
  git diff --cached --numstat -- "$file" | grep -q '^-' && continue

  added=$(git diff --cached -U0 -- "$file" | grep '^+' | grep -v '^+++')
  [ -z "$added" ] && continue

  # ── 2. A Bitwarden EncString ─────────────────────────────────────────────
  # "<type>.<b64 iv>|<b64 ciphertext>|<b64 mac>" — Bitwarden's own ciphertext
  # format. Nothing else looks like this; its presence means vault data.
  if printf '%s' "$added" | grep -Eq '[0-9]\.[A-Za-z0-9+/=]{20,}\|[A-Za-z0-9+/=]{20,}\|[A-Za-z0-9+/=]{20,}'; then
    report "Bitwarden EncString in $file" \
      "That is encrypted vault content. It does not belong in source."
  fi

  # ── 3. The shape of a decrypted vault item ───────────────────────────────
  # bw stamps every object it emits with "object":"item"/"folder". Combined with
  # a password or TOTP field, this is a decrypted dump rather than a fixture.
  if printf '%s' "$added" | grep -Eq '"object"[[:space:]]*:[[:space:]]*"(item|folder|cipherDetails)"' \
     && printf '%s' "$added" | grep -Eq '"(password|totp|privateKey)"[[:space:]]*:[[:space:]]*"[^"]{4,}'; then
    report "Decrypted vault item in $file" \
      "Looks like 'bw list items' output with real secrets. Use a redacted fixture."
  fi

  # ── 4. Private keys ──────────────────────────────────────────────────────
  if printf '%s' "$added" | grep -Eq -- '-----BEGIN [A-Z ]*PRIVATE KEY-----'; then
    report "Private key block in $file" "Never commit a private key."
  fi

  # ── 5. Exact vendor token shapes ─────────────────────────────────────────
  # Each of these is a documented, unambiguous prefix+length. No guessing.
  if printf '%s' "$added" | grep -Eq '(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{50,}'; then
    report "GitHub token in $file" "Revoke it, then remove it from the change."
  fi
  if printf '%s' "$added" | grep -Eq 'AKIA[0-9A-Z]{16}'; then
    report "AWS access key id in $file" "Revoke it immediately."
  fi
  if printf '%s' "$added" | grep -Eq 'xox[baprs]-[A-Za-z0-9-]{10,}'; then
    report "Slack token in $file" "Revoke it immediately."
  fi
  if printf '%s' "$added" | grep -Eq '(sk|rk)_(live|test)_[A-Za-z0-9]{20,}'; then
    report "Stripe key in $file" "Revoke it immediately."
  fi
  if printf '%s' "$added" | grep -Eq 'AIza[0-9A-Za-z_-]{35}'; then
    report "Google API key in $file" "Revoke it immediately."
  fi
  # NuGet keys matter here: the sibling repos publish packages.
  if printf '%s' "$added" | grep -Eq 'oy2[a-z0-9]{43}'; then
    report "NuGet API key in $file" "Revoke it on nuget.org."
  fi

  # ── 6. An assigned BW_SESSION ────────────────────────────────────────────
  # The session key decrypts the entire vault. Only flagged when it is being
  # given a literal value — referring to the variable is normal and fine.
  if printf '%s' "$added" | grep -Eq 'BW_SESSION[[:space:]]*=[[:space:]]*["'"'"']?[A-Za-z0-9+/]{40,}={0,2}'; then
    report "Hard-coded BW_SESSION in $file" \
      "That key decrypts the whole vault. Read it from the environment instead."
  fi

  # ── 7. A master password assigned in code ────────────────────────────────
  if printf '%s' "$added" | grep -Eiq '(master_?password|masterpw)[[:space:]]*[=:][[:space:]]*["'"'"'][^"'"'"']{6,}'; then
    report "Hard-coded master password in $file" "Never. Prompt for it."
  fi
done

if [ "$findings" -gt 0 ]; then
  printf "\n${YELLOW}%s finding(s). Commit blocked.${OFF}\n" "$findings" >&2
  printf "${DIM}   Genuinely a false positive? Bypass once: git commit --no-verify${OFF}\n" >&2
  printf "${DIM}   If a real secret already reached a commit, rotate it — deleting it later${OFF}\n" >&2
  printf "${DIM}   does not remove it from git history.${OFF}\n" >&2
  exit 1
fi

exit 0
