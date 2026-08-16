namespace BitwardenSharp.Application.Abstractions;

/// <summary>The outcome of an unlock attempt.</summary>
public sealed record UnlockResult
{
    public required bool Succeeded { get; init; }

    /// <summary>Why it failed, safe to show a user. Null on success.</summary>
    public string? Error { get; init; }

    public static UnlockResult Success() => new() { Succeeded = true };

    public static UnlockResult Failure(string error) => new() { Succeeded = false, Error = error };
}

/// <summary>
/// Controls the lock state of the vault.
/// </summary>
/// <remarks>
/// Separate from <see cref="IVaultClient"/> on purpose. Reading and merging need an unlocked
/// vault but have no business unlocking one, and the CLI presentation never unlocks at all — it
/// inherits a session from the environment. Only the desktop host takes a master password, so
/// only it depends on this.
/// </remarks>
public interface IVaultSession
{
    Task<VaultStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlocks the vault and retains the resulting session key for subsequent vault calls.
    /// </summary>
    /// <remarks>
    /// The password is used once and not retained. Implementations must not place it on a
    /// command line — see the note on the <c>bw</c> adapter.
    /// </remarks>
    Task<UnlockResult> UnlockAsync(string masterPassword, CancellationToken cancellationToken = default);

    /// <summary>Discards the session key and locks the vault.</summary>
    Task LockAsync(CancellationToken cancellationToken = default);
}
