using System.Collections.ObjectModel;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Application.Duplicates;
using BitwardenSharp.Application.Merging;
using BitwardenSharp.Desktop.Services;
using BitwardenSharp.Domain.Duplicates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>
/// The duplicate queue: every group the scanner found, with the merge it proposes.
/// </summary>
/// <remarks>
/// The split between routine and needs-attention is the point of this screen. On a real vault the
/// overwhelming majority of mergeable groups involve no credential decision at all — the members
/// agree on username and password by definition, and only the name, folder and URI set differ.
/// Routing all of those through a three-pane editor would be ceremony. They get one click; the
/// ones that genuinely conflict get the editor.
/// </remarks>
public sealed partial class DuplicatesViewModel(
    IVaultClient vault,
    DuplicateScanner scanner,
    MergeExecutor executor,
    IconLoader iconLoader) : ViewModelBase
{
    public event Action? Closed;

    /// <summary>Asks the view to confirm something destructive. Returns false if declined.</summary>
    public Func<string, string, Task<bool>>? Confirm { get; set; }

    public ObservableCollection<DuplicateGroupViewModel> Groups { get; } = [];

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _statusLine = string.Empty;
    [ObservableProperty] private string? _progress;

    /// <summary>Non-null while the three-pane editor is open over the queue.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorOpen))]
    private MergeEditorViewModel? _editor;

    public bool IsEditorOpen => Editor is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleGroups))]
    private bool _showRoutine = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleGroups))]
    private bool _showReviewOnly = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleGroups))]
    private bool _showResolved;

    public IEnumerable<DuplicateGroupViewModel> VisibleGroups => Groups.Where(g =>
        (ShowResolved || g.IsPending)
        && (g.IsRoutine ? ShowRoutine : ShowReviewOnly));

    public int RoutineCount => Groups.Count(g => g.IsRoutine && g.IsPending);

    public int AttentionCount => Groups.Count(g => g.NeedsAttention && g.IsPending);

    public int PendingDeletions => Groups.Where(g => g.IsRoutine && g.IsPending).Sum(g => g.DropCount);

    // ── loading ──────────────────────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        IsBusy = true;
        Error = null;
        Progress = "Reading the vault…";
        try
        {
            await vault.SyncAsync();
            var items = await vault.GetItemsAsync();

            Progress = "Scanning for duplicates…";
            var result = scanner.Scan(items);

            Groups.Clear();
            foreach (var group in result.Groups) Groups.Add(new DuplicateGroupViewModel(group));

            StatusLine =
                $"{result.LoginCount} logins · {result.Groups.Count} groups · "
                + $"{result.MergeableDeletions} deletions available";

            RefreshCounts();
            _ = LoadIconsAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
            Progress = null;
        }
    }

    private async Task LoadIconsAsync()
    {
        if (!iconLoader.IsEnabled) return;
        foreach (var byDomain in Groups
                     .Select(g => g.Survivor)
                     .Where(s => s.IconDomain is not null)
                     .GroupBy(s => s.IconDomain!))
        {
            var icon = await iconLoader.GetAsync(byDomain.Key);
            if (icon is null) continue;
            foreach (var survivor in byDomain) survivor.Icon = icon;
        }
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(VisibleGroups));
        OnPropertyChanged(nameof(RoutineCount));
        OnPropertyChanged(nameof(AttentionCount));
        OnPropertyChanged(nameof(PendingDeletions));
        ApproveAllRoutineCommand.NotifyCanExecuteChanged();
    }

    // ── the fast path ────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ApproveAsync(DuplicateGroupViewModel row)
    {
        if (Confirm is null || row is null) return;

        var message =
            $"{row.PlanSummary}.\n\nDeleted items go to Bitwarden's trash and stay restorable for 30 days.";
        if (row.Draft.Overwrites.Count > 0)
            message = "This replaces values on the item being kept:\n"
                      + string.Join("\n", row.Draft.Overwrites.Select(o => $"  {o.Field}: {o.Before} → {o.After}"))
                      + "\n\n" + message;

        if (!await Confirm($"Merge {row.Id}", message)) return;

        await RunAsync([row]);
    }

    private bool CanApproveAllRoutine => RoutineCount > 0;

    [RelayCommand(CanExecute = nameof(CanApproveAllRoutine))]
    private async Task ApproveAllRoutineAsync()
    {
        if (Confirm is null) return;

        var rows = Groups.Where(g => g.IsRoutine && g.IsPending).ToList();
        var deletions = rows.Sum(r => r.DropCount);

        var confirmed = await Confirm(
            "Approve routine merges",
            $"Merge {rows.Count} group(s), deleting {deletions} item(s).\n\n"
            + "Every one of these keeps the credentials unchanged — only names, folders and URI "
            + "lists differ. Nothing here replaces a password.\n\n"
            + "Deleted items go to Bitwarden's trash and stay restorable for 30 days.");
        if (!confirmed) return;

        await RunAsync(rows);
    }

    private async Task RunAsync(IReadOnlyList<DuplicateGroupViewModel> rows)
    {
        IsBusy = true;
        Error = null;
        var merged = 0;
        var failed = 0;

        try
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                Progress = $"Merging {i + 1} of {rows.Count} — {row.Key}";

                var outcome = await executor.ApplyAsync(row.Draft, dryRun: false);

                row.StateDetail = outcome.Message ?? string.Join(", ", outcome.Changes);
                row.State = outcome.Status switch
                {
                    MergeStatus.Merged => QueueState.Merged,
                    MergeStatus.Skipped => QueueState.Skipped,
                    _ => QueueState.Failed,
                };

                if (row.State == QueueState.Merged) merged++;
                else if (row.State == QueueState.Failed) failed++;
            }

            await vault.SyncAsync();
            StatusLine = $"{merged} merged"
                         + (failed > 0 ? $", {failed} failed — see the rows marked ✗" : string.Empty);
            if (failed > 0)
                Error = $"{failed} merge(s) did not complete. Nothing was deleted for those groups.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
            Progress = null;
            RefreshCounts();
        }
    }

    // ── the editor ───────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenEditor(DuplicateGroupViewModel row)
    {
        if (row is null) return;

        var editor = new MergeEditorViewModel(row);
        editor.Cancelled += () => Editor = null;
        editor.Committed += async draft =>
        {
            row.Draft = draft;
            Editor = null;
            await RunAsync([row]);
        };
        Editor = editor;
    }

    [RelayCommand]
    private void DismissError() => Error = null;

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void Close() => Closed?.Invoke();
}
