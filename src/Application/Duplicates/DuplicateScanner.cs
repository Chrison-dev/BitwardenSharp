using BitwardenSharp.Domain.Duplicates;
using BitwardenSharp.Domain.Uris;
using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Application.Duplicates;

/// <summary>
/// Finds sets of login items that describe the same account, and classifies each set by how
/// safely it can be collapsed.
/// </summary>
/// <remarks>
/// <para>
/// The ordering of the passes matters. Each pass claims the items it groups, so a later, weaker
/// signal cannot re-group items an earlier, stronger one already explained.
/// </para>
/// <para>
/// <b>A shared password is not evidence of a duplicate.</b> Password reuse is common enough that
/// on a real vault a single password can cover hundreds of unrelated accounts, so "same
/// credentials" only ever promotes a group that some other signal — same registrable target, same
/// brand, same service family — has already established. An earlier version of this rule accepted
/// a group when *any one* of its domains belonged to a known family, which swept unrelated sites
/// together and proposed deleting live accounts. See the RelatedCredentials pass.
/// </para>
/// </remarks>
public sealed class DuplicateScanner
{
    /// <summary>Groups the login items in <paramref name="items"/>, strongest signal first.</summary>
    public DuplicateScanResult Scan(IReadOnlyList<VaultItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var logins = items
            .Where(i => i.Type == ItemType.Login && i.Login is not null)
            .OrderBy(i => i.Id, StringComparer.Ordinal)
            .ToList();

        var targets = logins.ToDictionary(
            i => i.Id,
            i => i.Uris.Select(u => UriTarget.Parse(u.Uri)).OfType<UriTarget>().Distinct().ToList());

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<DuplicateGroup>();
        var counters = new Dictionary<DuplicateCategory, int>();

        void Emit(DuplicateCategory category, string key, IReadOnlyList<VaultItem> members, bool claim)
        {
            var signature = Signature(members);
            if (members.Count < 2 || !seen.Add(signature)) return;

            counters[category] = counters.GetValueOrDefault(category) + 1;
            groups.Add(Build($"{Prefix(category)}-{counters[category]:D3}", category, key, members, targets));
            if (claim) foreach (var m in members) claimed.Add(m.Id);
        }

        // ── Pass 1: same target, same username, same password ────────────────────────────────
        // The strongest signal there is. Everything here is one account the vault recorded twice.
        var byTargetUser = new Dictionary<(string Target, string User), List<VaultItem>>();
        foreach (var item in logins)
        {
            var user = item.Login!.NormalisedUsername;
            if (user is null || item.Login.PasswordFingerprint is null) continue;
            foreach (var target in targets[item.Id])
                byTargetUser.GetOrAdd((target.Value, user)).Add(item);
        }

        var conflicts = new List<((string Target, string User) Key, List<VaultItem> Members)>();
        foreach (var (key, members) in byTargetUser.OrderBy(kv => kv.Key))
        {
            var distinct = members.DistinctBy(m => m.Id).ToList();
            if (distinct.Count < 2) continue;

            var byPassword = distinct.GroupBy(m => m.Login!.PasswordFingerprint!).ToList();
            foreach (var sharing in byPassword.Where(g => g.Count() > 1))
                Emit(DuplicateCategory.ExactDuplicate, $"{key.Target} · {key.User}", [.. sharing], claim: true);

            // Same door, same name, different keys — one of them is stale. Held for pass 3 so
            // that any exact duplicates inside it are recognised as such first.
            if (byPassword.Count > 1) conflicts.Add((key, distinct));
        }

        // ── Pass 2: same credentials across related targets ──────────────────────────────────
        var byCredential = new Dictionary<(string User, string Password), List<VaultItem>>();
        foreach (var item in logins)
        {
            var user = item.Login!.NormalisedUsername;
            var password = item.Login.PasswordFingerprint;
            if (user is null || password is null) continue;
            byCredential.GetOrAdd((user, password)).Add(item);
        }

        foreach (var (key, members) in byCredential.OrderBy(kv => kv.Key))
        {
            if (members.Count < 2) continue;

            var distinctTargets = members.SelectMany(m => targets[m.Id]).Distinct().ToList();
            if (distinctTargets.Count < 2) continue;

            var infrastructure = distinctTargets
                .Where(t => t.Kind is UriTargetKind.IpAddress or UriTargetKind.Host)
                .ToList();
            var domains = distinctTargets.Where(t => t.Kind == UriTargetKind.Domain).ToList();

            if (infrastructure.Count > 1)
            {
                // Distinct machines that happen to share a login. Merging would delete the
                // inventory of every host but one.
                Emit(DuplicateCategory.InfrastructureSharedCredential,
                    $"{key.User} · {infrastructure.Count} hosts", members, claim: false);
                continue;
            }

            // Related only if EVERY domain agrees — one shared brand, or one shared family with
            // no unrecognised brand among them. Anything else is reuse across unrelated sites.
            var brands = domains.Select(d => d.Brand).Distinct().ToList();
            var families = domains.Select(ServiceFamily.ForTarget).ToList();

            var sameBrand = brands.Count == 1;
            var sameFamily = domains.Count > 0
                             && families.All(f => f is not null)
                             && families.Distinct().Count() == 1;

            if (sameBrand || sameFamily)
                Emit(DuplicateCategory.RelatedDomain,
                    $"{key.User} · {string.Join(", ", distinctTargets.Select(t => t.Value).Order())}",
                    members, claim: true);
        }

        // ── Pass 3: same target and username, different passwords ────────────────────────────
        foreach (var (key, members) in conflicts)
            Emit(DuplicateCategory.CredentialConflict,
                $"{key.Target} · {key.User}", members, claim: false);

        // ── Pass 4: identical name, nothing else in common ───────────────────────────────────
        foreach (var byName in logins
                     .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                     .GroupBy(i => i.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var members = byName.ToList();
            if (members.Count < 2 || members.Any(m => claimed.Contains(m.Id))) continue;
            Emit(DuplicateCategory.SameName, byName.Key, members, claim: false);
        }

        return new DuplicateScanResult
        {
            TotalItems = items.Count,
            LoginCount = logins.Count,
            Groups = groups,
        };
    }

    private static string Signature(IEnumerable<VaultItem> members) =>
        string.Join('|', members.Select(m => m.Id).Order(StringComparer.Ordinal));

    private static string Prefix(DuplicateCategory category) => category switch
    {
        DuplicateCategory.ExactDuplicate => "EXACT",
        DuplicateCategory.RelatedDomain => "RELATED",
        DuplicateCategory.CredentialConflict => "CONFLICT",
        DuplicateCategory.InfrastructureSharedCredential => "INFRA",
        DuplicateCategory.SameName => "NAME",
        _ => "GROUP",
    };

    private static DuplicateGroup Build(
        string id,
        DuplicateCategory category,
        string key,
        IReadOnlyList<VaultItem> members,
        IReadOnlyDictionary<string, List<UriTarget>> targets)
    {
        // Richest survives: everything the others hold can be copied onto it, and an attachment
        // cannot be moved between items at all. Newest revision breaks a tie.
        var survivor = members
            .OrderByDescending(m => m.Richness)
            .ThenByDescending(m => m.RevisionDate ?? DateTimeOffset.MinValue)
            .ThenBy(m => m.Id, StringComparer.Ordinal)
            .First();

        return new DuplicateGroup
        {
            Id = id,
            Category = category,
            Key = key,
            Survivor = survivor,
            Members = members,
            Warnings = MergeWarnings.For(members, survivor),
        };
    }
}

internal static class DictionaryExtensions
{
    public static List<TValue> GetOrAdd<TKey, TValue>(
        this Dictionary<TKey, List<TValue>> source, TKey key) where TKey : notnull
    {
        if (source.TryGetValue(key, out var existing)) return existing;
        return source[key] = [];
    }
}
