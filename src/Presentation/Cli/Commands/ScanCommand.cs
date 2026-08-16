using System.ComponentModel;
using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Application.Duplicates;
using BitwardenSharp.Domain.Duplicates;
using BitwardenSharp.Infrastructure.Bw;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BitwardenSharp.Cli.Commands;

public sealed class ScanSettings : CommandSettings
{
    [CommandOption("-c|--category <CATEGORY>")]
    [Description("Show only one category, e.g. ExactDuplicate")]
    public string? Category { get; init; }

    [CommandOption("--detail")]
    [Description("List every member of every group, not just the summary")]
    public bool Detail { get; init; }

    [CommandOption("--no-sync")]
    [Description("Skip the server sync and scan the local cache")]
    public bool NoSync { get; init; }

    [CommandOption("--from <FILE>")]
    [Description("Scan a saved `bw list items` dump instead of a live vault. Read-only.")]
    public string? FromFile { get; init; }
}

/// <summary>Reads the vault and reports duplicate groups. Never writes.</summary>
public sealed class ScanCommand(IVaultClient vault, DuplicateScanner scanner)
    : AsyncCommand<ScanSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ScanSettings settings, CancellationToken cancellationToken)
    {
        // A file snapshot needs no unlock, and never reaches the live vault.
        var source = settings.FromFile is null ? vault : new JsonFileVaultClient(settings.FromFile);

        var status = await source.GetStatusAsync(cancellationToken);
        if (!status.IsUnlocked)
        {
            AnsiConsole.MarkupLine(
                $"[red]Vault is {status.Status}.[/] Unlock it and export the session:\n"
                + "  [grey]export BW_SESSION=$(bw unlock --raw)[/]");
            return 1;
        }

        DuplicateScanResult result = default!;
        await AnsiConsole.Status().StartAsync("Reading vault…", async ctx =>
        {
            if (!settings.NoSync && settings.FromFile is null)
            {
                ctx.Status("Syncing…");
                await source.SyncAsync(cancellationToken);
            }

            ctx.Status("Reading items…");
            var items = await source.GetItemsAsync(cancellationToken);

            ctx.Status("Scanning for duplicates…");
            result = scanner.Scan(items);
        });

        AnsiConsole.MarkupLine(
            $"\n[bold]{result.TotalItems}[/] items, [bold]{result.LoginCount}[/] logins, "
            + $"[bold]{result.Groups.Count}[/] groups, "
            + $"[bold green]{result.MergeableDeletions}[/] deletions available\n");

        var summary = new Table().Border(TableBorder.Rounded);
        summary.AddColumns("Category", "Groups", "Deletions", "Disposition");
        foreach (var category in Enum.GetValues<DuplicateCategory>())
        {
            var groups = result.Groups.Where(g => g.Category == category).ToList();
            if (groups.Count == 0) continue;
            var mergeable = category.Disposition() == MergeDisposition.Mergeable;
            summary.AddRow(
                category.ToString(),
                groups.Count.ToString(),
                mergeable ? groups.Where(g => g.CanMerge).Sum(g => g.Losers.Count()).ToString() : "—",
                mergeable ? "[green]mergeable[/]" : "[yellow]review only[/]");
        }
        AnsiConsole.Write(summary);

        var shown = result.Groups.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(settings.Category))
        {
            if (!Enum.TryParse<DuplicateCategory>(settings.Category, ignoreCase: true, out var wanted))
            {
                AnsiConsole.MarkupLine($"[red]Unknown category '{settings.Category}'.[/]");
                return 1;
            }
            shown = shown.Where(g => g.Category == wanted);
        }

        foreach (var group in shown)
        {
            var blocked = group.Warnings.Any(w => w.IsBlocking);
            var marker = group.CanMerge ? "[green]●[/]" : blocked ? "[red]●[/]" : "[yellow]●[/]";
            AnsiConsole.MarkupLine(
                $"\n{marker} [bold]{group.Id}[/]  [grey]{group.Category}[/]  {Markup.Escape(group.Key)}");
            AnsiConsole.MarkupLine($"    keep [green]{Markup.Escape(group.Survivor.Name)}[/]");

            foreach (var loser in group.Losers)
                AnsiConsole.MarkupLine($"    drop [red]{Markup.Escape(loser.Name)}[/]");

            if (settings.Detail)
                foreach (var uri in group.Members.SelectMany(m => m.Uris).Select(u => u.Uri).Distinct())
                    AnsiConsole.MarkupLine($"      [grey]{Markup.Escape(uri)}[/]");

            foreach (var warning in group.Warnings)
                AnsiConsole.MarkupLine(
                    $"    {(warning.IsBlocking ? "[red]blocked[/]" : "[yellow]warn[/]")}: "
                    + Markup.Escape(warning.Message));
        }

        AnsiConsole.MarkupLine(
            "\n[grey]Merge with:[/] bwsharp merge <group-id…>   [grey](add --apply to write)[/]");
        return 0;
    }
}
