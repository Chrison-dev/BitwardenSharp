using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Infrastructure.Bw;
using BitwardenSharp.Infrastructure.Serve;
using Microsoft.Extensions.DependencyInjection;

namespace BitwardenSharp.Infrastructure;

/// <summary>Registers the `bw`-backed adapter for the Application's IVaultClient port.</summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddBitwardenCli(
        this IServiceCollection services,
        Action<BwCliOptions>? configure = null)
    {
        var options = new BwCliOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<BwProcessRunner>();
        services.AddSingleton<IVaultClient, BwCliVaultClient>();
        services.AddSingleton<IVaultSession, BwVaultSession>();
        return services;
    }

    /// <summary>
    /// Registers the <c>bw serve</c> adapter: one child process exposing the local Vault
    /// Management API, with HTTP for every call.
    /// </summary>
    /// <remarks>
    /// Prefer this in any host that outlives a single command — the process-per-call adapter
    /// pays a fresh Node start-up every time. The server is started lazily on first use and
    /// stopped when the provider is disposed. Note the API is unauthenticated; see
    /// <see cref="BwServeProcess"/>.
    /// </remarks>
    public static IServiceCollection AddBitwardenServe(
        this IServiceCollection services,
        Action<BwServeOptions>? configure = null)
    {
        var options = new BwServeOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<BwServeProcess>();

        // Registration must not start anything: the first service is resolved on the UI thread,
        // and blocking there to await start-up deadlocks the app. BwServeConnection starts the
        // server on first awaited use instead.
        services.AddSingleton<BwServeConnection>();
        services.AddSingleton<BwServeVaultClient>();
        services.AddSingleton<IVaultClient>(p => p.GetRequiredService<BwServeVaultClient>());
        services.AddSingleton<IVaultSession>(p => p.GetRequiredService<BwServeVaultClient>());
        return services;
    }
}
