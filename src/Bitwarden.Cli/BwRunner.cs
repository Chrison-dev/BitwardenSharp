using System.Diagnostics;

namespace Bitwarden.Cli
{
    internal static class BwRunner
    {
        private const string BwExecutable = "bw"; // assume 'bw' is available in PATH

    // Static helper - no instance state

        public static bool IsBwAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = BwExecutable,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit(2000);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

    public static async Task<int> LoginAsync(string[] args, Config? config = null)
        {
            // Use supplied args or fall back to config for non-interactive login
            if (config != null && !string.IsNullOrEmpty(config.Email) && config.GetPassword() != null)
            {
                var email = config.Email;
                var password = config.GetPassword()!;
                var arguments = $"login {EscapeArg(email)} {EscapeArg(password)}";
                return await RunBwAsync(arguments);
            }

            var argumentsInteractive = args.Length >= 2 ? $"login {EscapeArg(args[0])} {EscapeArg(args[1])}" : "login";
            return await RunBwAsync(argumentsInteractive);
        }

    public static async Task<int> SyncAsync(string[] args)
        {
            return await RunBwAsync("sync");
        }

    public static async Task<int> ListAsync(string[] args)
        {
            var what = args.Length > 0 ? args[0] : "items";
            return await RunBwAsync($"list {EscapeArg(what)}");
        }

        public static async Task<int> StatusAsync(string[] args)
        {
            return await RunBwAsync("status");
        }

        public static async Task<int> LogoutAsync(string[] args)
        {
            return await RunBwAsync("logout");
        }

        public static async Task<int> ItemsGetAsync(string[] args)
        {
            if (args.Length == 0) return 2; // failure
            var id = args[0];
            return await RunBwAsync($"get item {EscapeArg(id)}");
        }

    private static async Task<int> RunBwAsync(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = BwExecutable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) throw new InvalidOperationException("Failed to start 'bw' process. Is Bitwarden CLI installed and on PATH?");

            var outputTask = ConsumeStreamAsync(proc.StandardOutput, Console.Out);
            var errorTask = ConsumeStreamAsync(proc.StandardError, Console.Error);

            await Task.WhenAll(outputTask, errorTask);

            proc.WaitForExit();
            return proc.ExitCode;
        }

        private static async Task ConsumeStreamAsync(System.IO.StreamReader reader, TextWriter writer)
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                await writer.WriteLineAsync(line);
            }
        }

        private static string EscapeArg(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            return s.Contains(' ') ? "\"" + s.Replace("\"", "\\\"") + "\"" : s;
        }
    }
}
