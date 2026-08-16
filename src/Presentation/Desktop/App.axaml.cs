using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BitwardenSharp.Application;
using BitwardenSharp.Desktop.ViewModels;
using BitwardenSharp.Desktop.Views;
using BitwardenSharp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BitwardenSharp.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddBitwardenSharpApplication();
            // A GUI outlives every call, so the long-lived server beats process-per-call.
            services.AddBitwardenServe();
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<UnlockViewModel>();
            services.AddTransient<VaultViewModel>();

            var provider = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainWindowViewModel>(),
            };

            // The session key dies with the process; lock explicitly anyway so the vault is not
            // left unlocked for the next `bw` invocation from any other tool.
            desktop.ShutdownRequested += (_, _) =>
                provider.GetRequiredService<MainWindowViewModel>().OnShutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
