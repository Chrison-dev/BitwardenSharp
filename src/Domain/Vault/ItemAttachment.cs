namespace BitwardenSharp.Domain.Vault;

/// <summary>
/// A file attached to an item. Present for detection only: the Bitwarden CLI offers no way to
/// move an attachment between items, so any merge involving one has to be refused rather than
/// silently dropping the file.
/// </summary>
public sealed record ItemAttachment
{
    public required string Id { get; init; }

    public string? FileName { get; init; }

    public long? Size { get; init; }
}
