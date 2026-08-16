using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>
/// One node in the folder tree implied by Bitwarden's slash-separated folder names.
/// </summary>
/// <remarks>
/// A node may exist purely as a path segment: if the vault has "Homelab/Proxmox" but no folder
/// literally named "Homelab", the parent is synthesised. Such a node has no <see cref="FolderId"/>,
/// holds no items directly, and cannot be renamed or deleted — there is nothing in the vault to
/// rename. It can still be dropped onto, because creating a child under it is a valid new name.
/// </remarks>
public sealed partial class FolderNode(string name, string path) : ObservableObject
{
    public string Name { get; } = name;

    /// <summary>Full slash path, which is also the folder's name in Bitwarden.</summary>
    public string Path { get; } = path;

    /// <summary>Null when this node is only an implied path segment, not a real folder.</summary>
    public string? FolderId { get; set; }

    /// <summary>True for the synthetic "No folder" node, which is a filter rather than a folder.</summary>
    public bool IsUnfiled { get; init; }

    /// <summary>Whether this node corresponds to a folder that can be renamed or deleted.</summary>
    public bool IsRealFolder => FolderId is { Length: > 0 } && !IsUnfiled;

    public int DirectCount { get; set; }

    public ObservableCollection<FolderNode> Children { get; } = [];

    [ObservableProperty] private bool _isExpanded = true;

    /// <summary>Highlighted while a drag hovers over it.</summary>
    [ObservableProperty] private bool _isDropTarget;

    /// <summary>Items here and in everything below, which is what selecting this node shows.</summary>
    public int TotalCount => DirectCount + Children.Sum(c => c.TotalCount);

    public string CountLabel => Children.Count == 0 || DirectCount == TotalCount
        ? TotalCount.ToString()
        : $"{DirectCount} / {TotalCount}";

    /// <summary>Every real folder id at or below this node.</summary>
    public IEnumerable<string> DescendantFolderIds()
    {
        if (FolderId is not null) yield return FolderId;
        foreach (var id in Children.SelectMany(c => c.DescendantFolderIds())) yield return id;
    }

    /// <summary>Depth-first walk including this node.</summary>
    public IEnumerable<FolderNode> SelfAndDescendants()
    {
        yield return this;
        foreach (var node in Children.SelectMany(c => c.SelfAndDescendants())) yield return node;
    }
}

/// <summary>Paints a folder row while a drag hovers over it.</summary>
internal sealed class DropTargetBrushConverter : Avalonia.Data.Converters.IValueConverter
{
    private static readonly Avalonia.Media.IBrush Highlight =
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x40, 0x4C, 0x6E, 0xF5));

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is true ? Highlight : Avalonia.Media.Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
