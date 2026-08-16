using BitwardenSharp.Application;
using BitwardenSharp.Cli.Commands;
using BitwardenSharp.Cli.Hosting;
using BitwardenSharp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddBitwardenSharpApplication();
services.AddBitwardenCli();

var app = new CommandApp(new TypeRegistrar(services));
app.Configure(config =>
{
    config.SetApplicationName("bwsharp");

    config.AddCommand<ScanCommand>("scan")
        .WithDescription("Find duplicate logins in the vault. Read-only.")
        .WithExample("scan")
        .WithExample("scan", "--category", "ExactDuplicate", "--detail");

    config.AddCommand<MergeCommand>("merge")
        .WithDescription("Merge approved duplicate groups. Dry run unless --apply is given.")
        .WithExample("merge", "EXACT-001", "EXACT-002")
        .WithExample("merge", "EXACT-001", "--apply");
});

return await app.RunAsync(args);
