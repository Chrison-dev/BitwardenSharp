using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using BitwardenSharp.Desktop.ViewModels;
using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Desktop.Views;

/// <summary>
/// Code-behind for the vault browser: drag-and-drop and dialog plumbing.
/// </summary>
/// <remarks>
/// Both belong here rather than in the view-model. Dragging is a gesture made of pointer
/// positions and hit-tests, and a modal dialog needs an owner window — neither is something a
/// view-model should know about. The view-model exposes <see cref="VaultViewModel.PromptForName"/>
/// and <see cref="VaultViewModel.Confirm"/> as callbacks so it can ask for input without ever
/// referencing a <see cref="Window"/>.
/// </remarks>
public partial class VaultView : UserControl
{
    /// <summary>
    /// In-process drag formats. Avalonia 12's <c>CreateInProcessFormat</c> carries the actual
    /// object rather than a serialized blob, so a drag never has to round-trip through a string —
    /// and the payload cannot leak to another application, which matters when it is vault data.
    /// </summary>
    private static readonly DataFormat<string[]> ItemIdsFormat =
        DataFormat.CreateInProcessFormat<string[]>("bitwardensharp.item-ids");

    private static readonly DataFormat<FolderNode> FolderFormat =
        DataFormat.CreateInProcessFormat<FolderNode>("bitwardensharp.folder");

    /// <summary>
    /// A neutral label put on the system pasteboard alongside the in-process payload.
    /// </summary>
    /// <remarks>
    /// macOS requires at least one pasteboard item per drag image and aborts the process
    /// otherwise — an in-process format alone leaves the pasteboard empty, and AppKit raises
    /// NSGenericException ("0 items on the pasteboard, but 1 drag images") which takes the whole
    /// app down with SIGABRT.
    ///
    /// The text is deliberately a fixed marker rather than the item's name. It is the only part
    /// of the drag that another application can see, and an item name can itself be sensitive —
    /// dropping a vault row onto a text editor should reveal nothing.
    /// </remarks>
    private const string PasteboardMarker = "BitwardenSharp item";

    private PointerPressedEventArgs? _pressed;
    private Point _origin;

    public VaultView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is not VaultViewModel vm) return;

            vm.PromptForName = async (title, label, initial) =>
                TopLevel.GetTopLevel(this) is Window owner
                    ? await TextPromptWindow.ShowAsync(owner, title, label, initial)
                    : null;

            vm.Confirm = async (title, message) =>
                TopLevel.GetTopLevel(this) is Window owner
                && await ConfirmWindow.ShowAsync(owner, title, message);
        };

        // Attached routed events, wired here rather than as XAML attributes.
        FolderPane.AddHandler(DragDrop.DragOverEvent, OnRootDragOver);
        FolderPane.AddHandler(DragDrop.DropEvent, OnRootDrop);

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => ClearDropHighlight());
    }

    // ── starting a drag ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Records the press but starts nothing. Beginning a drag here would make every click a drag
    /// and break selection, so the gesture only becomes a drag once the pointer has actually moved.
    /// </summary>
    private void OnDragSourcePressed(object? sender, PointerPressedEventArgs e)
    {
        _pressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ? e : null;
        _origin = e.GetPosition(this);
    }

    private void OnDragSourceReleased(object? sender, PointerReleasedEventArgs e) => _pressed = null;

    private async void OnDragSourceMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is null || sender is not Control { DataContext: { } source }) return;

        // A few pixels of slop, so a slightly shaky click stays a click.
        var delta = e.GetPosition(this) - _origin;
        if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5) return;

        var pressed = _pressed;
        _pressed = null;

        DataTransferItem payload;
        switch (source)
        {
            case ItemViewModel item:
                payload = DataTransferItem.Create(ItemIdsFormat, new[] { item.Id });
                break;
            case FolderNode { IsRealFolder: true } folder:
                payload = DataTransferItem.Create(FolderFormat, folder);
                break;
            default:
                return;
        }

        // The real payload rides the in-process format; this only exists so the platform has a
        // pasteboard item to attach the drag image to. See PasteboardMarker.
        payload.SetText(PasteboardMarker);

        var transfer = new DataTransfer();
        transfer.Add(payload);

        try
        {
            await DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            // async void: nothing may escape, or the process dies mid-gesture.
            if (DataContext is VaultViewModel vm) vm.Error = ex.Message;
        }
        finally
        {
            ClearDropHighlight();
        }
    }

    // ── dropping ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Walks up from whatever was hit to the folder node it belongs to.</summary>
    private static FolderNode? TargetNodeOf(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control.DataContext is FolderNode node)
                return node;
        return null;
    }

    /// <summary>Stops a folder being dropped into its own subtree, which would orphan it.</summary>
    private static bool WouldOrphan(FolderNode target, FolderNode source) =>
        target.Path == source.Path || FolderPaths.IsDescendantOf(target.Path, source.Path);

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        ClearDropHighlight();

        var target = TargetNodeOf(e.Source);
        if (target is null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var items = e.DataTransfer.TryGetValue(ItemIdsFormat);
        var folder = e.DataTransfer.TryGetValue(FolderFormat);

        // "No folder" accepts items — dropping there means unfile them — but never a folder.
        var allowed = items is { Length: > 0 }
            ? target.IsUnfiled || target.IsRealFolder
            : folder is not null && target.IsRealFolder && !WouldOrphan(target, folder);

        e.DragEffects = allowed ? DragDropEffects.Move : DragDropEffects.None;
        if (allowed) target.IsDropTarget = true;

        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        ClearDropHighlight();

        if (DataContext is not VaultViewModel vm) return;
        var target = TargetNodeOf(e.Source);
        if (target is null) return;

        e.Handled = true;

        try
        {
            if (e.DataTransfer.TryGetValue(ItemIdsFormat) is { Length: > 0 } itemIds)
            {
                await vm.MoveItemsToFolderAsync(itemIds, target);
                return;
            }

            if (e.DataTransfer.TryGetValue(FolderFormat) is { } source && !WouldOrphan(target, source))
                await vm.MoveFolderAsync(source, target);
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
    }

    private void ClearDropHighlight()
    {
        if (DataContext is not VaultViewModel vm) return;
        foreach (var node in vm.Folders.SelectMany(f => f.SelfAndDescendants()))
            node.IsDropTarget = false;
    }

    // ── dropping on empty space: move to root ────────────────────────────────────────────────

    private void OnRootDragOver(object? sender, DragEventArgs e)
    {
        // Only when the drop did not land on a node — otherwise the node's own handler owns it.
        if (TargetNodeOf(e.Source) is not null) return;

        e.DragEffects = e.DataTransfer.TryGetValue(FolderFormat) is not null
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnRootDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not VaultViewModel vm) return;
        if (TargetNodeOf(e.Source) is not null) return;
        if (e.DataTransfer.TryGetValue(FolderFormat) is not { } source) return;

        e.Handled = true;
        try
        {
            await vm.MoveFolderAsync(source, target: null);
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
    }
}
