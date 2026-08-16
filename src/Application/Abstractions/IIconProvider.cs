namespace BitwardenSharp.Application.Abstractions;

/// <summary>
/// Supplies website icons for vault items.
/// </summary>
/// <remarks>
/// <para>
/// Bitwarden does not store an icon on an item. Its clients derive one from the item's first URI
/// and fetch it from a hosted icon service, which is why an item with no URI never has an icon in
/// any Bitwarden client either.
/// </para>
/// <para>
/// <b>This leaks information.</b> Asking the icon service for <c>git.internal.example/icon.png</c>
/// tells that service the domain is in someone's vault, and doing it for every item hands over a
/// list of the sites you hold accounts with. Implementations must therefore be switchable off,
/// must request the registrable domain only, and should cache aggressively so a domain is asked
/// for once rather than on every render.
/// </para>
/// </remarks>
public interface IIconProvider
{
    /// <summary>Whether icons are being fetched at all.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// The icon bytes for a domain, or null when there is none, when fetching is disabled, or
    /// when the lookup failed. Callers show a placeholder for null rather than treating it as an
    /// error — a missing icon is the normal case for anything self-hosted.
    /// </summary>
    Task<byte[]?> GetIconAsync(string domain, CancellationToken cancellationToken = default);
}
