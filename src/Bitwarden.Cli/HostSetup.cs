using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Runtime.InteropServices;
using Bitwarden.Application;
using Bitwarden.Infrastructure;

namespace Bitwarden.Cli
{
    public static class HostSetup
    {
        public static IHostBuilder ConfigureBitwardenHost(this IHostBuilder builder)
        {
            return builder.ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddEnvironmentVariables();
                })
                .ConfigureServices((ctx, services) =>
                {
                    // If CI/tests set override env var, use in-memory secret store
                    var useInMemory = Environment.GetEnvironmentVariable("BITWARDEN_INMEMORY_SECRETS");
                    if (!string.IsNullOrEmpty(useInMemory) && useInMemory == "1")
                    {
                        services.AddSingleton<ISecretStore, InMemorySecretStore>();
                    }
                    else
                    {
                        // Register platform-specific secret store
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            services.AddSingleton<ISecretStore, DpapiSecretStore>();
                        else
                            services.AddSingleton<ISecretStore, OsKeyringSecretStore>();
                    }

                    // Register Config as instance-based singleton using DI
                    services.AddSingleton<Config>(provider =>
                    {
                        var store = provider.GetRequiredService<ISecretStore>();
                        var cfg = new Config(store);
                        return cfg;
                    });
                    // Register BwRunner implementation
                    // core infra services
                    services.AddSingleton<Bitwarden.Application.IProcessRunner, Bitwarden.Infrastructure.ProcessRunner>();
                    services.AddSingleton<Bitwarden.Application.IConsole, Bitwarden.Infrastructure.ConsoleWrapper>();
                    services.AddSingleton<Bitwarden.Application.IBwRunner, Bitwarden.Infrastructure.BwRunner>();
                })
                .UseSerilog((ctx, services, loggerConfig) =>
                {
                    loggerConfig
                        .Enrich.FromLogContext()
                        .WriteTo.Console();
                });
        }
    }
}
