using Microsoft.Extensions.Logging;

namespace BitwardenSharp.Infrastructure.Serve;

/// <summary>
/// Starts <c>bw serve</c> on first use and hands out the <see cref="HttpClient"/> pointed at it.
/// </summary>
/// <remarks>
/// <para>
/// The server has to be running before its port — and therefore the client's base address — is
/// known, so acquiring the client is inherently asynchronous. This type exists to keep that
/// asynchrony honest.
/// </para>
/// <para>
/// <b>Nothing here may block on a task.</b> An earlier version started the server from a DI
/// factory with <c>StartAsync().GetAwaiter().GetResult()</c>. Resolving the first service happens
/// on the UI thread, where Avalonia installs a <see cref="SynchronizationContext"/>; the awaits
/// inside startup then tried to resume on the thread that was blocked waiting for them, and the
/// app deadlocked before its window appeared. The gate below is a <see cref="SemaphoreSlim"/>
/// awaited asynchronously for exactly that reason — never <c>lock</c>, never <c>.Result</c>.
/// </para>
/// </remarks>
public sealed class BwServeConnection(
    BwServeProcess server,
    ILogger<BwServeConnection>? logger = null) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HttpClient? _http;

    /// <summary>
    /// The client for the local API, starting the server if this is the first call. Concurrent
    /// callers wait on the same startup rather than racing to spawn two servers.
    /// </summary>
    public async Task<HttpClient> GetClientAsync(CancellationToken cancellationToken = default)
    {
        if (_http is not null) return _http;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_http is null)
            {
                await server.StartAsync(cancellationToken);
                _http = new HttpClient
                {
                    BaseAddress = server.BaseAddress,
                    Timeout = TimeSpan.FromMinutes(2),
                };
                logger?.LogDebug("Vault Management API ready at {BaseAddress}", server.BaseAddress);
            }
            return _http;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _http?.Dispose();
        _http = null;
        await server.DisposeAsync();
        _gate.Dispose();
    }
}
