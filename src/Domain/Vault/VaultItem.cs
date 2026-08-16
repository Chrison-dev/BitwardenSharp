namespace BitwardenSharp.Domain.Vault;

/// <summary>
/// A single vault entry, mirroring the shape the Bitwarden CLI emits from
/// <c>bw list items</c> / <c>bw get item</c>.
/// </summary>
/// <remarks>
/// This is a faithful transport model, not an idealised one: field names and nullability follow
/// what <c>bw</c> actually produces so a round-trip (get → mutate → edit) preserves everything
/// the CLI gave us. Dropping an unrecognised field here would silently delete it from the vault
/// on the next write.
/// </remarks>
public sealed record VaultItem
{
    public required string Id { get; init; }

    public required ItemType Type { get; init; }

    public required string Name { get; init; }

    public string? FolderId { get; init; }

    public string? OrganizationId { get; init; }

    public string? Notes { get; init; }

    public bool Favorite { get; init; }

    public LoginDetails? Login { get; init; }

    public IReadOnlyList<CustomField> Fields { get; init; } = [];

    public IReadOnlyList<ItemAttachment> Attachments { get; init; } = [];

    public DateTimeOffset? RevisionDate { get; init; }

    public DateTimeOffset? CreationDate { get; init; }

    /// <summary>Every URI on this item, or empty when it has none.</summary>
    public IReadOnlyList<LoginUri> Uris => Login?.Uris ?? [];

    /// <summary>
    /// How much irreplaceable data this item carries. Used to choose which member of a duplicate
    /// group survives a merge — the richest one wins, because everything the others hold can be
    /// copied onto it, but an attachment cannot be moved between items by the CLI at all.
    /// </summary>
    public int Richness =>
        Attachments.Count * 10
        + (string.IsNullOrWhiteSpace(Login?.Totp) ? 0 : 5)
        + Fields.Count * 3
        + (string.IsNullOrWhiteSpace(Notes) ? 0 : 2)
        + (string.IsNullOrWhiteSpace(FolderId) ? 0 : 2)
        + (Favorite ? 1 : 0)
        + Uris.Count;

    /// <summary>Deliberately excludes <see cref="Login"/> — see <see cref="LoginDetails"/>.</summary>
    public override string ToString() => $"VaultItem {{ Id = {Id}, Name = {Name}, Type = {Type} }}";
}
