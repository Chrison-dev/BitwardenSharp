using System.ComponentModel;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Application.Duplicates;
using BitwardenSharp.Application.Merging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BitwardenSharp.Cli.Commands;

public sealed class MergeSettings : CommandSettings
{
    [CommandArgument(0, "<GROUP_IDS>")]
    [Description("Group ids from `bwsharp scan`, e.g. EXACT-001 RELATED-003")]
    public string[] GroupIds { get; init; } = [];

    [CommandOption("--apply")]
    [Description("Actually write. Without this the merge is computed and shown but nothing changes")]
    public bool Apply { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation prompt (for non-interactive use)")]
    public bool AssumeYes { get; init; }
}

/// <summary>
/// Merges approved duplicate groups.
/// </summary>
/// <remarks>
/// Dry run is the default and <c>--apply</c> is the only way to write, because the losing side of
/// a merge is deleted. Deletions are soft, so Bitwarden's trash holds them for 30 days.
/// </remarks>
public sealed class MergeCommand(IVaultClient vault, DuplicateScanner scanner, MergeExecutor executor)
    : AsyncCommand<MergeSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, MergeSettings settings, CancellationToken cancellationToken)
    {
        var status = await vault.GetStatusAsync(cancellationToken);
        if (!status.IsUnlocked)
        {
            AnsiConsole.MarkupLine($"[red]Vault is {status.Status}.[/] Unlock it first.");
            return 1;
        }

        // Re-scan rather than trusting ids from an older run: group ids are only stable within
        // one scan of one vault state.
        await vault.SyncAsync(cancellationToken);
        var result = scanner.Scan(await vault.GetItemsAsync(cancellationToken));

        var wanted = settings.GroupIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groups = result.Groups.Where(g => wanted.Contains(g.Id)).ToList();

        var missing = wanted.Except(groups.Select(g => g.Id), StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
        {
            AnsiConsole.MarkupLine(
                $"[red]Not found in the current scan:[/] {string.Join(", ", missing)}\n"
                + "[grey]Group ids change between scans — re-run `bwsharp scan`.[/]");
            return 1;
        }

        var refused = groups.Where(g => !g.CanMerge).ToList();
        foreach (var group in refused)
            AnsiConsole.MarkupLine(
                $"[yellow]{group.Id} will be skipped:[/] "
                + Markup.Escape(string.Join("; ",
                    group.Warnings.Where(w => w.IsBlocking).Select(w => w.Message)
                        .DefaultIfEmpty($"{group.Category} is review-only"))));

        var actionable = groups.Where(g => g.CanMerge).ToList();
        if (actionable.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nothing to merge.[/]");
            return 1;
        }

        var deletions = actionable.Sum(g => g.Losers.Count());
        AnsiConsole.MarkupLine(
            $"\n[bold]{(settings.Apply ? "APPLY" : "DRY RUN")}[/] — "
            + $"{actionable.Count} group(s), {deletions} deletion(s)\n");

        if (settings.Apply && !settings.AssumeYes)
        {
            foreach (var group in actionable)
                AnsiConsole.MarkupLine(
                    $"  {group.Id}: keep [green]{Markup.Escape(group.Survivor.Name)}[/], "
                    + $"delete [red]{string.Join(", ", group.Losers.Select(l => Markup.Escape(l.Name)))}[/]");

            if (!AnsiConsole.Confirm($"\nDelete {deletions} item(s) to trash?", defaultValue: false))
            {
                AnsiConsole.MarkupLine("[yellow]Aborted. Nothing was changed.[/]");
                return 1;
            }
        }

        var failures = 0;
        foreach (var group in actionable)
        {
            var outcome = await executor.ApplyAsync(group, dryRun: !settings.Apply, cancellationToken);

            var colour = outcome.Status switch
            {
                MergeStatus.Merged => "green",
                MergeStatus.Skipped => "yellow",
                _ => "red",
            };
            AnsiConsole.MarkupLine($"[{colour}]{outcome.Status}[/] {group.Id} — {Markup.Escape(group.Key)}");

            foreach (var change in outcome.Changes)
                AnsiConsole.MarkupLine($"    [grey]{Markup.Escape(change)}[/]");

            if (outcome.Message is not null)
                AnsiConsole.MarkupLine($"    [yellow]{Markup.Escape(outcome.Message)}[/]");

            foreach (var id in outcome.DeletedItemIds)
                AnsiConsole.MarkupLine($"    [red]deleted[/] [grey]{id}[/] → trash");

            if (outcome.Status is MergeStatus.Failed or MergeStatus.VerificationFailed) failures++;
        }

        if (settings.Apply)
        {
            await vault.SyncAsync(cancellationToken);
            AnsiConsole.MarkupLine(
                "\n[grey]Deleted items are in Bitwarden's trash and restorable for 30 days.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("\n[grey]Dry run. Re-run with --apply to write.[/]");
        }

        return failures > 0 ? 1 : 0;
    }
}
