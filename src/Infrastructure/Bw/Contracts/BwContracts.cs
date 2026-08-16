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
