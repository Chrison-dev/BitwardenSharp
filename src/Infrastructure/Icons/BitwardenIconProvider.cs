using System.Security.Cryptography;
using System.Text;
using BitwardenSharp.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Infrastructure.Icons;

/// <summary>How website icons are fetched, if at all.</summary>
public sealed class IconOptions
{
    /// <summary>
    /// Whether to fetch icons at all. Off means every item shows its placeholder and no request
    /// ever leaves the machine.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The icon service. Bitwarden runs one per region; EU accounts should use the EU host so the
    /// lookups stay in the same jurisdiction as the vault.
    /// </summary>
    public Uri ServiceUrl { get; set; } = new("https://icons.bitwarden.eu/");

    /// <summary>Where fetched icons are cached. Null uses the platform's local app-data folder.</summary>
    public string? CacheDirectory { get; set; }

    /// <summary>How long a cached icon (or a cached miss) is trusted.</summary>
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>
/// Fetches website icons from Bitwarden's icon service, cached on disk.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every lookup tells the icon service that a domain is in this vault.</b> Fetching icons for
/// a whole vault hands over a list of the sites its owner holds accounts with — including private
/// ones like <c>git.internal.example</c>, whose mere existence is information. Bitwarden's own
/// clients behave this way and expose it as a setting; so does this, via
/// <see cref="IconOptions.Enabled"/>.
/// </para>
/// <para>
/// Three things limit the exposure. Only the registrable domain is ever sent, never a full URI
/// with its path. Results are cached on disk for a month, so a domain is asked about once rather
/// than on every render. And misses are cached too — a self-hosted host that has no icon is not
/// re-requested every time the app opens.
/// </para>
/// </remarks>
public sealed class BitwardenIconProvider : IIconProvider, IDisposable
{
    private readonly IconOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<BitwardenIconProvider>? _logger;
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _concurrency = new(4, 4);

    /// <summary>Domains already resolved this session, so repeats never touch the disk either.</summary>
    private readonly Dictionary<string, byte[]?> _memory = new(StringComparer.OrdinalIgnoreCase);

    public BitwardenIconProvider(IconOptions options, ILogger<BitwardenIconProvider>? logger = null)
    {
        _options = options;
        _logger = logger;
        _http = new HttpClient { BaseAddress = options.ServiceUrl, Timeout = TimeSpan.FromSeconds(10) };

        _cacheDirectory = options.CacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BitwardenSharp", "icons");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public bool IsEnabled => _options.Enabled;

    public async Task<byte[]?> GetIconAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(domain)) return null;

        var key = domain.Trim().ToLowerInvariant();

        lock (_memory)
        {
            if (_memory.TryGetValue(key, out var remembered)) return remembered;
        }

        var cached = ReadCache(key);
        if (cached is not null)
        {
            var icon = cached.Length == 0 ? null : cached; // zero bytes is a cached miss
            lock (_memory) _memory[key] = icon;
            return icon;
        }

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            var icon = await FetchAsync(key, cancellationToken);
            WriteCache(key, icon ?? []);
            lock (_memory) _memory[key] = icon;
            return icon;
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task<byte[]?> FetchAsync(string domain, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync($"{Uri.EscapeDataString(domain)}/icon.png", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogDebug("No icon for {Domain} ({Status})", domain, (int)response.StatusCode);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return bytes.Length == 0 ? null : bytes;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A missing icon is cosmetic. Never let it surface as an error to the user.
            _logger?.LogDebug(ex, "Icon lookup failed for {Domain}", domain);
            return null;
        }
    }

    /// <summary>
    /// Cache file name. The domain is hashed rather than used directly so the cache directory is
    /// not itself a plainly readable list of the sites in the vault.
    /// </summary>
    private string CachePath(string domain) =>
        Path.Combine(
            _cacheDirectory,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(domain)))[..24] + ".png");

    private byte[]? ReadCache(string domain)
    {
        try
        {
            var path = CachePath(domain);
            if (!File.Exists(path)) return null;
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > _options.CacheLifetime) return null;
            return File.ReadAllBytes(path);
        }
        catch
        {
            return null;
        }
    }

    private void WriteCache(string domain, byte[] bytes)
    {
        try { File.WriteAllBytes(CachePath(domain), bytes); }
        catch (Exception ex) { _logger?.LogDebug(ex, "Could not cache icon for {Domain}", domain); }
    }

    public void Dispose()
    {
        _http.Dispose();
        _concurrency.Dispose();
    }
}
