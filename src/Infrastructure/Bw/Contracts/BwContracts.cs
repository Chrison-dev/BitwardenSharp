using System.Text.Json.Serialization;

namespace BitwardenSharp.Infrastructure.Bw.Contracts;

/// <summary>
/// The wire shape of a vault item as the <c>bw</c> CLI emits and accepts it.
/// </summary>
/// <remarks>
/// Kept separate from the domain model on purpose. An item written back to the vault replaces the
/// stored one wholesale, so this type has to round-trip <b>every</b> property <c>bw</c> gave us,
/// including ones the domain has no opinion about. <see cref="ExtensionData"/> catches anything
/// added by a future CLI version, so an unrecognised field survives a merge instead of being
/// silently deleted from the vault.
/// </remarks>
public sealed class BwItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("organizationId")] public string? OrganizationId { get; set; }
    [JsonPropertyName("folderId")] public string? FolderId { get; set; }
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("notes")] public string? Notes { get; set; }
    [JsonPropertyName("favorite")] public bool Favorite { get; set; }
    [JsonPropertyName("login")] public BwLogin? Login { get; set; }
    [JsonPropertyName("card")] public BwCard? Card { get; set; }
    [JsonPropertyName("identity")] public BwIdentity? Identity { get; set; }
    [JsonPropertyName("secureNote")] public BwSecureNote? SecureNote { get; set; }
    [JsonPropertyName("sshKey")] public BwSshKey? SshKey { get; set; }
    [JsonPropertyName("passwordHistory")] public List<BwPasswordHistory>? PasswordHistory { get; set; }

    /// <summary>Per-cipher key. Opaque, and fatal to drop on a write.</summary>
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("fields")] public List<BwField>? Fields { get; set; }
    [JsonPropertyName("attachments")] public List<BwAttachment>? Attachments { get; set; }
    [JsonPropertyName("collectionIds")] public List<string>? CollectionIds { get; set; }
    [JsonPropertyName("revisionDate")] public DateTimeOffset? RevisionDate { get; set; }
    [JsonPropertyName("creationDate")] public DateTimeOffset? CreationDate { get; set; }
    [JsonPropertyName("deletedDate")] public DateTimeOffset? DeletedDate { get; set; }
    [JsonPropertyName("reprompt")] public int? Reprompt { get; set; }

    /// <summary>Anything this version of the model does not name, preserved verbatim.</summary>
    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwLogin
{
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("password")] public string? Password { get; set; }
    [JsonPropertyName("totp")] public string? Totp { get; set; }
    [JsonPropertyName("uris")] public List<BwUri>? Uris { get; set; }
    [JsonPropertyName("passwordRevisionDate")] public DateTimeOffset? PasswordRevisionDate { get; set; }
    [JsonPropertyName("fido2Credentials")] public List<object>? Fido2Credentials { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwUri
{
    [JsonPropertyName("uri")] public string? Uri { get; set; }
    [JsonPropertyName("match")] public int? Match { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwField
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
    [JsonPropertyName("type")] public int Type { get; set; }
    [JsonPropertyName("linkedId")] public string? LinkedId { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwAttachment
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("fileName")] public string? FileName { get; set; }
    [JsonPropertyName("size")] public string? Size { get; set; }
    [JsonPropertyName("sizeName")] public string? SizeName { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwCard
{
    [JsonPropertyName("cardholderName")] public string? CardholderName { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("number")] public string? Number { get; set; }
    [JsonPropertyName("expMonth")] public string? ExpMonth { get; set; }
    [JsonPropertyName("expYear")] public string? ExpYear { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwIdentity
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("middleName")] public string? MiddleName { get; set; }
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    [JsonPropertyName("address1")] public string? Address1 { get; set; }
    [JsonPropertyName("address2")] public string? Address2 { get; set; }
    [JsonPropertyName("address3")] public string? Address3 { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("postalCode")] public string? PostalCode { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("company")] public string? Company { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("ssn")] public string? Ssn { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("passportNumber")] public string? PassportNumber { get; set; }
    [JsonPropertyName("licenseNumber")] public string? LicenseNumber { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwSecureNote
{
    [JsonPropertyName("type")] public int Type { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwSshKey
{
    [JsonPropertyName("privateKey")] public string? PrivateKey { get; set; }
    [JsonPropertyName("publicKey")] public string? PublicKey { get; set; }
    [JsonPropertyName("keyFingerprint")] public string? KeyFingerprint { get; set; }

    [JsonExtensionData] public Dictionary<string, object>? ExtensionData { get; set; }
}

public sealed class BwPasswordHistory
{
    [JsonPropertyName("password")] public string? Password { get; set; }
    [JsonPropertyName("lastUsedDate")] public DateTimeOffset? LastUsedDate { get; set; }
}

public sealed class BwFolder
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public sealed class BwStatus
{
    [JsonPropertyName("status")] public string Status { get; set; } = "unknown";
    [JsonPropertyName("userEmail")] public string? UserEmail { get; set; }
    [JsonPropertyName("serverUrl")] public string? ServerUrl { get; set; }
    [JsonPropertyName("lastSync")] public DateTimeOffset? LastSync { get; set; }
}
