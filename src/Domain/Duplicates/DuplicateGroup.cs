using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Domain.Duplicates;

/// <summary>Something about a group that a human should read before approving it.</summary>
public sealed record MergeWarning(string Code, string Message)
{
    /// <summary>
    /// A warning that makes the group unmergeable outright, rather than merely worth reading.
    /// </summary>
    public bool IsBlocking { get; init; }
}

/// <summary>
/// A set of items believed to describe one account, with the merge already worked out: which
/// item survives, which are deleted, and what has to be carried across first.
/// </summary>
public sealed record DuplicateGroup
{
    /// <summary>Stable within one scan, e.g. <c>EXACT-007</c>. Used to approve groups by name.</summary>
    public required string Id { get; init; }

    public required DuplicateCategory Category { get; init; }

    /// <summary>What the members had in common — the grouping key, for display.</summary>
    public required string Key { get; init; }

    /// <summary>The item that will be kept and updated. Always a member of <see cref="Members"/>.</summary>
    public required VaultItem Survivor { get; init; }

    public required IReadOnlyList<VaultItem> Members { get; init; }

    public IReadOnlyList<MergeWarning> Warnings { get; init; } = [];

    /// <summary>The items that would be deleted, in the order they would be deleted.</summary>
    public IEnumerable<VaultItem> Losers => Members.Where(m => m.Id != Survivor.Id);

    /// <summary>
    /// Whether this group can be applied. A mergeable category still refuses when a blocking
    /// warning is present — an attachment on a loser, for instance, which the CLI cannot move.
    /// </summary>
    public bool CanMerge =>
        Category.Disposition() == MergeDisposition.Mergeable
        && !Warnings.Any(w => w.IsBlocking);

    public override string ToString() =>
        $"{Id} [{Category}] {Key} — keep {Survivor.Name}, drop {Losers.Count()}";
}
