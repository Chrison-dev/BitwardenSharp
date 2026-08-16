using System.Security.Cryptography;
using System.Text;

namespace BitwardenSharp.Domain.Vault;

/// <summary>The credential half of a <see cref="ItemType.Login"/> item.</summary>
public sealed record LoginDetails
{
    public string? Username { get; init; }

    public string? Password { get; init; }

    /// <summary>TOTP seed or otpauth:// URI. Treated as a secret.</summary>
    public string? Totp { get; init; }

    public IReadOnlyList<LoginUri> Uris { get; init; } = [];

    public DateTimeOffset? PasswordRevisionDate { get; init; }

    /// <summary>
    /// Username lowered and trimmed, for comparison. Bitwarden preserves whatever case the user
    /// typed, but "Chrison" and "chrison" on the same site are the same account.
    /// </summary>
    public string? NormalisedUsername =>
        string.IsNullOrWhiteSpace(Username) ? null : Username.Trim().ToLowerInvariant();

    /// <summary>
    /// A stable, non-reversible fingerprint of the password, for equality comparison and for
    /// display in reports. Two items share a password if and only if these match.
    /// </summary>
    /// <remarks>
    /// SHA-256 truncated to 12 hex characters. This exists so duplicate analysis can say "same
    /// password" in a report a human reads, without that report becoming a plaintext password
    /// dump. It is a comparison aid, never a credential — anything storing or transmitting the
    /// actual secret must use <see cref="Password"/> directly and treat it accordingly.
    /// </remarks>
    public string? PasswordFingerprint
    {
        get
        {
            if (string.IsNullOrEmpty(Password)) return null;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Password));
            return Convert.ToHexStringLower(hash)[..12];
        }
    }

    /// <summary>
    /// Redacts every secret. The compiler-generated record <c>ToString</c> would print
    /// <see cref="Password"/> and <see cref="Totp"/> in full, so any log line, exception message
    /// or debugger string that touched a login would leak it.
    /// </summary>
    public override string ToString() =>
        $"LoginDetails {{ Username = {Username}, Password = {(Password is null ? "<null>" : "<redacted>")}, "
        + $"Totp = {(Totp is null ? "<null>" : "<redacted>")}, Uris = {Uris.Count} }}";
}
