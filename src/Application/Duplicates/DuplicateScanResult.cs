using BitwardenSharp.Domain.Duplicates;

namespace BitwardenSharp.Application.Duplicates;

/// <summary>Everything one scan found.</summary>
public sealed record DuplicateScanResult
{
    public required int TotalItems { get; init; }

    public required int LoginCount { get; init; }

    public required IReadOnlyList<DuplicateGroup> Groups { get; init; }

    public IEnumerable<DuplicateGroup> Mergeable => Groups.Where(g => g.CanMerge);

    public IEnumerable<DuplicateGroup> NeedingReview => Groups.Where(g => !g.CanMerge);

    /// <summary>How many items would be deleted if every mergeable group were applied.</summary>
    public int MergeableDeletions => Mergeable.Sum(g => g.Losers.Count());

    public IReadOnlyDictionary<DuplicateCategory, int> CountByCategory =>
        Groups.GroupBy(g => g.Category).ToDictionary(g => g.Key, g => g.Count());
}
