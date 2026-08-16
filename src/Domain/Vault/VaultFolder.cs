namespace BitwardenSharp.Domain.Vault;

/// <summary>
/// A vault folder. Bitwarden has no real hierarchy — nesting is a naming convention, where
/// "Homelab/Proxmox" is a single folder whose name happens to contain a slash.
/// </summary>
public sealed record VaultFolder
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The path segments implied by the name, e.g. ["Homelab", "Proxmox"].</summary>
    public IReadOnlyList<string> Segments =>
        Name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
