using System.Text.Json;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Infrastructure.Bw.Contracts;
using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Infrastructure.Bw;

/// <summary>
/// Unlocks the vault through the <c>bw</c> client and holds the resulting session key.
/// </summary>
/// <remarks>
/// <para>
/// <c>bw unlock</c> offers three ways to supply a password. Two are unacceptable here:
/// the positional argument puts it in <c>argv</c>, where <c>ps</c> shows it to every local user,
/// and <c>--passwordfile</c> writes it to disk. This uses <c>--passwordenv</c>, which names an
/// environment variable set on the child process only — never on ours, and never written down.
/// </para>
/// <para>
/// The variable name is randomised per attempt so nothing can be primed to read a fixed one, and
/// the value is dropped as soon as the process exits.
/// </para>
/// </remarks>
public sealed class BwVaultSession(
    BwProcessRunner runner,
    BwCliOptions options,
    ILogger<BwVaultSession>? logger = null) : IVaultSession
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<VaultStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var json = await runner.RunAsync(["status"], cancellationToken: cancellationToken);
        var status = JsonSerializer.Deserialize<BwStatus>(json, Json)
                     ?? throw new InvalidOperationException("bw status returned nothing");
        return new VaultStatus
        {
            Status = status.Status,
            UserEmail = status.UserEmail,
            ServerUrl = status.ServerUrl,
            LastSync = status.LastSync,
        };
    }

    public async Task<UnlockResult> UnlockAsync(
        string masterPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(masterPassword)) return UnlockResult.Failure("Enter your master password.");

        var variable = $"BWSHARP_MP_{Guid.NewGuid():N}";
        var result = await runner.TryRunAsync(
            ["unlock", "--raw", "--passwordenv", variable],
            environment: new Dictionary<string, string> { [variable] = masterPassword },
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            logger?.LogWarning("Unlock rejected (exit {ExitCode})", result.ExitCode);
            // bw's own message is safe to surface: it says the password is wrong, not what it is.
            var error = result.StandardError.Trim();
            return UnlockResult.Failure(error.Length == 0 ? "Could not unlock the vault." : error);
        }

        var session = result.StandardOutput.Trim();
        if (session.Length == 0) return UnlockResult.Failure("bw returned an empty session key.");

        options.Session = session;
        logger?.LogInformation("Vault unlocked");
        return UnlockResult.Success();
    }

    public async Task LockAsync(CancellationToken cancellationToken = default)
    {
        await runner.TryRunAsync(["lock"], cancellationToken: cancellationToken);
        options.Session = null;
        logger?.LogInformation("Vault locked");
    }
}
