using System.Text.Json.Serialization;

namespace BitwardenManager.Core.Models;

public enum ItemType
{
    Login = 1,
    SecureNote = 2,
    Card = 3,
    Identity = 4
}

public enum UriMatchType
{
    Domain = 0,
    Host = 1,
    StartsWith = 2,
    Exact = 3,
    RegularExpression = 4,
    Never = 5
}

public class VaultItem
{
    public string? Id { get; set; }
    public string? OrganizationId { get; set; }
    public string? FolderId { get; set; }
    public ItemType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool Favorite { get; set; }
    public DateTime? RevisionDate { get; set; }
    public DateTime? CreationDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    public bool Reprompt { get; set; }
    
    // Type-specific data
    public LoginData? Login { get; set; }
    public SecureNoteData? SecureNote { get; set; }
    public CardData? Card { get; set; }
    public IdentityData? Identity { get; set; }
    
    public List<Field> Fields { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = new();
}

public class LoginData
{
    public List<Uri> Uris { get; set; } = new();
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Totp { get; set; }
    public DateTime? PasswordRevisionDate { get; set; }
}

public class Uri
{
    public string? UriValue { get; set; }
    public UriMatchType? Match { get; set; }
}

public class SecureNoteData
{
    public int Type { get; set; } = 0; // Generic
}

public class CardData
{
    public string? CardholderName { get; set; }
    public string? Brand { get; set; }
    public string? Number { get; set; }
    public string? ExpMonth { get; set; }
    public string? ExpYear { get; set; }
    public string? Code { get; set; }
}

public class IdentityData
{
    public string? Title { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Company { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? SSN { get; set; }
    public string? Username { get; set; }
    public string? PassportNumber { get; set; }
    public string? LicenseNumber { get; set; }
}

public class Field
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public int Type { get; set; } // 0 = text, 1 = hidden, 2 = boolean
    public bool LinkedId { get; set; }
}

public class Attachment
{
    public string? Id { get; set; }
    public string? FileName { get; set; }
    public string? Key { get; set; }
    public long Size { get; set; }
    public string? SizeName { get; set; }
    public string? Url { get; set; }
}

public class Folder
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? RevisionDate { get; set; }
}

public class Collection
{
    public string? Id { get; set; }
    public string? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public DateTime? RevisionDate { get; set; }
}
