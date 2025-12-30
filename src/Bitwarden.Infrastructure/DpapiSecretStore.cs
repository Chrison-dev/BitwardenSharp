using System.Text;
using Bitwarden.Application;

namespace Bitwarden.Infrastructure
{
    public class DpapiSecretStore : ISecretStore
    {
        public void SetSecret(string service, string account, string secret)
        {
            var protectedBytes = Bitwarden.Application.Config.Protect(secret);
            var file = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "bitwarden-cli-helper", $"secret_{Escape(service)}_{Escape(account)}.bin");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file) ?? ".");
            System.IO.File.WriteAllBytes(file, protectedBytes);
        }

        public string? GetSecret(string service, string account)
        {
            var file = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "bitwarden-cli-helper", $"secret_{Escape(service)}_{Escape(account)}.bin");
            if (!System.IO.File.Exists(file)) return null;
            var data = System.IO.File.ReadAllBytes(file);
            return Bitwarden.Application.Config.Unprotect(data);
        }

        private static string Escape(string s) => s?.Replace("/", "_") ?? "";
    }
}
