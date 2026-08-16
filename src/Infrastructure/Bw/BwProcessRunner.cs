using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Infrastructure.Bw;

/// <summary>The result of one <c>bw</c> invocation.</summary>
public sealed record BwResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Raised when <c>bw</c> exits non-zero.</summary>
public sealed class BwCommandException(string command, BwResult result)
    : Exception($"`bw {command}` failed with exit code {result.ExitCode}: {Summarise(result.StandardError)}")
{
    public BwResult Result { get; } = result;

    private static string Summarise(string stderr)
    {
        var text = stderr.Trim();
        return text.Length <= 400 ? text : text[..400] + "…";
    }
}

/// <summary>
/// Runs the official <c>bw</c> client.
/// </summary>
/// <remarks>
/// <para>
/// Two rules hold everywhere in this class, and both exist because the arguments of a running
/// process are world-readable via <c>ps</c> on every platform this ships to:
/// </para>
/// <list type="number">
/// <item><description>
/// Arguments go through <see cref="ProcessStartInfo.ArgumentList"/>, never a joined string. The
/// runtime quotes each element correctly per platform, so nothing has to be escaped by hand.
/// </description></item>
/// <item><description>
/// <b>Secrets never become arguments.</b> The session key travels in the environment, and the
/// base64 item payload for <c>edit</c>/<c>create</c> — which contains the password in clear —
/// is piped to stdin, which <c>bw</c> accepts in place of the positional argument.
/// </description></item>
/// </list>
/// </remarks>
public sealed class BwProcessRunner(BwCliOptions options, ILogger<BwProcessRunner>? logger = null)
{
    /// <summary>Runs <c>bw</c> and returns its output, throwing if it exits non-zero.</summary>
    public async Task<string> RunAsync(
        IEnumerable<string> arguments,
        string? standardInput = null,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var result = await TryRunAsync(arguments, standardInput, environment, cancellationToken);
        if (!result.Succeeded) throw new BwCommandException(DescribeSafely(arguments), result);
        return result.StandardOutput;
    }

    /// <summary>Runs <c>bw</c> and returns the result without throwing on a non-zero exit.</summary>
    public async Task<BwResult> TryRunAsync(
        IEnumerable<string> arguments,
        string? standardInput = null,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var args = arguments.ToList();

        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        // The session key is a decryption key for the whole vault. It goes in the environment of
        // the child alone — not the command line, and not this process's own environment.
        if (!string.IsNullOrEmpty(options.Session)) startInfo.Environment["BW_SESSION"] = options.Session;

        // bw asks for input on a TTY when it wants a password. Nothing here should ever reach
        // that state, and if it does we want a clean failure rather than a hung process.
        startInfo.Environment["BW_NOINTERACTION"] = "true";

        // Caller-supplied values -- currently the master password for `unlock --passwordenv`.
        // Set on the child only; this process's own environment is never touched.
        if (environment is not null)
            foreach (var (name, value) in environment) startInfo.Environment[name] = value;

        logger?.LogDebug("Running bw {Arguments}", DescribeSafely(args));

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"could not start '{options.ExecutablePath}'");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync(cancellationToken);
        return new BwResult(process.ExitCode, (await stdout).Trim(), (await stderr).Trim());
    }

    /// <summary>
    /// Renders an argument list for logging with anything past a known-safe verb elided, so a
    /// debug log can never become a record of item ids or payloads.
    /// </summary>
    private static string DescribeSafely(IEnumerable<string> arguments)
    {
        var args = arguments.ToList();
        var safe = new StringBuilder();
        for (var i = 0; i < args.Count; i++)
        {
            // The first two tokens are the verb and object ("get item", "list items"); anything
            // after them can identify a specific secret.
            safe.Append(i < 2 ? args[i] : "<…>");
            if (i < args.Count - 1) safe.Append(' ');
            if (i >= 2) break;
        }
        return safe.ToString();
    }
}
