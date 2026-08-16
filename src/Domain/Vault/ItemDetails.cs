namespace BitwardenSharp.Domain.Vault;

/// <summary>Payment card details. Every field here is sensitive.</summary>
public sealed record CardDetails
{
    public string? CardholderName { get; init; }
    public string? Brand { get; init; }
    public string? Number { get; init; }
    public string? ExpMonth { get; init; }
    public string? ExpYear { get; init; }

    /// <summary>CVV/CVC.</summary>
    public string? Code { get; init; }

    /// <summary>Last four digits, for display without revealing the card.</summary>
    public string? LastFour =>
        Number is { Length: >= 4 } number ? number[^4..] : null;

    public string? Expiry => ExpMonth is null && ExpYear is null
        ? null
        : $"{ExpMonth?.PadLeft(2, '0') ?? "??"}/{ExpYear ?? "????"}";

    /// <summary>Redacts the number and CVV; the generated record <c>ToString</c> would print both.</summary>
    public override string ToString() =>
        $"CardDetails {{ Brand = {Brand}, Number = {(Number is null ? "<null>" : $"••••{LastFour}")}, "
        + $"Code = {(Code is null ? "<null>" : "<redacted>")} }}";
}

/// <summary>
/// Personal identity details. Bitwarden omits null members from its JSON, so most of these are
/// absent on any given item — the full set is modelled so a round-trip never drops one.
/// </summary>
public sealed record IdentityDetails
{
    public string? Title { get; init; }
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public string? Address1 { get; init; }
    public string? Address2 { get; init; }
    public string? Address3 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? Company { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Username { get; init; }

    /// <summary>Social security or national insurance number.</summary>
    public string? Ssn { get; init; }

    public string? PassportNumber { get; init; }
    public string? LicenseNumber { get; init; }

    public string? FullName =>
        string.Join(' ', new[] { Title, FirstName, MiddleName, LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } name
            ? name
            : null;

    public string? Address =>
        string.Join(", ", new[] { Address1, Address2, Address3, City, State, PostalCode, Country }
            .Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } address
            ? address
            : null;

    /// <summary>Redacts the government identifiers.</summary>
    public override string ToString() =>
        $"IdentityDetails {{ Name = {FullName}, Ssn = {(Ssn is null ? "<null>" : "<redacted>")}, "
        + $"PassportNumber = {(PassportNumber is null ? "<null>" : "<redacted>")} }}";
}

/// <summary>Secure-note payload. Bitwarden only defines one kind; the text lives in the item's notes.</summary>
public sealed record SecureNoteDetails
{
    public SecureNoteType Type { get; init; } = SecureNoteType.Generic;
}

public enum SecureNoteType
{
    Generic = 0,
}

/// <summary>An SSH key pair held in the vault.</summary>
public sealed record SshKeyDetails
{
    public string? PrivateKey { get; init; }
    public string? PublicKey { get; init; }
    public string? KeyFingerprint { get; init; }

    /// <summary>The algorithm, read off the public key ("ssh-ed25519 AAAA…" gives "ssh-ed25519").</summary>
    public string? Algorithm => PublicKey?.Split(' ', 2).FirstOrDefault();

    /// <summary>Redacts the private key.</summary>
    public override string ToString() =>
        $"SshKeyDetails {{ Algorithm = {Algorithm}, Fingerprint = {KeyFingerprint}, "
        + $"PrivateKey = {(PrivateKey is null ? "<null>" : "<redacted>")} }}";
}

/// <summary>A password this item used previously, kept by Bitwarden when one is changed.</summary>
public sealed record PasswordHistoryEntry
{
    public string? Password { get; init; }
    public DateTimeOffset? LastUsedDate { get; init; }

    /// <summary>Redacts the password — the whole point of this record is that it holds an old one.</summary>
    public override string ToString() =>
        $"PasswordHistoryEntry {{ LastUsedDate = {LastUsedDate}, Password = <redacted> }}";
}

/// <summary>
/// Whether Bitwarden asks for the master password again before revealing this item.
/// </summary>
public enum RepromptType
{
    None = 0,
    MasterPassword = 1,
}
