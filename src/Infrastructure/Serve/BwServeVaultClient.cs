using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Domain.Vault;
using BitwardenSharp.Infrastructure.Bw;
using BitwardenSharp.Infrastructure.Bw.Contracts;
using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Infrastructure.Serve;

/// <summary>Every Vault Management API response is wrapped in this envelope.</summary>
internal sealed class ServeEnvelope<T>
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

/// <summary>A list response nests its payload one level deeper.</summary>
internal sealed class ServeList<T>
{
    [JsonPropertyName("object")] public string? Object { get; set; }
    [JsonPropertyName("data")] public List<T> Data { get; set; } = [];
}

/// <summary>
/// <c>/status</c> alone nests its payload one level further, under a "template" object, rather
/// than placing it directly in <c>data</c> like every other endpoint. Verified against
/// bw 2026.7.0 — do not assume the envelope is uniform.
/// </summary>
internal sealed class ServeTemplate<T>
{
    [JsonPropertyName("object")] public string? Object { get; set; }
    [JsonPropertyName("template")] public T? Template { get; set; }
}

internal sealed class ServeMessage
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("raw")] public string? Raw { get; set; }
}

/// <summary>Raised when the Vault Management API reports a failure.</summary>
public sealed class BwServeException(string message) : Exception(message);

