using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Application.Merging;

/// <summary>The merged item, and a human-readable account of what changed to produce it.</summary>
public sealed record MergeResult(VaultItem Merged, IReadOnlyList<string> Changes);

/// <summary>
/// Folds the losers of a duplicate group onto its survivor. Purely additive: the survivor's own
/// name, username, password and folder are never overwritten, so the worst case of a wrong
/// grouping is an item carrying a URI it did not need — not a changed credential.
/// </summary>
public static class MergeBuilder
{
    internal const string NoteSeparator = "\n\n--- merged ---\n";

    /// <summary>
    /// Builds the item a resolved <see cref="MergeDraft"/> describes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the additive overload, this one may replace the target's own values — that is the
    /// point of the editor. The one replacement worth special handling is the password: when a
    /// draft displaces one, the displaced value is pushed onto
    /// <see cref="VaultItem.PasswordHistory"/> rather than dropped.
    /// </para>
    /// <para>
    /// That matters because picking the wrong side of a credential conflict is the single way this
    /// tool can lose something irreplaceable. The losing item goes to the trash and is restorable
    /// for 30 days; the displaced password stays on the surviving item indefinitely. Two
    /// independent recovery paths for one irreversible decision.
    /// </para>
    /// </remarks>
    public static MergeResult Build(MergeDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var changes = new List<string>();
        var target = draft.TargetItem;

        // A new item inherits the type from the group and nothing else; there is no prior state.
        var basis = target ?? new VaultItem
        {
            Id = string.Empty,
            Type = draft.Group.Survivor.Type,
            Name = draft.Name.Value,
        };

        var history = basis.PasswordHistory.ToList();
        if (target is not null
            && !string.IsNullOrEmpty(target.Login?.Password)
            && !string.Equals(target.Login.Password, draft.Password.Value, StringComparison.Ordinal))
        {
            history.Insert(0, new PasswordHistoryEntry
            {
                Password = target.Login.Password,
                LastUsedDate = target.Login.PasswordRevisionDate ?? target.RevisionDate,
            });
            changes.Add("password replaced (previous value kept in password history)");
        }

        foreach (var (field, _, _) in draft.Overwrites.Where(o => o.Field != "Password"))
            changes.Add($"{field} replaced");

        var addedUris = draft.Uris.Count - basis.Uris.Count;
        if (addedUris > 0) changes.Add($"+{addedUris} uri(s)");

        var addedFields = draft.Fields.Count - basis.Fields.Count;
        if (addedFields > 0) changes.Add($"+{addedFields} custom field(s)");

        var login = basis.Login ?? new LoginDetails();
        var merged = basis with
        {
            Name = draft.Name.Value,
            FolderId = draft.FolderId.Value,
            Notes = string.IsNullOrWhiteSpace(draft.Notes.Value) ? null : draft.Notes.Value,
            Favorite = draft.Favorite.Value,
            Fields = draft.Fields,
            PasswordHistory = history,
            Login = basis.Type == ItemType.Login || draft.Group.Survivor.Type == ItemType.Login
                ? login with
                {
                    Username = draft.Username.Value,
                    Password = draft.Password.Value,
                    Totp = draft.Totp.Value,
                    Uris = draft.Uris,
                }
                : basis.Login,
        };

        return new MergeResult(merged, changes);
    }

    public static MergeResult Build(VaultItem survivor, IReadOnlyList<VaultItem> losers)
    {
        ArgumentNullException.ThrowIfNull(survivor);
        ArgumentNullException.ThrowIfNull(losers);

        var changes = new List<string>();
        var login = survivor.Login ?? new LoginDetails();

        // ── URIs: union, keeping each entry's own match rule ──────────────────────────────────
        var uris = login.Uris.ToList();
        var seenUris = new HashSet<string>(
            uris.Select(u => u.Uri.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var uri in losers.SelectMany(l => l.Uris))
        {
            if (string.IsNullOrWhiteSpace(uri.Uri) || !seenUris.Add(uri.Uri.Trim())) continue;
            uris.Add(uri);
            changes.Add($"+uri {uri.Uri}");
        }

        // ── TOTP: adopt only when unambiguous ─────────────────────────────────────────────────
        var totp = login.Totp;
        if (string.IsNullOrWhiteSpace(totp))
        {
            var seeds = losers
                .Select(l => l.Login?.Totp)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Two differing seeds is a blocking warning upstream; refusing again here keeps the
            // builder correct on its own, without depending on the caller having checked.
            if (seeds.Count == 1)
            {
                totp = seeds[0];
                changes.Add("+totp (adopted from a deleted item)");
            }
        }

        // ── Custom fields: add only names the survivor lacks ──────────────────────────────────
        var fields = survivor.Fields.ToList();
        var seenFields = new HashSet<string>(
            fields.Select(f => f.Name ?? string.Empty), StringComparer.OrdinalIgnoreCase);
        foreach (var field in losers.SelectMany(l => l.Fields))
        {
            if (!seenFields.Add(field.Name ?? string.Empty)) continue;
            fields.Add(field);
            changes.Add($"+field {field.Name}");
        }

        // ── Notes: append anything not already present ────────────────────────────────────────
        var notes = survivor.Notes?.Trim() ?? string.Empty;
        foreach (var loser in losers)
        {
            var note = loser.Notes?.Trim();
            if (string.IsNullOrEmpty(note) || notes.Contains(note, StringComparison.Ordinal)) continue;
            notes = notes.Length == 0 ? note : notes + NoteSeparator + note;
            changes.Add($"+notes from \"{loser.Name}\"");
        }

        // ── Folder: only ever fills a gap ─────────────────────────────────────────────────────
        var folderId = survivor.FolderId;
        if (string.IsNullOrWhiteSpace(folderId))
        {
            folderId = losers.Select(l => l.FolderId).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f));
            if (!string.IsNullOrWhiteSpace(folderId)) changes.Add("+folder (adopted from a deleted item)");
        }

        var merged = survivor with
        {
            FolderId = folderId,
            Notes = notes.Length == 0 ? null : notes,
            Fields = fields,
            Login = login with { Uris = uris, Totp = totp },
        };

        return new MergeResult(merged, changes);
    }
}
