using System.Collections.ObjectModel;

namespace BitwardenSharp.Desktop.ViewModels;

/// <summary>
/// One node in the folder tree implied by Bitwarden's slash-separated folder names.
/// </summary>
/// <remarks>
/// A node may exist purely as a path segment: if the vault has "Homelab/Proxmox" but no folder
/// literally named "Homelab", the parent is synthesised and has no <see cref="FolderId"/> of its
/// own. Such a node holds no items directly but still aggregates its children.
/// </remarks>
public sealed class FolderNode(string name, string path)
{
    public string Name { get; } = name;

    public string Path { get; } = path;

    /// <summary>Null when this node is only an implied path segment, not a real folder.</summary>
    public string? FolderId { get; set; }

    public int DirectCount { get; set; }

    public ObservableCollection<FolderNode> Children { get; } = [];

    /// <summary>Items here and in everything below, which is what selecting this node shows.</summary>
    public int TotalCount => DirectCount + Children.Sum(c => c.TotalCount);

    public string Label => Children.Count == 0 || DirectCount == TotalCount
        ? $"{Name}  ({TotalCount})"
        : $"{Name}  ({DirectCount} / {TotalCount})";

    /// <summary>Every real folder id at or below this node.</summary>
    public IEnumerable<string> DescendantFolderIds()
    {
        if (FolderId is not null) yield return FolderId;
        foreach (var id in Children.SelectMany(c => c.DescendantFolderIds())) yield return id;
    }
}
