using BitwardenSharp.Domain.Vault;
using BitwardenSharp.Infrastructure.Bw.Contracts;

namespace BitwardenSharp.Infrastructure.Bw;

/// <summary>Translates between the <c>bw</c> wire shape and the domain model.</summary>
internal static class BwItemMapper
{
    public static VaultItem ToDomain(BwItem wire) => new()
    {
        Id = wire.Id,
        Type = (ItemType)wire.Type,
        Name = wire.Name,
        FolderId = wire.FolderId,
        OrganizationId = wire.OrganizationId,
        Notes = wire.Notes,
        Favorite = wire.Favorite,
        RevisionDate = wire.RevisionDate,
        CreationDate = wire.CreationDate,
        Reprompt = (RepromptType)(wire.Reprompt ?? 0),
        CollectionIds = wire.CollectionIds ?? [],
        Key = wire.Key,
        PasswordHistory = wire.PasswordHistory?
            .Select(h => new PasswordHistoryEntry { Password = h.Password, LastUsedDate = h.LastUsedDate })
            .ToList() ?? [],
        Card = wire.Card is null ? null : new CardDetails
        {
            CardholderName = wire.Card.CardholderName,
            Brand = wire.Card.Brand,
            Number = wire.Card.Number,
            ExpMonth = wire.Card.ExpMonth,
            ExpYear = wire.Card.ExpYear,
            Code = wire.Card.Code,
        },
        Identity = wire.Identity is null ? null : new IdentityDetails
        {
            Title = wire.Identity.Title,
            FirstName = wire.Identity.FirstName,
            MiddleName = wire.Identity.MiddleName,
            LastName = wire.Identity.LastName,
            Address1 = wire.Identity.Address1,
            Address2 = wire.Identity.Address2,
            Address3 = wire.Identity.Address3,
            City = wire.Identity.City,
            State = wire.Identity.State,
            PostalCode = wire.Identity.PostalCode,
            Country = wire.Identity.Country,
            Company = wire.Identity.Company,
            Email = wire.Identity.Email,
            Phone = wire.Identity.Phone,
            Ssn = wire.Identity.Ssn,
            Username = wire.Identity.Username,
            PassportNumber = wire.Identity.PassportNumber,
            LicenseNumber = wire.Identity.LicenseNumber,
        },
        SecureNote = wire.SecureNote is null
            ? null
            : new SecureNoteDetails { Type = (SecureNoteType)wire.SecureNote.Type },
        SshKey = wire.SshKey is null ? null : new SshKeyDetails
        {
            PrivateKey = wire.SshKey.PrivateKey,
            PublicKey = wire.SshKey.PublicKey,
            KeyFingerprint = wire.SshKey.KeyFingerprint,
        },
        Login = wire.Login is null ? null : new LoginDetails
        {
            Username = wire.Login.Username,
            Password = wire.Login.Password,
            Totp = wire.Login.Totp,
            PasswordRevisionDate = wire.Login.PasswordRevisionDate,
            Uris = wire.Login.Uris?
                .Where(u => !string.IsNullOrWhiteSpace(u.Uri))
                .Select(u => new LoginUri { Uri = u.Uri!, Match = (UriMatchType?)u.Match })
                .ToList() ?? [],
        },
        Fields = wire.Fields?
            .Select(f => new CustomField
            {
                Name = f.Name,
                Value = f.Value,
                Type = (FieldType)f.Type,
                LinkedId = f.LinkedId,
            })
            .ToList() ?? [],
        Attachments = wire.Attachments?
            .Select(a => new ItemAttachment
            {
                Id = a.Id,
                FileName = a.FileName,
                Size = long.TryParse(a.Size, out var size) ? size : null,
            })
            .ToList() ?? [],
    };

    /// <summary>
    /// A blank wire object for an item that does not exist yet.
    /// </summary>
    /// <remarks>
    /// Only the discriminators the server needs in order to accept the create; everything else is
    /// filled by <see cref="ApplyTo"/>. No id, and no per-cipher key — both are the vault's to
    /// assign, and sending a borrowed one would attach the new item to another item's key.
    /// </remarks>
    public static BwItem NewWireItem(VaultItem item) => new()
    {
        Type = (int)item.Type,
        Name = item.Name,
        Login = item.Type == ItemType.Login ? new BwLogin() : null,
        SecureNote = item.Type == ItemType.SecureNote ? new BwSecureNote() : null,
        Card = item.Type == ItemType.Card ? new BwCard() : null,
        Identity = item.Type == ItemType.Identity ? new BwIdentity() : null,
    };

    /// <summary>
    /// Writes the domain item's mutable state onto the wire object it came from and returns it.
    /// </summary>
    /// <remarks>
    /// Mutating the original rather than building a fresh <see cref="BwItem"/> is what preserves
    /// <c>collectionIds</c>, <c>reprompt</c>, <c>fido2Credentials</c> and anything a newer CLI has
    /// added that landed in extension data. A <c>bw edit</c> replaces the stored item outright, so
    /// a field absent from the payload is a field deleted from the vault.
    /// </remarks>
    public static BwItem ApplyTo(VaultItem item, BwItem wire)
    {
        wire.Name = item.Name;
        wire.FolderId = item.FolderId;
        wire.Notes = item.Notes;
        wire.Favorite = item.Favorite;

        wire.Fields = item.Fields.Count == 0
            ? null
            : item.Fields.Select(f => new BwField
            {
                Name = f.Name,
                Value = f.Value,
                Type = (int)f.Type,
                LinkedId = f.LinkedId,
            }).ToList();

        if (item.Login is not null)
        {
            wire.Login ??= new BwLogin();
            wire.Login.Username = item.Login.Username;
            wire.Login.Password = item.Login.Password;
            wire.Login.Totp = item.Login.Totp;
            wire.Login.Uris = item.Login.Uris.Count == 0
                ? null
                : item.Login.Uris.Select(u => new BwUri { Uri = u.Uri, Match = (int?)u.Match }).ToList();
        }

        return wire;
    }

    public static VaultFolder ToDomain(BwFolder wire) => new()
    {
        // The pseudo-folder "No Folder" comes back with a null id; represent it as empty so it
        // is a value rather than a hole.
        Id = wire.Id ?? string.Empty,
        Name = wire.Name,
    };
}
