using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Infrastructure.Bw;
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
        return services;
    }
}
