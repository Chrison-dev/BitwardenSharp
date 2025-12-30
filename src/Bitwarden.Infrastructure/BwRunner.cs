using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Bitwarden.Application;

namespace Bitwarden.Infrastructure
{
    public class BwRunner : Bitwarden.Application.IBwRunner
    {
        private const string BwExecutable = "bw"; // assume 'bw' is available in PATH
        private readonly Bitwarden.Application.IProcessRunner _processRunner;

        public BwRunner(Bitwarden.Application.IProcessRunner processRunner)
        {
            _processRunner = processRunner;
        }

        public bool IsBwAvailable()
        {
            return _processRunner.IsAvailable(BwExecutable, "--version");
        }

    public async Task<int> LoginAsync(string[] args, Bitwarden.Application.Config? config = null)
        {
            if (config != null && !string.IsNullOrEmpty(config.Email) && config.GetPassword() != null)
            {
                var email = config.Email;
                var password = config.GetPassword()!;
                var arguments = $"login {EscapeArg(email)} {EscapeArg(password)}";
                return await _processRunner.RunAsync(BwExecutable, arguments);
            }

            var argumentsInteractive = args.Length >= 2 ? $"login {EscapeArg(args[0])} {EscapeArg(args[1])}" : "login";
            return await _processRunner.RunAsync(BwExecutable, argumentsInteractive);
        }

        public async Task<int> SyncAsync(string[] args)
        {
            return await _processRunner.RunAsync(BwExecutable, "sync");
        }

        public async Task<int> ListAsync(string[] args)
        {
            var what = args.Length > 0 ? args[0] : "items";
            return await _processRunner.RunAsync(BwExecutable, $"list {EscapeArg(what)}");
        }

        public async Task<int> StatusAsync(string[] args)
        {
            return await _processRunner.RunAsync(BwExecutable, "status");
        }

        public async Task<int> LogoutAsync(string[] args)
        {
            return await _processRunner.RunAsync(BwExecutable, "logout");
        }

        public async Task<int> ItemsGetAsync(string[] args)
        {
            if (args.Length == 0) return 2; // failure
            var id = args[0];
            return await _processRunner.RunAsync(BwExecutable, $"get item {EscapeArg(id)}");
        }

        // Process execution and streaming are delegated to IProcessRunner

        private static string EscapeArg(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            return s.Contains(' ') ? "\"" + s.Replace("\"", "\\\"") + "\"" : s;
        }
    }
}
