using BitwardenSharp.Application.Merging;
using BitwardenSharp.Domain.Duplicates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>What has happened to a group during this session's queue run.</summary>
public enum QueueState
{
    Pending,
    Merged,
    Skipped,
    Failed,
}

/// <summary>One row in the duplicate queue.</summary>
public sealed partial class DuplicateGroupViewModel : ViewModelBase
{
    public DuplicateGroupViewModel(DuplicateGroup group)
    {
        Group = group;
        Survivor = new ItemViewModel(group.Survivor);

        // Every group carries the default draft from the start, so the row can describe exactly
        // what Approve would do without the editor ever being opened.
        Draft = MergeDraft.Default(group);
    }

    public DuplicateGroup Group { get; }

    public ItemViewModel Survivor { get; }

    /// <summary>Replaced when the editor resolves the group differently.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanSummary), nameof(KeepsName), nameof(DropCount))]
    private MergeDraft _draft;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(StateGlyph))]
    private QueueState _state = QueueState.Pending;

    [ObservableProperty] private string? _stateDetail;

    /// <summary>Whether this row is ticked for a bulk approve.</summary>
    [ObservableProperty] private bool _isSelected;

    public string Id => Group.Id;
    public DuplicateCategory Category => Group.Category;
    public string Key => Group.Key;
    public int MemberCount => Group.Members.Count;
    public bool CanMerge => Group.CanMerge;
    public bool IsPending => State == QueueState.Pending;

    /// <summary>
    /// Whether the default is safe to approve without looking. True only for the categories that
    /// are mergeable at all and where the credentials already agree — which is the whole point of
    /// the fast path: those decisions are cosmetic.
    /// </summary>
    public bool IsRoutine =>
        CanMerge
        && Category is DuplicateCategory.ExactDuplicate or DuplicateCategory.RelatedDomain
        && Draft.Overwrites.Count == 0;

    /// <summary>Groups that need a human: a real credential conflict, or a blocking warning.</summary>
    public bool NeedsAttention => !IsRoutine;

    public string KeepsName => Draft.TargetItem?.Name ?? "a new item";

    public int DropCount => Draft.Doomed.Count();

    public string PlanSummary => Draft.Target.IsNewItem
        ? $"create a new item, delete all {MemberCount}"
        : $"keep \"{KeepsName}\", delete {DropCount}";

    public IReadOnlyList<MergeWarning> Warnings => Group.Warnings;

    public bool HasWarnings => Group.Warnings.Count > 0;

    public string WarningSummary => string.Join(" · ", Group.Warnings.Select(w => w.Message));

    public string StateGlyph => State switch
    {
        QueueState.Merged => "✓",
        QueueState.Skipped => "—",
        QueueState.Failed => "✗",
        _ => "",
    };

    public string CategoryLabel => Category switch
    {
        DuplicateCategory.ExactDuplicate => "same site",
        DuplicateCategory.RelatedDomain => "related site",
        DuplicateCategory.CredentialConflict => "different passwords",
        DuplicateCategory.InfrastructureSharedCredential => "shared across hosts",
        DuplicateCategory.SameName => "same name",
        _ => Category.ToString(),
    };
}
