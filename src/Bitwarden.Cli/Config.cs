using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bitwarden.Cli
{
    public class Config
    {
        public string? Email { get; set; }
        public byte[]? EncryptedPassword { get; set; }
        public string? ClientId { get; set; }
        public byte[]? EncryptedClientSecret { get; set; }

        private readonly ISecretStore _secretStore;
        private static string AppFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bitwarden-cli-helper");
        private static string ConfigPath => Path.Combine(AppFolder, "config.json");
        private const string DefaultAccount = "default";

        public Config(ISecretStore secretStore)
        {
            _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
            LoadFromFile();
        }

        private void LoadFromFile()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                var loaded = JsonSerializer.Deserialize<ConfigDto>(json);
                if (loaded == null) return;

                Email = loaded.Email;
                ClientId = loaded.ClientId;
                EncryptedPassword = loaded.EncryptedPassword;
                EncryptedClientSecret = loaded.EncryptedClientSecret;
            }
            catch
            {
                // ignore load errors
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(AppFolder);

            // For non-Windows platforms do not persist secrets in the config JSON; they are stored in the OS keyring instead.
            var copy = new ConfigDto
            {
                Email = this.Email,
                ClientId = this.ClientId,
                EncryptedPassword = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? this.EncryptedPassword : null,
                EncryptedClientSecret = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? this.EncryptedClientSecret : null
            };

            var json = JsonSerializer.Serialize(copy);
            File.WriteAllText(ConfigPath, json, Encoding.UTF8);
        }

        public void Clear()
        {
            var email = Email; // capture for deletion
            Email = null;
            EncryptedPassword = null;
            ClientId = null;
            EncryptedClientSecret = null;

            try
            {
                if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: failed to delete config file: {ex.Message}");
            }

            // Remove any stored secrets in secret store (best-effort)
            try
            {
                _secretStore.SetSecret("bitwarden-cli", email ?? DefaultAccount, string.Empty);
            }
            catch
            {
                // best-effort removal of secret; ignore failures
            }
        }

        public static byte[] Protect(string plaintext)
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext ?? string.Empty);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            }
            // Non-Windows: return raw bytes (we don't persist them in JSON; OS keyring is used instead)
            return bytes;
        }

        public static string Unprotect(byte[] protectedData)
        {
            if (protectedData == null) return string.Empty;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var bytes = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            return Encoding.UTF8.GetString(protectedData);
        }

        // Convenience wrappers
        public void SetPassword(string password)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                EncryptedPassword = Protect(password);
            }
            else
            {
                _secretStore.SetSecret("bitwarden-cli", Email ?? DefaultAccount, password);
                EncryptedPassword = null;
            }
        }

        public string? GetPassword()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (EncryptedPassword == null) return null;
                try { return Unprotect(EncryptedPassword); } catch { return null; }
            }
            else
            {
                try { return _secretStore.GetSecret("bitwarden-cli", Email ?? DefaultAccount); } catch { return null; }
            }
        }

        private sealed class ConfigDto
        {
            public string? Email { get; set; }
            public byte[]? EncryptedPassword { get; set; }
            public string? ClientId { get; set; }
            public byte[]? EncryptedClientSecret { get; set; }
        }
    }
}
