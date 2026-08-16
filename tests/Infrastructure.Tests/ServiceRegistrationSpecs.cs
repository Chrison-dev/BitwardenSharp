using System.Diagnostics;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Infrastructure;
using BitwardenSharp.Infrastructure.Serve;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace BitwardenSharp.Infrastructure.Tests;

public class ServiceRegistrationSpecs
{
    /// <summary>
    /// Regression. Resolving a service must not start <c>bw serve</c>.
    /// </summary>
    /// <remarks>
    /// The desktop host resolves its first service on the UI thread, where Avalonia has installed
    /// a <see cref="SynchronizationContext"/>. A registration that blocked on start-up
    /// (<c>StartAsync().GetAwaiter().GetResult()</c>) deadlocked instantly: the awaits inside
    /// start-up needed the UI thread to resume, and the UI thread was blocked waiting for them.
    /// The app froze before drawing its window.
    ///
    /// The executable path here points at nothing. If resolution ever tries to launch it, the
    /// test fails — either by throwing, or by taking far longer than construction should.
    /// </remarks>
    [Fact]
    public async Task Resolving_the_serve_client_starts_no_process()
    {
        var services = new ServiceCollection();
        services.AddBitwardenServe(o => o.ExecutablePath = "/nonexistent/definitely-not-bw");

        await using var provider = services.BuildServiceProvider();

        var stopwatch = Stopwatch.StartNew();
        var client = Should.NotThrow(provider.GetRequiredService<IVaultClient>);
        stopwatch.Stop();

        client.ShouldNotBeNull();
        stopwatch.Elapsed.ShouldBeLessThan(
            TimeSpan.FromSeconds(2),
            "resolving a service must construct objects, never launch or wait on a server");
    }

    [Fact]
    public async Task The_serve_client_satisfies_both_ports_as_one_instance()
    {
        var services = new ServiceCollection();
        services.AddBitwardenServe(o => o.ExecutablePath = "/nonexistent/definitely-not-bw");

        await using var provider = services.BuildServiceProvider();

        // One connection, one server. Registering these separately would spawn two.
        provider.GetRequiredService<IVaultClient>()
            .ShouldBeSameAs(provider.GetRequiredService<IVaultSession>());
    }

    [Fact]
    public void The_cli_client_starts_no_process_on_resolution_either()
    {
        var services = new ServiceCollection();
        services.AddBitwardenCli(o => o.ExecutablePath = "/nonexistent/definitely-not-bw");

        using var provider = services.BuildServiceProvider();

        Should.NotThrow(provider.GetRequiredService<IVaultClient>).ShouldNotBeNull();
    }

    [Fact]
    public async Task Serve_startup_surfaces_a_missing_executable_rather_than_hanging()
    {
        var options = new BwServeOptions { ExecutablePath = "/nonexistent/definitely-not-bw" };
        await using var connection = new BwServeConnection(new BwServeProcess(options));

        // Must fail fast and loudly, not sit until the startup timeout.
        var act = async () => await connection.GetClientAsync(TestContext.Current.CancellationToken);

        await act.ShouldThrowAsync<Exception>();
    }
}
