using Fallout.Common.CI.GitHubActions;

/// <summary>
/// The CI/CD definition — every workflow this repository has, declared as an attribute.
/// </summary>
/// <remarks>
/// <para>
/// <b>`.github/workflows/*.yml` is GENERATED from what follows.</b> Hand-editing the YAML is
/// silently undone by the next generation. Change an attribute, then regenerate:
/// <c>dotnet fallout --generate-configuration GitHubActions_&lt;name&gt; --host GitHubActions</c>
/// (or just build the <c>_build</c> project, which regenerates all of them).
/// </para>
/// <para>
/// The build is defined in C#, not in YAML. A workflow provisions a runner and routes a channel;
/// every step that does something invokes a Fallout target — so the same commands behave
/// identically on a laptop and on a runner.
/// </para>
/// <para>The branch model is GitFlow, matching the other Chrison-dev repositories.</para>
/// </remarks>

// ── The gate ──────────────────────────────────────────────────────────────────
//
// Runs on every PR into a long-lived branch, and on every push to the two permanent ones.
// Feature branches build nothing until a PR is opened.
//
// Deliberately NO path exclusions: the job name is the required status check, so a docs-only
// PR filtered out here would wait forever on a check that never fires.
[GitHubActions(
    "build",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    OnPushBranches = new[] { DevelopBranch, MainBranch },
    OnPullRequestBranches = new[]
    {
        DevelopBranch, MainBranch, ReleaseBranchPattern, HotfixBranchPattern, SupportBranchPattern,
    },
    InvokedTargets = new[] { nameof(Test) })]

// ── Publish ───────────────────────────────────────────────────────────────────
//
// The three libraries and the `bwsharp` dotnet tool go to nuget.org on a release tag.
// Tag-triggered rather than push-triggered: a version is published because it was tagged,
// never because something landed on a branch.
[GitHubActions(
    "publish",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    OnPushTags = new[] { ReleaseTagPattern },
    InvokedTargets = new[] { nameof(Test) },
    ImportSecrets = new[] { nameof(NuGetApiKey) })]
partial class Build
{
    /// <summary>Integration branch; everything lands here first.</summary>
    const string DevelopBranch = "develop";

    /// <summary>The trunk. Only release and hotfix merges reach it.</summary>
    const string MainBranch = "main";

    /// <summary>Short-lived stabilisation window cut from <see cref="DevelopBranch"/>.</summary>
    const string ReleaseBranchPattern = "release/*";

    /// <summary>Short-lived urgent production fix cut from <see cref="MainBranch"/>.</summary>
    const string HotfixBranchPattern = "hotfix/*";

    /// <summary>Long-lived maintenance line for a release <see cref="MainBranch"/> has moved past.</summary>
    const string SupportBranchPattern = "support/*";

    /// <summary>Release tags are <c>v1.2.3</c>; publishing is driven by these, not by branches.</summary>
    const string ReleaseTagPattern = "v*";

    [Secret] readonly string NuGetApiKey;
}
