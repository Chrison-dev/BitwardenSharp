using System.Text.Json;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Domain.Vault;
using BitwardenSharp.Infrastructure.Bw.Contracts;

namespace BitwardenSharp.Infrastructure.Bw;

/// <summary>
/// A read-only <see cref="IVaultClient"/> over a saved <c>bw list items</c> dump.
/// </summary>
/// <remarks>
/// <para>
/// Useful for auditing an export without unlocking a vault, for reproducing a scan against a
/// fixed snapshot, and for driving the scanner from a test fixture.
/// </para>
/// <para>
/// Every mutating member throws. A file-backed vault that silently accepted writes would be a
/// trap: a merge would report success having changed nothing.
/// </para>
/// </remarks>
public sealed class JsonFileVaultClient(string itemsPath, string? foldersPath = null) : IVaultClient
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Task SyncAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<VaultStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new VaultStatus
        {
            Status = "unlocked",
            ServerUrl = $"file://{itemsPath}",
        });

    public async Task<IReadOnlyList<VaultItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(itemsPath);
        var items = await JsonSerializer.DeserializeAsync<List<BwItem>>(stream, Json, cancellationToken) ?? [];
        return items.Select(BwItemMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<VaultFolder>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        if (foldersPath is null || !File.Exists(foldersPath)) return [];
        await using var stream = File.OpenRead(foldersPath);
        var folders = await JsonSerializer.DeserializeAsync<List<BwFolder>>(stream, Json, cancellationToken) ?? [];
        return folders.Select(BwItemMapper.ToDomain).ToList();
    }

    public async Task<VaultItem> GetItemAsync(string id, CancellationToken cancellationToken = default) =>
        (await GetItemsAsync(cancellationToken)).FirstOrDefault(i => i.Id == id)
        ?? throw new KeyNotFoundException($"no item {id} in {itemsPath}");

    public Task<VaultItem> UpdateItemAsync(VaultItem item, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("this vault is a read-only file snapshot; merges need a live vault");

    public Task DeleteItemAsync(string id, bool permanent = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("this vault is a read-only file snapshot; merges need a live vault");
}
