using BitwardenSharp.Domain.Duplicates;
using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Application.Duplicates;

/// <summary>Works out what a human needs to know before approving a group.</summary>
internal static class MergeWarnings
{
    public static IReadOnlyList<MergeWarning> For(IReadOnlyList<VaultItem> members, VaultItem survivor)
    {
        var warnings = new List<MergeWarning>();

        // Blocking. `bw` exposes no way to move an attachment between items, so merging would
        // delete the file along with its item. Refuse rather than lose it.
        var withAttachments = members.Where(m => m.Attachments.Count > 0).ToList();
        if (withAttachments.Count > 0)
        {
            var total = withAttachments.Sum(m => m.Attachments.Count);
            warnings.Add(new MergeWarning(
                "attachments",
                $"{total} attachment(s) across {withAttachments.Count} item(s); the CLI cannot move "
                + "attachments between items, so these must be handled by hand")
            { IsBlocking = true });
        }

        // Blocking. Two different second factors for one account means one of them is wrong, and
        // guessing costs the account.
        var seeds = members
            .Select(m => m.Login?.Totp)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (seeds.Count > 1)
        {
            warnings.Add(new MergeWarning(
                "totp-conflict",
                $"{seeds.Count} differing TOTP seeds in this group; resolve which is current before merging")
            { IsBlocking = true });
        }
        else if (seeds.Count == 1 && string.IsNullOrWhiteSpace(survivor.Login?.Totp))
        {
            warnings.Add(new MergeWarning(
                "totp-transfer",
                "the TOTP seed is on an item being deleted and will be carried onto the survivor"));
        }

        var notes = members
            .Select(m => m.Notes?.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (notes.Count > 1)
            warnings.Add(new MergeWarning("notes", $"{notes.Count} differing notes will be concatenated"));

        var clashing = members
            .SelectMany(m => m.Fields)
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .GroupBy(f => f.Name!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(f => f.Value).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (clashing.Count > 0)
            warnings.Add(new MergeWarning(
                "field-conflict",
                $"custom field(s) with the same name but different values: {string.Join(", ", clashing)}; "
                + "the survivor's value is kept"));

        var folders = members
            .Select(m => m.FolderId)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (folders.Count > 1)
            warnings.Add(new MergeWarning("folder-span", $"members span {folders.Count} different folders"));

        return warnings;
    }
}
