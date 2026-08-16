using ArchUnitNET.Loader;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace BitwardenSharp.Architecture.Tests;

/// <summary>
/// Enforces the hexagon: Domain ← Application ← Infrastructure, with Presentation as the outer
/// composition layer. Dependencies may only point inward.
/// </summary>
public class HexagonalArchitectureSpecs
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Domain.Vault.VaultItem).Assembly,
            typeof(Application.ApplicationServiceExtensions).Assembly,
            typeof(Infrastructure.InfrastructureServiceExtensions).Assembly,
            typeof(Cli.Commands.ScanCommand).Assembly,
            typeof(Desktop.ViewModels.VaultViewModel).Assembly)
        .Build();

    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        Types().That().ResideInNamespaceMatching(@"BitwardenSharp\.Domain")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"BitwardenSharp\.(Application|Infrastructure|Cli|Desktop)")
            .Because("Domain is the core of the hexagon and must depend on nothing else.")
            .Check(Architecture);
    }

    [Fact]
    public void Application_depends_only_on_domain()
    {
        Types().That().ResideInNamespaceMatching(@"BitwardenSharp\.Application")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"BitwardenSharp\.(Infrastructure|Cli|Desktop)")
            .Because("Application owns the ports; adapters depend on it, never the reverse.")
            .Check(Architecture);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_presentation()
    {
        Types().That().ResideInNamespaceMatching(@"BitwardenSharp\.Infrastructure")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"BitwardenSharp\.(Cli|Desktop)")
            .Because("Infrastructure implements ports; it must not reach into a presentation host.")
            .Check(Architecture);
    }

    [Fact]
    public void Domain_does_not_depend_on_serialization()
    {
        // The wire contracts live in Infrastructure precisely so the domain model can stay free of
        // transport concerns; a JsonPropertyName appearing on a domain type means that boundary
        // has started to leak.
        Types().That().ResideInNamespaceMatching(@"BitwardenSharp\.Domain")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"System\.Text\.Json.*")
            .Because("the domain model is not a serialization contract; BwItem is.")
            .Check(Architecture);
    }

    [Fact]
    public void Only_infrastructure_starts_processes()
    {
        // Shelling out to `bw` is an adapter concern. If Application or Domain ever reaches for
        // Process directly, the port has been bypassed and the code is no longer testable
        // without a real vault.
        Types().That().ResideInNamespaceMatching(@"BitwardenSharp\.(Domain|Application)")
            .Should().NotDependOnAnyTypesThat()
            .HaveFullNameContaining("System.Diagnostics.Process")
            .Because("only Infrastructure may run the bw client.")
            .Check(Architecture);
    }
}
