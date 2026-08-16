using System.Collections.ObjectModel;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Application.Folders;
using BitwardenSharp.Desktop.Services;
using BitwardenSharp.Domain.Vault;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>The vault browser: folder tree on the left, items in the middle, detail on the right.</summary>
public sealed partial class VaultViewModel(
    IVaultClient vault,
    IVaultSession session,
    FolderService folders,
    IconLoader iconLoader) : ViewModelBase
{
    public event Action? Locked;
    public event Action? DuplicatesRequested;

    /// <summary>Raised when the view should ask the user for a name. Returns null if cancelled.</summary>
    public Func<string, string, string?, Task<string?>>? PromptForName { get; set; }

    /// <summary>Raised when the view should ask the user to confirm something destructive.</summary>
    public Func<string, string, Task<bool>>? Confirm { get; set; }

    private IReadOnlyList<VaultItem> _allItems = [];
    private IReadOnlyList<VaultFolder> _allFolders = [];
    private CancellationTokenSource _iconLoad = new();

    public ObservableCollection<FolderNode> Folders { get; } = [];
    public ObservableCollection<ItemViewModel> Items { get; } = [];

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _statusLine = string.Empty;
    [ObservableProperty] private ItemViewModel? _selectedItem;
    [ObservableProperty] private string _search = string.Empty;

    [NotifyPropertyChangedFor(nameof(FilterDescription))]
    [NotifyCanExecuteChangedFor(nameof(RenameFolderCommand), nameof(DeleteFolderCommand))]
    [ObservableProperty]
    private FolderNode? _selectedFolder;

    public string FilterDescription => SelectedFolder?.Path ?? "All items";

    public bool IconsEnabled => iconLoader.IsEnabled;

    partial void OnSelectedFolderChanged(FolderNode? value) => ApplyFilter();

    partial void OnSearchChanged(string value) => ApplyFilter();

    // ── loading ──────────────────────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            await vault.SyncAsync();
            _allFolders = await vault.GetFoldersAsync();
            _allItems = await vault.GetItemsAsync();

            RebuildFolderTree();
            ApplyFilter();

            var status = await session.GetStatusAsync();
            StatusLine = $"{status.UserEmail}  ·  {_allItems.Count} items  ·  {_allFolders.Count} folders";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Reloads folders and items while keeping the selected folder path selected.</summary>
    private async Task ReloadPreservingSelectionAsync()
    {
        var selectedPath = SelectedFolder?.Path;
        await LoadAsync();

        if (selectedPath is null) return;
        SelectedFolder = Folders
            .SelectMany(f => f.SelfAndDescendants())
            .FirstOrDefault(f => f.Path == selectedPath);
    }

    /// <summary>
    /// Bitwarden has no real folder hierarchy — nesting is a naming convention where
    /// "Homelab/Proxmox" is one folder whose name contains a slash. Rebuild the implied tree so
    /// the UI can present it as one, inserting intermediate nodes that have no folder of their own.
    /// </summary>
    private void RebuildFolderTree()
    {
        Folders.Clear();

        var counts = _allItems
            .GroupBy(i => i.FolderId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Count());

        var roots = new List<FolderNode>();
        var index = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in _allFolders
                     .Where(f => f.Id.Length > 0)
                     .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            var segments = FolderPaths.Segments(folder.Name);
            if (segments.Count == 0) continue;

            FolderNode? parent = null;
            var path = string.Empty;

            for (var depth = 0; depth < segments.Count; depth++)
            {
                path = depth == 0 ? segments[depth] : $"{path}/{segments[depth]}";

                if (!index.TryGetValue(path, out var node))
                {
                    node = new FolderNode(segments[depth], path);
                    index[path] = node;
                    (parent?.Children ?? (ICollection<FolderNode>)roots).Add(node);
                }
                parent = node;
            }

            parent!.FolderId = folder.Id;
            parent.DirectCount = counts.GetValueOrDefault(folder.Id, 0);
        }

        var unfiled = counts.GetValueOrDefault(string.Empty, 0);
        if (unfiled > 0)
            roots.Add(new FolderNode("No folder", "￿ unfiled")
            {
                FolderId = string.Empty,
                DirectCount = unfiled,
                IsUnfiled = true,
            });

        foreach (var root in roots) Folders.Add(root);
    }

    private void ApplyFilter()
    {
        var query = _allItems.AsEnumerable();

        if (SelectedFolder is not null)
        {
            // Selecting a parent shows everything beneath it, which is what the slash-naming
            // implies even though Bitwarden stores the folders flat.
            var ids = SelectedFolder.DescendantFolderIds().ToHashSet(StringComparer.Ordinal);
            query = query.Where(i => ids.Contains(i.FolderId ?? string.Empty));
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            query = query.Where(i =>
                i.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (i.Login?.Username?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || i.Uris.Any(u => u.Uri.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        Items.Clear();
        foreach (var item in query.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            Items.Add(new ItemViewModel(item));

        SelectedItem = Items.FirstOrDefault();
        _ = LoadIconsAsync();
    }

    /// <summary>
    /// Fetches icons for what is currently listed.
    /// </summary>
    /// <remarks>
    /// Cancelled and restarted on every filter change: typing in the search box should not leave
    /// hundreds of lookups in flight for rows that are no longer shown. Distinct by domain so a
    /// site with a dozen duplicate entries is fetched once.
    /// </remarks>
    private async Task LoadIconsAsync()
    {
        await _iconLoad.CancelAsync();
        _iconLoad.Dispose();
        _iconLoad = new CancellationTokenSource();
        var token = _iconLoad.Token;

        if (!iconLoader.IsEnabled) return;

        try
        {
            foreach (var group in Items.Where(i => i.IconDomain is not null).GroupBy(i => i.IconDomain!))
            {
                if (token.IsCancellationRequested) return;

                var icon = await iconLoader.GetAsync(group.Key, token);
                if (icon is null) continue;
                foreach (var vm in group) vm.Icon = icon;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected whenever the filter changes mid-flight.
        }
    }

    // ── folder operations ────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        if (PromptForName is null) return;

        // A new folder is created under whatever is selected, mirroring a file explorer.
        var parent = SelectedFolder is { IsUnfiled: false } node ? node.Path : null;
        var name = await PromptForName(
            "New folder",
            parent is null ? "Name" : $"Name (inside {parent})",
            null);
        if (string.IsNullOrWhiteSpace(name)) return;

        await RunFolderOperationAsync(() => folders.CreateAsync(parent, name));
    }

    private bool CanActOnFolder => SelectedFolder?.IsRealFolder == true;

    [RelayCommand(CanExecute = nameof(CanActOnFolder))]
    private async Task RenameFolderAsync()
    {
        if (PromptForName is null || SelectedFolder is not { IsRealFolder: true } node) return;

        var name = await PromptForName("Rename folder", "Name", node.Name);
        if (string.IsNullOrWhiteSpace(name) || name == node.Name) return;

        await RunFolderOperationAsync(() => folders.RenameAsync(node.FolderId!, name));
    }

    [RelayCommand(CanExecute = nameof(CanActOnFolder))]
    private async Task DeleteFolderAsync()
    {
        if (Confirm is null || SelectedFolder is not { IsRealFolder: true } node) return;

        var subtree = node.SelfAndDescendants().Count(n => n.IsRealFolder);
        var items = node.TotalCount;

        var message = subtree > 1
            ? $"Delete \"{node.Path}\" and {subtree - 1} folder(s) beneath it?"
            : $"Delete \"{node.Path}\"?";
        if (items > 0)
            message += $"\n\n{items} item(s) will be moved out of any folder. Nothing is deleted.";

        if (!await Confirm("Delete folder", message)) return;

        await RunFolderOperationAsync(() => folders.DeleteAsync(node.FolderId!));
    }

    /// <summary>Moves items into a folder. Called by the view when a drag is dropped on the tree.</summary>
    public async Task MoveItemsToFolderAsync(IReadOnlyList<string> itemIds, FolderNode target)
    {
        if (itemIds.Count == 0) return;

        // An implied path segment has no folder to move into; unfiled means clearing the folder.
        if (!target.IsUnfiled && !target.IsRealFolder)
        {
            Error = $"\"{target.Path}\" isn't a real folder yet — create it before moving items into it.";
            return;
        }

        var folderId = target.IsUnfiled ? null : target.FolderId;
        await RunFolderOperationAsync(() => folders.MoveItemsAsync(itemIds, folderId));
    }

    /// <summary>Moves a folder under another. Called by the view on a folder-to-folder drop.</summary>
    public async Task MoveFolderAsync(FolderNode source, FolderNode? target)
    {
        if (!source.IsRealFolder) return;
        if (target is { IsUnfiled: true }) return;
        if (target is not null && target.Path == source.Path) return;

        await RunFolderOperationAsync(() => folders.MoveAsync(source.FolderId!, target?.Path));
    }

    private async Task RunFolderOperationAsync(Func<Task<FolderOperationResult>> operation)
    {
        IsBusy = true;
        Error = null;
        try
        {
            var result = await operation();
            if (!result.Succeeded)
            {
                Error = result.Error;
                return;
            }
            await ReloadPreservingSelectionAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── misc ─────────────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenDuplicates() => DuplicatesRequested?.Invoke();

    [RelayCommand]
    private void ClearFolder() => SelectedFolder = null;

    [RelayCommand]
    private void DismissError() => Error = null;

    [RelayCommand]
    private async Task RefreshAsync() => await ReloadPreservingSelectionAsync();

    [RelayCommand]
    private async Task LockAsync()
    {
        await _iconLoad.CancelAsync();
        await session.LockAsync();
        Locked?.Invoke();
    }
}
