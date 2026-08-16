using BitwardenSharp.Application.Duplicates;
using BitwardenSharp.Application.Merging;
using Microsoft.Extensions.DependencyInjection;

namespace BitwardenSharp.Application;

/// <summary>Registers the vault operations. Requires an IVaultClient from Infrastructure.</summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddBitwardenSharpApplication(this IServiceCollection services)
    {
        services.AddSingleton<DuplicateScanner>();
        services.AddSingleton<MergeExecutor>();
        return services;
    }
}
