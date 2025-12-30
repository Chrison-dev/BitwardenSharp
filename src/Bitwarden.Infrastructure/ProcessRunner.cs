using System.Diagnostics;
using Bitwarden.Application;

namespace Bitwarden.Infrastructure
{
    public class ProcessRunner : IProcessRunner
    {
        public bool IsAvailable(string executable, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = args,
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

        public async Task<int> RunAsync(string executable, string arguments, TextWriter? output = null, TextWriter? error = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) throw new InvalidOperationException($"Failed to start '{executable}' process.");

            var outputTask = ConsumeStreamAsync(proc.StandardOutput, output ?? Console.Out);
            var errorTask = ConsumeStreamAsync(proc.StandardError, error ?? Console.Error);

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
    }
}
