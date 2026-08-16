using BitwardenSharp.Domain.Vault;

namespace BitwardenSharp.Application.Tests;

/// <summary>Terse construction of vault items for tests.</summary>
internal static class TestVault
{
    private static int _sequence;

    public static VaultItem Login(
        string name,
        string? username = "user@example.com",
        string? password = "hunter2",
        string[]? uris = null,
        string? folderId = null,
        string? totp = null,
        string? notes = null,
        CustomField[]? fields = null,
        ItemAttachment[]? attachments = null,
        DateTimeOffset? revised = null) => new()
    {
        Id = $"item-{Interlocked.Increment(ref _sequence):D4}",
        Type = ItemType.Login,
        Name = name,
        FolderId = folderId,
        Notes = notes,
        Fields = fields ?? [],
        Attachments = attachments ?? [],
        RevisionDate = revised,
        Login = new LoginDetails
        {
            Username = username,
            Password = password,
            Totp = totp,
            Uris = (uris ?? []).Select(u => new LoginUri { Uri = u }).ToList(),
        },
    };
}
