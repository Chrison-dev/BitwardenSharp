namespace BitwardenSharp.Domain.Vault;

/// <summary>One URI on a login, with the match rule Bitwarden should use for it.</summary>
public sealed record LoginUri
{
    public required string Uri { get; init; }

    /// <summary>Null means "use the vault-wide default match strategy".</summary>
    public UriMatchType? Match { get; init; }
}
