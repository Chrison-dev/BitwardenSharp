namespace BitwardenSharp.Domain.Vault;

/// <summary>One folder that has to be renamed to carry out a tree operation.</summary>
public sealed record FolderRename(string FolderId, string OldName, string NewName);

/// <summary>Why a folder operation was refused, in words a user can act on.</summary>
public sealed record FolderOperationError(string Message);

/// <summary>A planned tree operation: the renames to apply, or the reason it cannot be done.</summary>
public sealed record FolderPlan
{
    public IReadOnlyList<FolderRename> Renames { get; init; } = [];

    public FolderOperationError? Error { get; init; }

    public bool IsValid => Error is null;

    public static FolderPlan Invalid(string message) => new() { Error = new FolderOperationError(message) };

    public static FolderPlan Of(IReadOnlyList<FolderRename> renames) => new() { Renames = renames };
}

/// <summary>
/// Tree operations over Bitwarden's flat folder list.
/// </summary>
/// <remarks>
/// <para>
/// Bitwarden has no folder hierarchy. "Homelab/Proxmox" is a single folder whose <i>name</i>
/// contains a slash; it is not a child of "Homelab", and "Homelab" need not even exist. Clients
/// render the implied tree, but the storage is flat.
/// </para>
/// <para>
/// The consequence is that every tree operation is a bulk rename. Renaming "Homelab" to "Lab"
/// leaves "Homelab/Proxmox" untouched unless it is renamed too — the UI would show the folder
/// moving and its contents staying behind. Everything here therefore plans the full set of
/// renames, including descendants, before anything is written.
/// </para>
/// </remarks>
public static class FolderPaths
{
    public const char Separator = '/';

    /// <summary>Trims each segment and drops empties, so " A / / B " becomes "A/B".</summary>
    public static string Normalise(string name) =>
        string.Join(Separator, Segments(name));

    public static IReadOnlyList<string> Segments(string? name) =>
        (name ?? string.Empty)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The parent path of "A/B/C" is "A/B"; a root folder has none.</summary>
    public static string? Parent(string name)
    {
        var segments = Segments(name);
        return segments.Count <= 1 ? null : string.Join(Separator, segments.Take(segments.Count - 1));
    }

    /// <summary>The last segment: "A/B/C" gives "C".</summary>
    public static string Leaf(string name) => Segments(name).LastOrDefault() ?? string.Empty;

    /// <summary>Whether <paramref name="candidate"/> sits underneath <paramref name="ancestor"/>.</summary>
    /// <remarks>
    /// Compares whole segments, so "Homelab2" is not treated as a child of "Homelab" the way a
    /// naive <c>StartsWith</c> would have it.
    /// </remarks>
    public static bool IsDescendantOf(string candidate, string ancestor)
    {
        var a = Segments(ancestor);
        var c = Segments(candidate);
        return c.Count > a.Count
               && a.Select((segment, i) => string.Equals(segment, c[i], StringComparison.OrdinalIgnoreCase))
                   .All(match => match);
    }

    private static string Join(string? parent, string leaf) =>
        string.IsNullOrEmpty(parent) ? leaf : $"{parent}{Separator}{leaf}";

    /// <summary>Rebases a path from one ancestor onto another, keeping the part below it.</summary>
    private static string Rebase(string path, string oldRoot, string newRoot) =>
        Join(newRoot, string.Join(Separator, Segments(path).Skip(Segments(oldRoot).Count)));

    /// <summary>
    /// Plans renaming a folder's own last segment, carrying every descendant with it.
    /// </summary>
    public static FolderPlan PlanRename(
        IReadOnlyList<VaultFolder> folders,
        string folderId,
        string newLeafName)
    {
        var target = folders.FirstOrDefault(f => f.Id == folderId);
        if (target is null) return FolderPlan.Invalid("That folder no longer exists.");

        var leaf = Normalise(newLeafName);
        if (leaf.Length == 0) return FolderPlan.Invalid("A folder needs a name.");
        if (leaf.Contains(Separator))
            return FolderPlan.Invalid(
                $"A name cannot contain '{Separator}'. Move the folder instead to change where it sits.");

        var newPath = Join(Parent(target.Name), leaf);
        return PlanRebase(folders, target, newPath);
    }

    /// <summary>
    /// Plans moving a folder under a new parent, or to the root when
    /// <paramref name="newParentPath"/> is null.
    /// </summary>
    public static FolderPlan PlanMove(
        IReadOnlyList<VaultFolder> folders,
        string folderId,
        string? newParentPath)
    {
        var target = folders.FirstOrDefault(f => f.Id == folderId);
        if (target is null) return FolderPlan.Invalid("That folder no longer exists.");

        var parent = newParentPath is null ? null : Normalise(newParentPath);

        // Moving a folder inside itself would orphan the whole subtree.
        if (parent is not null
            && (string.Equals(parent, target.Name, StringComparison.OrdinalIgnoreCase)
                || IsDescendantOf(parent, target.Name)))
            return FolderPlan.Invalid("A folder cannot be moved inside itself.");

        var newPath = Join(parent, Leaf(target.Name));
        return PlanRebase(folders, target, newPath);
    }

    private static FolderPlan PlanRebase(
        IReadOnlyList<VaultFolder> folders,
        VaultFolder target,
        string newPath)
    {
        if (string.Equals(newPath, target.Name, StringComparison.Ordinal)) return FolderPlan.Of([]);

        var clash = folders.FirstOrDefault(f =>
            f.Id != target.Id && string.Equals(f.Name, newPath, StringComparison.OrdinalIgnoreCase));
        if (clash is not null) return FolderPlan.Invalid($"A folder called \"{newPath}\" already exists.");

        var renames = new List<FolderRename> { new(target.Id, target.Name, newPath) };

        // Descendants are independent folders whose names merely start with the old path. They
        // are renamed deepest-first so no intermediate state has two folders sharing a name.
        renames.AddRange(folders
            .Where(f => IsDescendantOf(f.Name, target.Name))
            .OrderByDescending(f => Segments(f.Name).Count)
            .Select(f => new FolderRename(f.Id, f.Name, Rebase(f.Name, target.Name, newPath))));

        return FolderPlan.Of(renames);
    }

    /// <summary>Validates a name for a new folder at <paramref name="parentPath"/>.</summary>
    public static FolderPlan PlanCreate(
        IReadOnlyList<VaultFolder> folders,
        string? parentPath,
        string leafName)
    {
        var leaf = Normalise(leafName);
        if (leaf.Length == 0) return FolderPlan.Invalid("A folder needs a name.");

        var path = Join(parentPath is null ? null : Normalise(parentPath), leaf);
        if (folders.Any(f => string.Equals(f.Name, path, StringComparison.OrdinalIgnoreCase)))
            return FolderPlan.Invalid($"A folder called \"{path}\" already exists.");

        // Carries the intended full name as a rename with no id for the caller to create.
        return FolderPlan.Of([new FolderRename(string.Empty, string.Empty, path)]);
    }
}
