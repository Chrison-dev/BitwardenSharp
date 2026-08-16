namespace BitwardenSharp.Infrastructure.Bw;

/// <summary>How to reach the <c>bw</c> client.</summary>
public sealed class BwCliOptions
{
    /// <summary>Path to the executable. Resolved from PATH when left as the bare name.</summary>
    public string ExecutablePath { get; set; } = "bw";

    /// <summary>
    /// The vault session key.
    /// </summary>
    /// <remarks>
    /// Held in memory for the life of the process and passed to child processes through their
    /// environment. It is never written to disk and never becomes a command-line argument.
    /// Defaults to <c>BW_SESSION</c> from the environment, which is how <c>bw unlock --raw</c>
    /// is normally plumbed through.
    /// </remarks>
    public string? Session { get; set; } = Environment.GetEnvironmentVariable("BW_SESSION");
}
