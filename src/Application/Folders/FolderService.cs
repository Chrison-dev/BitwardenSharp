using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Domain.Vault;
using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Application.Folders;

/// <summary>The result of a folder operation, in terms the UI can show directly.</summary>
public sealed record FolderOperationResult
{
    public required bool Succeeded { get; init; }
    public string? Error { get; init; }

    /// <summary>How many folders were renamed. A subtree move touches more than one.</summary>
    public int FoldersChanged { get; init; }

    /// <summary>How many items were moved between folders.</summary>
    public int ItemsChanged { get; init; }

    public static FolderOperationResult Failure(string error) => new() { Succeeded = false, Error = error };
}

/// <summary>
/// File-explorer operations over Bitwarden's flat folder list.
/// </summary>
/// <remarks>
/// Every method here plans the whole operation with <see cref="FolderPaths"/> before writing
/// anything, so an invalid gesture — a name collision, a folder dropped into its own subtree —
/// is refused with the vault untouched rather than half-applied.
/// </remarks>
public sealed class FolderService(IVaultClient vault, ILogger<FolderService>? logger = null)
{
    public async Task<FolderOperationResult> CreateAsync(
        string? parentPath,
        string leafName,
        CancellationToken cancellationToken = default)
    {
        var folders = await vault.GetFoldersAsync(cancellationToken);
        var plan = FolderPaths.PlanCreate(folders, parentPath, leafName);
        if (!plan.IsValid) return FolderOperationResult.Failure(plan.Error!.Message);

        var name = plan.Renames[0].NewName;
        await vault.CreateFolderAsync(name, cancellationToken);
        logger?.LogInformation("Created folder {Name}", name);

        return new FolderOperationResult { Succeeded = true, FoldersChanged = 1 };
    }

    /// <summary>Renames a folder's own segment, carrying its whole subtree.</summary>
    public async Task<FolderOperationResult> RenameAsync(
        string folderId,
        string newLeafName,
        CancellationToken cancellationToken = default)
    {
        var folders = await vault.GetFoldersAsync(cancellationToken);
        return await ApplyAsync(FolderPaths.PlanRename(folders, folderId, newLeafName), cancellationToken);
    }

    /// <summary>Moves a folder under a new parent, or to the root when the path is null.</summary>
    public async Task<FolderOperationResult> MoveAsync(
        string folderId,
        string? newParentPath,
        CancellationToken cancellationToken = default)
    {
        var folders = await vault.GetFoldersAsync(cancellationToken);
        return await ApplyAsync(FolderPaths.PlanMove(folders, folderId, newParentPath), cancellationToken);
    }

    /// <summary>
    /// Deletes a folder.
    /// </summary>
    /// <remarks>
    /// Bitwarden unfiles the items inside rather than deleting them, which is the behaviour we
    /// want. Descendant folders are separate records and are <b>not</b> removed automatically —
    /// deleting "Homelab" would strand "Homelab/Proxmox" as a root-level folder with a slash in
    /// its name. <paramref name="includeDescendants"/> deletes the subtree instead, deepest first.
    /// </remarks>
    public async Task<FolderOperationResult> DeleteAsync(
        string folderId,
        bool includeDescendants = true,
        CancellationToken cancellationToken = default)
    {
        var folders = await vault.GetFoldersAsync(cancellationToken);
        var target = folders.FirstOrDefault(f => f.Id == folderId);
        if (target is null) return FolderOperationResult.Failure("That folder no longer exists.");

        var doomed = new List<VaultFolder> { target };
        if (includeDescendants)
            doomed.AddRange(folders.Where(f => FolderPaths.IsDescendantOf(f.Name, target.Name)));

        // Deepest first, so the tree never passes through a state with a parentless child.
        foreach (var folder in doomed.OrderByDescending(f => FolderPaths.Segments(f.Name).Count))
        {
            await vault.DeleteFolderAsync(folder.Id, cancellationToken);
            logger?.LogInformation("Deleted folder {Name}", folder.Name);
        }

        return new FolderOperationResult { Succeeded = true, FoldersChanged = doomed.Count };
    }

    /// <summary>Moves items into a folder, or out of any folder when the id is null.</summary>
    public async Task<FolderOperationResult> MoveItemsAsync(
        IReadOnlyList<string> itemIds,
        string? targetFolderId,
        CancellationToken cancellationToken = default)
    {
        var moved = 0;
        foreach (var id in itemIds)
        {
            var item = await vault.GetItemAsync(id, cancellationToken);
            if (item.FolderId == targetFolderId) continue;

            await vault.UpdateItemAsync(item with { FolderId = targetFolderId }, cancellationToken);
            moved++;
        }

        logger?.LogInformation("Moved {Count} item(s) to folder {FolderId}", moved, targetFolderId ?? "<none>");
        return new FolderOperationResult { Succeeded = true, ItemsChanged = moved };
    }

    private async Task<FolderOperationResult> ApplyAsync(
        FolderPlan plan,
        CancellationToken cancellationToken)
    {
        if (!plan.IsValid) return FolderOperationResult.Failure(plan.Error!.Message);
        if (plan.Renames.Count == 0) return new FolderOperationResult { Succeeded = true };

        // The plan orders descendants deepest-first; applying in that order means no two folders
        // ever momentarily share a name.
        foreach (var rename in plan.Renames)
        {
            await vault.RenameFolderAsync(rename.FolderId, rename.NewName, cancellationToken);
            logger?.LogInformation("Renamed folder {Old} -> {New}", rename.OldName, rename.NewName);
        }

        return new FolderOperationResult { Succeeded = true, FoldersChanged = plan.Renames.Count };
    }
}
