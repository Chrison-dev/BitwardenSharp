using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Infrastructure.Serve;

/// <summary>How to run the local Vault Management API.</summary>
public sealed class BwServeOptions
{
    public string ExecutablePath { get; set; } = "bw";

    /// <summary>
    /// Loopback only. <c>bw serve</c> accepts <c>all</c> to bind every interface; do not use it —
    /// the API has no authentication whatsoever.
    /// </summary>
    public string Hostname { get; set; } = "localhost";

    /// <summary>Zero picks a free ephemeral port, which is the sane default.</summary>
    public int Port { get; set; }

    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Owns a child <c>bw serve</c> process exposing the Vault Management API.
/// </summary>
/// <remarks>
/// <para>
/// This is the right transport for a long-running host: one Node process serves every call over
/// HTTP, instead of paying a fresh ~0.5s interpreter start-up per <c>bw</c> invocation. Behind a
/// GUI listing 800 items that difference is the whole user experience.
/// </para>
/// <para>
/// <b>The API is unauthenticated.</b> Anything that can reach the port can read the entire vault
/// while it is unlocked — there is no token, no header, no handshake. The mitigations here are
/// therefore structural: bind loopback only, take a random ephemeral port rather than the
/// well-known 8087, own the process as a child, and kill it on dispose so the window is exactly
/// the lifetime of this object. Any local process running as this user can still reach it during
/// that window; that is inherent to the feature, not something this class can fix.
/// </para>
/// </remarks>
public sealed class BwServeProcess(BwServeOptions options, ILogger<BwServeProcess>? logger = null)
    : IAsyncDisposable
{
    private Process? _process;

    public Uri BaseAddress { get; private set; } = null!;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null) return;

        var port = options.Port == 0 ? FreePort() : options.Port;

        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("serve");
        startInfo.ArgumentList.Add("--hostname");
        startInfo.ArgumentList.Add(options.Hostname);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString());

        _process = Process.Start(startInfo)
                   ?? throw new InvalidOperationException($"could not start '{options.ExecutablePath} serve'");

        BaseAddress = new Uri($"http://{options.Hostname}:{port}/");
        logger?.LogInformation("Started bw serve on {BaseAddress} (pid {Pid})", BaseAddress, _process.Id);

        await WaitUntilAnsweringAsync(cancellationToken);
    }

    /// <summary>
    /// Polls <c>/status</c> until the server answers. <c>bw serve</c> prints its banner before the
    /// listener is actually accepting, so waiting on stdout would race.
    /// </summary>
    private async Task WaitUntilAnsweringAsync(CancellationToken cancellationToken)
    {
        using var probe = new HttpClient { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTimeOffset.UtcNow + options.StartupTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_process!.HasExited)
            {
                var stderr = await _process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"bw serve exited immediately (code {_process.ExitCode}): {stderr.Trim()}");
            }

            try
            {
                using var response = await probe.GetAsync("status", cancellationToken);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { /* not listening yet */ }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException($"bw serve did not answer within {options.StartupTimeout.TotalSeconds:0}s");
    }

    /// <summary>Asks the OS for an unused port by binding one and immediately releasing it.</summary>
    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null) return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not stop bw serve cleanly");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
