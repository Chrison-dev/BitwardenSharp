namespace BitwardenSharp.Domain.Vault;

/// <summary>Bitwarden's item discriminator, using the CLI's own numeric values.</summary>
public enum ItemType
{
    Login = 1,
    SecureNote = 2,
    Card = 3,
    Identity = 4,
    SshKey = 5,
}
