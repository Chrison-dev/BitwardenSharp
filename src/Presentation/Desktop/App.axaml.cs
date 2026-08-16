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
            //
            // ShutdownRequested is synchronous, so the only way to await here without blocking
            // the UI thread is to veto the first request, do the work asynchronously, and then
            // ask for shutdown again. Blocking instead -- even via Task.Run().Wait() -- is what
            // froze this app once already.
            var shuttingDown = false;
            desktop.ShutdownRequested += async (_, e) =>
            {
                if (shuttingDown) return;

                shuttingDown = true;
                e.Cancel = true;

                try
                {
                    await provider.GetRequiredService<MainWindowViewModel>().ShutdownAsync();
                    await provider.DisposeAsync();
                }
                catch
                {
                    // async void: an escaping exception here would crash on the way out rather
                    // than exiting. Nothing left to salvage at this point anyway.
                }

                desktop.Shutdown();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
