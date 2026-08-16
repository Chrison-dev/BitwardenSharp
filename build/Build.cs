using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tools.DotNet;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

/// <summary>
/// Fallout build for BitwardenSharp — the targets. The CI/CD definition that invokes them lives in
/// <c>Build.CI.GitHubActions.cs</c>, from which every <c>.github/workflows/*.yml</c> is GENERATED;
/// never hand-edit those.
///
/// Tests that touch a real vault through the `bw` CLI are tagged [Trait("Category","Live")] and
/// EXCLUDED from the default Test run — they need an unlocked vault and mutate real data.
/// Run them deliberately: ./build.sh TestLive
/// </summary>
partial class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Test);

    AbsolutePath SolutionFile => RootDirectory / "BitwardenSharp.slnx";

    Target Compile => _ => _
        .Description("Build the solution")
        .Executes(() => DotNetBuild(_ => _
            .SetProjectFile(SolutionFile)
            .SetConfiguration("Release")));

    Target Test => _ => _
        .Description("Run the unit and architecture tests (excludes live vault tests)")
        .DependsOn(Compile)
        .Executes(() => DotNetTest(_ => _
            .SetProjectFile(SolutionFile)
            .SetConfiguration("Release")
            .SetFilter("Category!=Live")
            .EnableNoBuild()));

    Target TestLive => _ => _
        .Description("Run ONLY the tests that drive a real `bw` CLI against an unlocked vault")
        .DependsOn(Compile)
        .Executes(() => DotNetTest(_ => _
            .SetProjectFile(SolutionFile)
            .SetConfiguration("Release")
            .SetFilter("Category=Live")
            .EnableNoBuild()));
}
