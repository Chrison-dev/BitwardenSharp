using System.Net;

namespace BitwardenSharp.Domain.Uris;

/// <summary>What a stored URI actually points at.</summary>
public enum UriTargetKind
{
    /// <summary>A registrable internet domain, e.g. <c>digikey.co.nz</c>.</summary>
    Domain,

    /// <summary>A literal IP address. Almost always a homelab box.</summary>
    IpAddress,

    /// <summary>A dotless hostname — <c>localhost</c>, <c>synology</c>, an mDNS name.</summary>
    Host,

    /// <summary>A native app identifier from an <c>androidapp://</c> or <c>iosapp://</c> URI.</summary>
    App,
}

/// <summary>
/// A stored URI reduced to the thing worth comparing: its registrable domain, IP, bare host or
/// app id. Two logins are candidates for the same account only if their targets agree.
/// </summary>
public sealed record UriTarget(UriTargetKind Kind, string Value)
{
    private static readonly string[] Schemes =
    [
        "http://", "https://", "ftp://", "ssh://", "sftp://", "androidapp://", "android://",
        "iosapp://", "otpauth://", "chrome://", "moz-extension://", "file://",
    ];

    /// <summary>
    /// Reduces a raw stored URI to its comparable target, or null when there is nothing to compare.
    /// </summary>
    public static UriTarget? Parse(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return null;

        var value = uri.Trim();

        // Native app identifiers are their own namespace: "com.google.android.gm" is not a domain
        // and must never be folded together with google.com by the domain rules below.
        foreach (var appScheme in (string[])["androidapp://", "android://", "iosapp://"])
        {
            if (value.StartsWith(appScheme, StringComparison.OrdinalIgnoreCase))
            {
                var app = value[appScheme.Length..].Trim('/');
                return app.Length == 0 ? null : new UriTarget(UriTargetKind.App, app.ToLowerInvariant());
            }
        }

        foreach (var scheme in Schemes)
        {
            if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                value = value[scheme.Length..];
                break;
            }
        }

        // Strip path, query and fragment.
        value = value.Split('/')[0].Split('?')[0].Split('#')[0];

        // Strip any userinfo, then the port.
        var at = value.LastIndexOf('@');
        if (at >= 0) value = value[(at + 1)..];
        value = value.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();

        if (value.Length == 0) return null;

        if (IPAddress.TryParse(value, out _)) return new UriTarget(UriTargetKind.IpAddress, value);

        if (!value.Contains('.')) return new UriTarget(UriTargetKind.Host, value);

        var labels = value.Split('.');
        if (labels.Length >= 3 && PublicSuffix.IsMultiLabelSuffix($"{labels[^2]}.{labels[^1]}"))
            return new UriTarget(UriTargetKind.Domain, string.Join('.', labels[^3..]));

        return new UriTarget(UriTargetKind.Domain, string.Join('.', labels[^2..]));
    }

    /// <summary>
    /// The leading label of a registrable domain — <c>digikey.co.nz</c> gives <c>digikey</c>.
    /// Null for anything that is not a domain, so IPs never compare as brands.
    /// </summary>
    public string? Brand => Kind == UriTargetKind.Domain ? Value.Split('.')[0] : null;

    public override string ToString() => $"{Value} ({Kind})";
}
