using Avalonia.Media.Imaging;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Domain.Uris;
using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Desktop.Services;

/// <summary>
/// Turns a vault item into a website icon, decoded once per domain.
/// </summary>
/// <remarks>
/// Many items share a domain — a vault with 87 duplicate groups has a lot of repeats — so the
/// decoded <see cref="Bitmap"/> is cached and shared rather than decoded per item. Bitmaps are
/// never disposed while in use here; they live for the life of the app, which for 24×24 favicons
/// is a few hundred kilobytes at most.
/// </remarks>
public sealed class IconLoader(IIconProvider icons)
{
    private readonly Dictionary<string, Bitmap?> _decoded = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsEnabled => icons.IsEnabled;

    /// <summary>
    /// The domain an item's icon is derived from — its first web URI. Null for items with no URI,
    /// which is why a secure note or an SSH key never has one in any Bitwarden client either.
    /// </summary>
    public static string? IconDomainFor(VaultItem item) =>
        item.Uris
            .Select(u => UriTarget.Parse(u.Uri))
            .FirstOrDefault(t => t?.Kind == UriTargetKind.Domain)
            ?.Value;

    public async Task<Bitmap?> GetAsync(string domain, CancellationToken cancellationToken = default)
    {
        lock (_decoded)
        {
            if (_decoded.TryGetValue(domain, out var cached)) return cached;
        }

        var bytes = await icons.GetIconAsync(domain, cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_decoded.TryGetValue(domain, out var raced)) return raced;

            Bitmap? bitmap = null;
            if (bytes is { Length: > 0 })
            {
                try
                {
                    using var stream = new MemoryStream(bytes);
                    bitmap = new Bitmap(stream);
                }
                catch
                {
                    // The service occasionally returns something that is not a decodable image.
                    // A placeholder is a fine outcome; a crash is not.
                    bitmap = null;
                }
            }

            lock (_decoded) _decoded[domain] = bitmap;
            return bitmap;
        }
        finally
        {
            _gate.Release();
        }
    }
}
