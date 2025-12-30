using System.Collections.Concurrent;

namespace Bitwarden.Cli
{
    // Simple in-memory secret store useful for unit tests and CI.
    public class InMemorySecretStore : ISecretStore
    {
        private readonly ConcurrentDictionary<string, string> _store = new();

        private static string Key(string service, string account) => $"{service}||{account}";

        public void SetSecret(string service, string account, string secret)
        {
            _store[Key(service, account)] = secret ?? string.Empty;
        }

        public string? GetSecret(string service, string account)
        {
            _store.TryGetValue(Key(service, account), out var v);
            return v;
        }
    }
}