/// <summary>
/// An <see cref="IVaultClient"/> and <see cref="IVaultSession"/> over the local Vault Management
/// API exposed by <c>bw serve</c>.
/// </summary>
/// <remarks>
/// <para>
/// Preferred over the process-per-call adapter wherever the host outlives a single command: one
/// child process, HTTP for everything, no ~0.5s Node start-up per operation.
/// </para>
/// <para>
/// It also removes the awkward part of the CLI adapter entirely. There is no base64 payload and
/// no <c>argv</c> to keep secrets out of — an item is a JSON request body and the master password
/// is a field in one, over loopback. See <see cref="BwServeProcess"/> for what that costs.
/// </para>
/// </remarks>
public sealed class BwServeVaultClient(
    BwServeConnection connection,
    ILogger<BwServeVaultClient>? logger = null) : IVaultClient, IVaultSession
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── IVaultSession ────────────────────────────────────────────────────────────────────────

    public async Task<VaultStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var wrapper = await GetAsync<ServeTemplate<BwStatus>>("status", cancellationToken);
        var status = wrapper.Template
                     ?? throw new BwServeException("status response carried no template");
        return new VaultStatus
        {
            Status = status.Status,
            UserEmail = status.UserEmail,
            ServerUrl = status.ServerUrl,
            LastSync = status.LastSync,
        };
    }

    public async Task<UnlockResult> UnlockAsync(
        string masterPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(masterPassword)) return UnlockResult.Failure("Enter your master password.");

        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.PostAsJsonAsync(
            "unlock", new { password = masterPassword }, Json, cancellationToken);

        var envelope = await response.Content
            .ReadFromJsonAsync<ServeEnvelope<ServeMessage>>(Json, cancellationToken);

        if (envelope?.Success != true)
        {
            var error = envelope?.Message ?? $"unlock failed ({(int)response.StatusCode})";
            logger?.LogWarning("Unlock rejected: {Error}", error);
            return UnlockResult.Failure(error);
        }

        logger?.LogInformation("Vault unlocked");
        return UnlockResult.Success();
    }

    public async Task LockAsync(CancellationToken cancellationToken = default)
    {
        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.PostAsync("lock", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        logger?.LogInformation("Vault locked");
    }

    // ── IVaultClient ─────────────────────────────────────────────────────────────────────────

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.PostAsync("sync", content: null, cancellationToken);
        await EnsureSucceededAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<VaultItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var list = await GetAsync<ServeList<BwItem>>("list/object/items", cancellationToken);
        logger?.LogDebug("Read {Count} items from the vault", list.Data.Count);
        return list.Data.Select(BwItemMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<VaultFolder>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var list = await GetAsync<ServeList<BwFolder>>("list/object/folders", cancellationToken);
        return list.Data.Select(BwItemMapper.ToDomain).ToList();
    }

    public async Task<VaultItem> GetItemAsync(string id, CancellationToken cancellationToken = default) =>
        BwItemMapper.ToDomain(await GetWireItemAsync(id, cancellationToken));

    public async Task<VaultItem> UpdateItemAsync(
        VaultItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        // As with the CLI adapter, a PUT replaces the item wholesale, so the wire object is
        // re-read and the domain changes applied onto it — otherwise fields this model does not
        // name would be deleted from the vault.
        var wire = BwItemMapper.ApplyTo(item, await GetWireItemAsync(item.Id, cancellationToken));

        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.PutAsJsonAsync($"object/item/{item.Id}", wire, Json, cancellationToken);
        var envelope = await ReadEnvelopeAsync<BwItem>(response, cancellationToken);
        return BwItemMapper.ToDomain(envelope);
    }

    public async Task DeleteItemAsync(
        string id,
        bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        // The API soft-deletes to trash by default, matching the CLI.
        var path = permanent ? $"object/item/{id}?permanent=true" : $"object/item/{id}";
        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.DeleteAsync(path, cancellationToken);
        await EnsureSucceededAsync(response, cancellationToken);
        logger?.LogInformation(
            "Deleted item {ItemId} ({Disposition})", id, permanent ? "permanently" : "to trash");
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────

    public async Task<VaultItem> CreateItemAsync(
        VaultItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var wire = BwItemMapper.ApplyTo(item, BwItemMapper.NewWireItem(item));
        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.PostAsJsonAsync("object/item", wire, Json, cancellationToken);
        return BwItemMapper.ToDomain(await ReadEnvelopeAsync<BwItem>(response, cancellationToken));
    }

    public async Task<VaultFolder> CreateFolderAsync(
        string name, CancellationToken cancellationToken = default)
    {
        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.PostAsJsonAsync(
            "object/folder", new BwFolder { Name = name }, Json, cancellationToken);
        return BwItemMapper.ToDomain(await ReadEnvelopeAsync<BwFolder>(response, cancellationToken));
    }

    public async Task<VaultFolder> RenameFolderAsync(
        string id, string name, CancellationToken cancellationToken = default)
    {
        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.PutAsJsonAsync(
            $"object/folder/{id}", new BwFolder { Id = id, Name = name }, Json, cancellationToken);
        return BwItemMapper.ToDomain(await ReadEnvelopeAsync<BwFolder>(response, cancellationToken));
    }

    public async Task DeleteFolderAsync(string id, CancellationToken cancellationToken = default)
    {
        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.DeleteAsync($"object/folder/{id}", cancellationToken);
        await EnsureSucceededAsync(response, cancellationToken);
        logger?.LogInformation("Deleted folder {FolderId}", id);
    }

    private async Task<BwItem> GetWireItemAsync(string id, CancellationToken cancellationToken) =>
        await GetAsync<BwItem>($"object/item/{id}", cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var http = await connection.GetClientAsync(cancellationToken);
        using var response = await http.GetAsync(path, cancellationToken);
        return await ReadEnvelopeAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadEnvelopeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var envelope = await response.Content
            .ReadFromJsonAsync<ServeEnvelope<T>>(Json, cancellationToken);

        if (envelope is null)
            throw new BwServeException($"empty response from {response.RequestMessage?.RequestUri}");

        if (!envelope.Success || envelope.Data is null)
            throw new BwServeException(envelope.Message ?? $"request failed ({(int)response.StatusCode})");

        return envelope.Data;
    }

    private static async Task EnsureSucceededAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var envelope = await response.Content
            .ReadFromJsonAsync<ServeEnvelope<JsonElement>>(Json, cancellationToken);

        if (envelope?.Success != true)
            throw new BwServeException(envelope?.Message ?? $"request failed ({(int)response.StatusCode})");
    }
}
