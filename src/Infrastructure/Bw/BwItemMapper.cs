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
