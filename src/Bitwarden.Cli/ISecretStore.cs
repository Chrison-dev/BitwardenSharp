namespace Bitwarden.Cli
{
    public interface ISecretStore
    {
        void SetSecret(string service, string account, string secret);
        string? GetSecret(string service, string account);
    }
}
