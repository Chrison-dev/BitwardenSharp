using System.Text;
using System.Text.Json;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Domain.Vault;
using BitwardenSharp.Infrastructure.Bw.Contracts;
using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Infrastructure.Bw;

/// <summary>
/// An <see cref="IVaultClient"/> backed by the official <c>bw</c> command-line client.
/// </summary>
/// <remarks>
/// Every call spawns a process, which costs roughly half a second of Node start-up. That is
/// irrelevant for the bulk read (one <c>bw list items</c> returns the whole vault) and acceptable
/// for merges, which are a handful of calls each and are gated on human approval anyway.
/// </remarks>
public sealed class BwCliVaultClient(
    BwProcessRunner runner,
    ILogger<BwCliVaultClient>? logger = null) : IVaultClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task SyncAsync(CancellationToken cancellationToken = default) =>
        await runner.RunAsync(["sync"], cancellationToken: cancellationToken);

    public async Task<VaultStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var json = await runner.RunAsync(["status"], cancellationToken: cancellationToken);
        var status = JsonSerializer.Deserialize<BwStatus>(json, Json)
                     ?? throw new InvalidOperationException("bw status returned nothing");
        return new VaultStatus
        {
            Status = status.Status,
            UserEmail = status.UserEmail,
            ServerUrl = status.ServerUrl,
            LastSync = status.LastSync,
        };
    }

    public async Task<IReadOnlyList<VaultItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var json = await runner.RunAsync(["list", "items"], cancellationToken: cancellationToken);
        var items = JsonSerializer.Deserialize<List<BwItem>>(json, Json) ?? [];
        logger?.LogDebug("Read {Count} items from the vault", items.Count);
        return items.Select(BwItemMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<VaultFolder>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var json = await runner.RunAsync(["list", "folders"], cancellationToken: cancellationToken);
        var folders = JsonSerializer.Deserialize<List<BwFolder>>(json, Json) ?? [];
        return folders.Select(BwItemMapper.ToDomain).ToList();
    }

    public async Task<VaultItem> GetItemAsync(string id, CancellationToken cancellationToken = default) =>
        BwItemMapper.ToDomain(await GetWireItemAsync(id, cancellationToken));

    public async Task<VaultItem> UpdateItemAsync(VaultItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Re-read the wire object and apply onto it: `bw edit` replaces the item wholesale, so
        // anything absent from the payload is deleted from the vault. See BwItemMapper.ApplyTo.
        var wire = BwItemMapper.ApplyTo(item, await GetWireItemAsync(item.Id, cancellationToken));
        var payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(wire, Json));

        // Piped, not passed. The payload contains the password in clear, and process arguments
        // are readable by any local user through `ps`.
        var json = await runner.RunAsync(
            ["edit", "item", item.Id],
            standardInput: payload,
            cancellationToken: cancellationToken);

        var updated = JsonSerializer.Deserialize<BwItem>(json, Json)
                      ?? throw new InvalidOperationException($"bw edit item {item.Id} returned nothing");
        return BwItemMapper.ToDomain(updated);
    }

    public async Task DeleteItemAsync(
        string id,
        bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        string[] args = permanent ? ["delete", "item", id, "--permanent"] : ["delete", "item", id];
        await runner.RunAsync(args, cancellationToken: cancellationToken);
        logger?.LogInformation(
            "Deleted item {ItemId} ({Disposition})", id, permanent ? "permanently" : "to trash");
    }

    public async Task<VaultFolder> CreateFolderAsync(
        string name, CancellationToken cancellationToken = default)
    {
        var payload = Convert.ToBase64String(
            JsonSerializer.SerializeToUtf8Bytes(new BwFolder { Name = name }, Json));
        var json = await runner.RunAsync(
            ["create", "folder"], standardInput: payload, cancellationToken: cancellationToken);
        return BwItemMapper.ToDomain(
            JsonSerializer.Deserialize<BwFolder>(json, Json)
            ?? throw new InvalidOperationException("bw create folder returned nothing"));
    }

    public async Task<VaultFolder> RenameFolderAsync(
        string id, string name, CancellationToken cancellationToken = default)
    {
        var payload = Convert.ToBase64String(
            JsonSerializer.SerializeToUtf8Bytes(new BwFolder { Id = id, Name = name }, Json));
        var json = await runner.RunAsync(
            ["edit", "folder", id], standardInput: payload, cancellationToken: cancellationToken);
        return BwItemMapper.ToDomain(
            JsonSerializer.Deserialize<BwFolder>(json, Json)
            ?? throw new InvalidOperationException($"bw edit folder {id} returned nothing"));
    }

    public async Task DeleteFolderAsync(string id, CancellationToken cancellationToken = default)
    {
        await runner.RunAsync(["delete", "folder", id], cancellationToken: cancellationToken);
        logger?.LogInformation("Deleted folder {FolderId}", id);
    }

    private async Task<BwItem> GetWireItemAsync(string id, CancellationToken cancellationToken)
    {
        var json = await runner.RunAsync(["get", "item", id], cancellationToken: cancellationToken);
        return JsonSerializer.Deserialize<BwItem>(json, Json)
               ?? throw new InvalidOperationException($"bw get item {id} returned nothing");
    }
}
