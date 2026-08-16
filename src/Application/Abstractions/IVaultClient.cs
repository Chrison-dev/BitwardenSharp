using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Application.Abstractions;

/// <summary>
/// The port onto a Bitwarden vault. Application code depends on this and never on a transport.
/// </summary>
/// <remarks>
/// There is no public Bitwarden API for personal vault items — the documented api.bitwarden.com
/// surface is organisation-scoped only. Every implementation therefore goes through the official
/// <c>bw</c> client in some form. Reimplementing Bitwarden's client-side crypto against the
/// internal endpoints was considered and rejected: it means owning Argon2id/PBKDF2 derivation and
/// AES-CBC-HMAC EncString handling against an unsupported, changeable API, in front of a password
/// vault.
/// </remarks>
public interface IVaultClient
{
    /// <summary>Pulls the latest vault state from the server.</summary>
    Task SyncAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the vault is currently unlocked and usable.</summary>
    Task<VaultStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VaultItem>> GetItemsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VaultFolder>> GetFoldersAsync(CancellationToken cancellationToken = default);

    Task<VaultItem> GetItemAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an item wholesale and returns what the vault stored. The Bitwarden CLI has no
    /// partial update: the object sent is the object kept, so callers must pass a complete item.
    /// </summary>
    Task<VaultItem> UpdateItemAsync(VaultItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an item. Soft by default, which moves it to the trash and keeps it restorable for
    /// 30 days — the only undo a merge has.
    /// </summary>
    Task DeleteItemAsync(string id, bool permanent = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an item and returns it as stored, with the id the vault assigned.
    /// </summary>
    /// <remarks>
    /// Used when a merge is resolved into a brand-new item rather than into one of its sources.
    /// Note that attachments cannot be carried onto a created item — the CLI has no way to move
    /// one — so a group holding an attachment cannot be merged this way at all.
    /// </remarks>
    Task<VaultItem> CreateItemAsync(VaultItem item, CancellationToken cancellationToken = default);

    /// <summary>Creates a folder. The name is the full path, e.g. "Homelab/Proxmox".</summary>
    Task<VaultFolder> CreateFolderAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a folder to the given full path.
    /// </summary>
    /// <remarks>
    /// This renames one folder only. Because Bitwarden stores folders flat, a tree operation is
    /// several of these — plan it with <see cref="Domain.Vault.FolderPaths"/> rather than calling
    /// this directly, or descendants get left behind under the old name.
    /// </remarks>
    Task<VaultFolder> RenameFolderAsync(
        string id, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a folder. Items inside it are not deleted; Bitwarden unfiles them.
    /// </summary>
    Task DeleteFolderAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>Lock state and identity of the vault behind an <see cref="IVaultClient"/>.</summary>
public sealed record VaultStatus
{
    public required string Status { get; init; }

    public string? UserEmail { get; init; }

    public string? ServerUrl { get; init; }

    public DateTimeOffset? LastSync { get; init; }

    public bool IsUnlocked => string.Equals(Status, "unlocked", StringComparison.OrdinalIgnoreCase);
}
