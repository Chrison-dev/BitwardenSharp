using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace BitwardenSharp.Cli.Hosting;

/// <summary>Bridges Spectre.Console.Cli's resolver onto Microsoft.Extensions.DependencyInjection.</summary>
internal sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public void Register(Type service, Type implementation) => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) =>
        services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) =>
        services.AddSingleton(service, _ => factory());

    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());
}

internal sealed class TypeResolver(ServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type) => type is null ? null : provider.GetService(type);

    public void Dispose() => provider.Dispose();
}
