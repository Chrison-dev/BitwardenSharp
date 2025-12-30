using Bitwarden.Application;

namespace Bitwarden.Infrastructure
{
    public class OsKeyringSecretStore : ISecretStore
    {
        public void SetSecret(string service, string account, string secret)
        {
            OSKeyring.SetSecret(service, account, secret);
        }

        public string? GetSecret(string service, string account)
        {
            return OSKeyring.GetSecret(service, account);
        }
    }
}
