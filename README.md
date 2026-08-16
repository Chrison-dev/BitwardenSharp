# BitwardenSharp

Vault-management tooling for [Bitwarden](https://bitwarden.com) that the official CLI does not
provide: finding duplicate logins, and merging them safely.

`bw` is a fine transport and a poor toolkit. It has no merge, no bulk operations, no dry run, and
every edit is a read → mutate → base64 → write round trip against an item it replaces wholesale.
BitwardenSharp adds the missing layer on top of it.

```
dotnet tool install -g BitwardenSharp.Cli

export BW_SESSION=$(bw unlock --raw)
bwsharp scan                       # read-only; find and classify duplicates
bwsharp merge EXACT-001            # dry run
bwsharp merge EXACT-001 --apply    # write
```

## What it finds

A scan classifies every group by how safely it can be collapsed:

| Category | Meaning | Merged? |
|---|---|---|
| `ExactDuplicate` | Same registrable domain, same username, same password | yes |
| `RelatedDomain` | Same credentials across one brand's TLDs, or one service family | yes |
| `CredentialConflict` | Same site and username, different passwords — one is stale | review |
| `InfrastructureSharedCredential` | One login reused across distinct hosts | review |
| `SameName` | Identical item name, credentials differ | review |

Only the first two are ever merged, and a group in either is still refused when it carries a
blocking warning — an attachment (which `bw` cannot move between items) or two differing TOTP
seeds.

**A shared password is not evidence of a duplicate.** Password reuse is common enough that in a
real vault one password can cover hundreds of unrelated accounts, so matching credentials only
ever promote a group that some stronger signal — same domain, same brand, same family — has
already established. Grouping on credentials alone proposes deleting live accounts.

## How a merge is safe

Merging is `edit` + `delete`, and the ordering is the safety property:

1. Re-read the survivor and every loser from the vault. A scan is a snapshot; acting on a stale
   one could overwrite a change made since.
2. Fold the losers onto the survivor — union the URIs, adopt an unambiguous TOTP seed, add custom
   fields the survivor lacks, append distinct notes, fill an empty folder. Purely additive: the
   survivor's own name, username and password are never overwritten.
3. Write the survivor, then **read it back and verify**.
4. Only then delete the losers, softly, to Bitwarden's trash.

There is no point at which data exists in neither item. A failure anywhere leaves the losers in
place and the operation simply re-runnable. Deletions stay restorable for 30 days.

Dry run is the default; `--apply` is the only way to write.

## Offline scanning

`bwsharp scan --from items.json` runs the same analysis over a saved `bw list items` dump without
unlocking anything. Useful for auditing an export, and for reproducing a scan against a fixed
snapshot. The file-backed vault refuses every write.

## Architecture

An onion, with dependencies pointing inward only — enforced by tests in
`tests/Architecture.Tests`, not by convention.

```
src/Domain               the vault model, URI/eTLD+1 reduction, duplicate value types. Depends on nothing.
src/Application          duplicate detection, survivor selection, merge planning. Owns the ports.
src/Infrastructure       two adapters onto `bw`, plus the wire contracts.
src/Presentation/Cli     the `bwsharp` tool, on Spectre.Console.
src/Presentation/Desktop the GUI, on Avalonia.
```

### Two transports

Both implement the same `IVaultClient` port, and each host picks what suits it:

| | `AddBitwardenCli()` | `AddBitwardenServe()` |
|---|---|---|
| How | one `bw` process per call | one long-lived `bw serve`, HTTP for everything |
| Cost | ~0.5s Node start-up **per call** | ~1.5s once |
| Exposure | none | an **unauthenticated** local port for its lifetime |
| Used by | the CLI — one-shot, so no port is worth opening | the desktop app — outlives every call |

The Vault Management API has no authentication of any kind: anything that can reach the port
reads the whole vault while it is unlocked. The mitigations are structural — loopback only, a
random ephemeral port rather than the well-known 8087, spawned as a child process and killed on
dispose, so the window is exactly the app's lifetime.

One shape to know about if you extend the serve adapter: `/status` nests its payload under
`data.template`, while every other endpoint puts it directly in `data`. The envelope is not
uniform.

### Why it wraps `bw` rather than replacing it

There is no public Bitwarden API for personal vault items — the documented `api.bitwarden.com`
surface is organisation-scoped only (members, groups, collections, policies, events). The
alternatives were to shell out to the official client, or to reimplement Bitwarden's client-side
crypto against its internal endpoints. The latter means owning Argon2id/PBKDF2 derivation and
AES-CBC-HMAC EncString handling against an unsupported, changeable API, in front of a password
vault. Not a trade worth making.

Note also that the official `Bitwarden.Sdk` NuGet package targets Secrets Manager, not personal
vault ciphers.

### Handling secrets

Two rules hold throughout, because process arguments are world-readable via `ps`:

- Arguments go through `ProcessStartInfo.ArgumentList`, never a joined, hand-escaped string.
- **Secrets never become arguments.** The session key travels in the child's environment, and the
  base64 item payload for `edit` — which contains the password in clear — is piped to stdin,
  which `bw` accepts in place of the positional argument.

`LoginDetails` and `CustomField` override the compiler-generated record `ToString` to redact,
so a stray log line or exception message cannot leak a credential.

## Desktop app

```
dotnet run --project src/Presentation/Desktop
```

Avalonia 12. Unlock screen, then a three-pane browser: the folder tree (rebuilt from Bitwarden's
slash-separated flat names), the item list, and a detail pane. Passwords are masked until
revealed, and revealing is per-view — never persisted.

Two rules the desktop host lives by, both learned the hard way:

- **Never block the UI thread on a task.** The first service is resolved on that thread, where
  Avalonia has installed a `SynchronizationContext`. Blocking there to await server start-up
  deadlocked the app before it drew its window — the awaits needed the thread that was waiting
  for them. `BwServeConnection` starts the server on first *awaited* use, and
  `ShutdownRequested` cancels itself, awaits, then shuts down for real. There is no
  `.GetAwaiter().GetResult()` anywhere, and a test enforces that resolution starts nothing.
- **The child must die with us.** A `bw serve` orphaned by a crash keeps an unauthenticated port
  onto an unlocked vault open indefinitely, so cleanup is hooked on both `ProcessExit` and
  `PosixSignalRegistration` for SIGTERM/SIGINT/SIGHUP. `ProcessExit` alone was observed not to
  land in time.

### Folders behave like a file explorer

Create, rename, delete, and drag items or whole folders between nodes. Dropping on "No folder"
unfiles items; dropping a folder on empty space moves it to the root.

**Bitwarden has no folder hierarchy.** "Homelab/Proxmox" is a single folder whose *name* contains
a slash — it is not a child of "Homelab", and "Homelab" need not exist at all. Clients render the
implied tree; the storage is flat. Every tree operation is therefore a bulk rename: moving
"Homelab" without rewriting "Homelab/Proxmox" would show the folder moving and its contents
staying behind. `FolderPaths` plans the whole set of renames — deepest-first, so no two folders
ever momentarily share a name — and refuses collisions and self-nesting before anything is
written. Descendancy compares whole segments, so "Homelab2" is never dragged along with
"Homelab".

Deleting a folder unfiles its items rather than deleting them, and takes the subtree with it —
otherwise "Homelab/Proxmox" would be stranded as a root folder with a slash in its name.

### Website icons

Items show their site's icon, falling back to a coloured initial. Bitwarden stores no icon on an
item: clients derive one from the first URI and fetch it from a hosted service, which is why an
item with no URI never has one anywhere.

**Each lookup discloses a domain from your vault to that service**, and doing it for a whole
vault hands over a list of the sites you hold accounts with — including private ones, where the
hostname alone is information. Three things limit it: only the registrable domain is sent, never
a path; results are cached on disk for a month, misses included, so a domain is asked about once;
and the cache filenames are hashed so the directory is not itself a readable list. Set
`AddBitwardenIcons(o => o.Enabled = false)` and nothing leaves the machine.

### Duplicate queue and merge editor

The **Duplicates** button opens a queue of every group the scanner found, each showing the merge
it proposes. It splits them by whether they need you:

- **Routine** — mergeable, and the members already agree on username and password, so every
  decision is cosmetic: which name, which folder, which URIs. One click each, or approve the lot.
- **Needs a decision** — a real credential conflict, or a blocking warning. These open the editor.

That split is not arbitrary. On the vault this was built against, **the password differs in zero
of the mergeable groups** — it cannot differ, because same-site and same-brand grouping both
require identical credentials. The dangerous decision only exists in the handful of
`CredentialConflict` groups, so routing everything else through a three-pane editor would be
ceremony.

The editor is three panes: the members on the left, one of them compared in the middle, and the
resolved result on the right. The left is a rail rather than a single pane because a real vault
has groups of three to five members and a strict two-pane layout has nowhere to put the rest; for
a two-member group it reads as a plain side-by-side.

Scalars take a value from either side or one you type. Collections — URIs, custom fields — are
unioned with per-element checkboxes, because "additive" only means something for a collection:
you cannot have two usernames. Identical rows are hidden by default, since most properties agree
and showing them buries the ones that don't.

A radio chooses what the result becomes: any member, or a brand-new item. **New item disables
itself when any member holds an attachment** — the CLI cannot move one, so creating a third item
and deleting the sources would destroy the file.

### Next

Retiring dead credentials, and an item template for API keys.

## Building

```
./build.sh          # compile + test
./build.sh TestLive # only the tests that drive a real bw against an unlocked vault
```

The build is [Fallout](https://fallout.build); `.github/workflows/*.yml` is generated from
`build/Build.CI.GitHubActions.cs` and must not be hand-edited.

## Licence

MIT.
