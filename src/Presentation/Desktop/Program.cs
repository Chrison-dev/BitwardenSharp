using Avalonia;

namespace BitwardenSharp.Desktop;

internal static class Program
{
    // Avalonia requires this to be called before anything touches the toolkit; keep it free of
    // application logic so a failure here is unambiguously a platform-init failure.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
