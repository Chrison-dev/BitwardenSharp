using System.Diagnostics;
using System.Text;

namespace Bitwarden.Infrastructure
{
    internal static class OSKeyring
    {
        public static void SetSecret(string service, string account, string secret)
        {
            if (OperatingSystem.IsMacOS())
            {
                // security add-generic-password -a <account> -s <service> -w <password> -U
                RunCommand("security", $"add-generic-password -a {Escape(account)} -s {Escape(service)} -w {Escape(secret)} -U");
            }
            else if (OperatingSystem.IsLinux())
            {
                // secret-tool store --label="label" service <service> account <account>
                var psi = new ProcessStartInfo
                {
                    FileName = "secret-tool",
                    Arguments = $"store --label=\"{Escape(service)}\" service {Escape(service)} account {Escape(account)}",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var p = Process.Start(psi) ?? throw new InvalidOperationException("secret-tool not available");
                p.StandardInput.Write(secret);
                p.StandardInput.Close();
                p.WaitForExit();
                if (p.ExitCode != 0) throw new InvalidOperationException("secret-tool store failed");
            }
            else
            {
                throw new PlatformNotSupportedException("OS keyring is not supported on this platform");
            }
        }

        public static string? GetSecret(string service, string account)
        {
            if (OperatingSystem.IsMacOS())
            {
                // security find-generic-password -a <account> -s <service> -w
                return RunCommandAndGetOutput("security", $"find-generic-password -a {Escape(account)} -s {Escape(service)} -w");
            }
            else if (OperatingSystem.IsLinux())
            {
                // secret-tool lookup service <service> account <account>
                return RunCommandAndGetOutput("secret-tool", $"lookup service {Escape(service)} account {Escape(account)}");
            }
            else
            {
                throw new PlatformNotSupportedException("OS keyring is not supported on this platform");
            }
        }

        private static string RunCommandAndGetOutput(string cmd, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var p = Process.Start(psi);
            if (p == null) throw new InvalidOperationException($"{cmd} not available");
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new InvalidOperationException($"{cmd} returned exit code {p.ExitCode}: {p.StandardError.ReadToEnd()}");
            return output.TrimEnd('\n', '\r');
        }

        private static void RunCommand(string cmd, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var p = Process.Start(psi);
            if (p == null) throw new InvalidOperationException($"{cmd} not available");
            p.WaitForExit();
            if (p.ExitCode != 0) throw new InvalidOperationException($"{cmd} returned exit code {p.ExitCode}: {p.StandardError.ReadToEnd()}");
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\"", "\\\"");
        }
    }
}
