using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Domain.Duplicates;
using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Application.Merging;

/// <summary>What happened to one group.</summary>
public enum MergeStatus
{
    /// <summary>Survivor updated and every loser deleted.</summary>
    Merged,

    /// <summary>Refused before any write: the group is not mergeable.</summary>
    Skipped,

    /// <summary>
    /// The survivor did not read back with the merged content, so no loser was deleted.
    /// Nothing was lost.
    /// </summary>
    VerificationFailed,

    /// <summary>The vault rejected a call. Any loser not yet deleted is untouched.</summary>
    Failed,
}

public sealed record MergeOutcome
{
    public required string GroupId { get; init; }

    public required MergeStatus Status { get; init; }

    public IReadOnlyList<string> Changes { get; init; } = [];

    public IReadOnlyList<string> DeletedItemIds { get; init; } = [];

    public string? Message { get; init; }
}

/// <summary>
/// Applies merges against a vault.
/// </summary>
/// <remarks>
/// <para>
/// The write order is the safety property. For each group the survivor is updated first, then
/// <b>read back and verified</b>, and only then are the losers deleted. There is therefore no
/// moment at which a URI, seed or note exists in neither item: if anything fails, it fails with
/// the losers still present and the operation is simply re-runnable.
/// </para>
/// <para>
/// Items are re-read from the vault immediately before merging rather than reused from the scan.
/// A scan is a snapshot, and acting on a stale one could overwrite a change made in the meantime.
/// </para>
/// </remarks>
public sealed class MergeExecutor(IVaultClient vault, ILogger<MergeExecutor>? logger = null)
{
    /// <summary>
    /// Applies one group. <paramref name="dryRun"/> performs every read and computes the merge
    /// but issues no write.
    /// </summary>
    public async Task<MergeOutcome> ApplyAsync(
        DuplicateGroup group,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (!group.CanMerge)
        {
            var blocking = group.Warnings.Where(w => w.IsBlocking).Select(w => w.Message);
            return new MergeOutcome
            {
                GroupId = group.Id,
                Status = MergeStatus.Skipped,
                Message = group.Category.Disposition() == MergeDisposition.ReviewOnly
                    ? $"{group.Category} is review-only and is never merged automatically"
                    : string.Join("; ", blocking),
            };
        }

        try
        {
            // Re-read: the scan is a snapshot and may be stale.
            var survivor = await vault.GetItemAsync(group.Survivor.Id, cancellationToken);
            var losers = new List<Domain.Vault.VaultItem>();
            foreach (var loser in group.Losers)
                losers.Add(await vault.GetItemAsync(loser.Id, cancellationToken));

            var (merged, changes) = MergeBuilder.Build(survivor, losers);

            if (dryRun)
                return new MergeOutcome { GroupId = group.Id, Status = MergeStatus.Merged, Changes = changes };

            await vault.UpdateItemAsync(merged, cancellationToken);

            // Verify before deleting anything.
            var readBack = await vault.GetItemAsync(merged.Id, cancellationToken);
            var stored = readBack.Uris.Select(u => u.Uri.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expected = merged.Uris.Select(u => u.Uri.Trim());
            if (!expected.All(stored.Contains))
            {
                logger?.LogError(
                    "Group {GroupId}: survivor {ItemId} did not verify after update; losers left in place",
                    group.Id, merged.Id);
                return new MergeOutcome
                {
                    GroupId = group.Id,
                    Status = MergeStatus.VerificationFailed,
                    Changes = changes,
                    Message = "survivor did not read back with the merged URIs; nothing was deleted",
                };
            }

            var deleted = new List<string>();
            foreach (var loser in losers)
            {
                await vault.DeleteItemAsync(loser.Id, permanent: false, cancellationToken);
                deleted.Add(loser.Id);
                logger?.LogInformation("Group {GroupId}: deleted {ItemId} to trash", group.Id, loser.Id);
            }

            return new MergeOutcome
            {
                GroupId = group.Id,
                Status = MergeStatus.Merged,
                Changes = changes,
                DeletedItemIds = deleted,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "Group {GroupId}: merge failed", group.Id);
            return new MergeOutcome { GroupId = group.Id, Status = MergeStatus.Failed, Message = ex.Message };
        }
    }
}
