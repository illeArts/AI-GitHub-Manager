using Avalonia;

namespace AI.GitHubManager.App;

internal static class Program
{
    /// <summary>
    /// True only when BuildAvaloniaApp() took the explicit UseAvaloniaNative()
    /// branch (macOS). Read by App.axaml.cs's startup diagnostics so a real
    /// run can prove which windowing backend actually got selected, instead
    /// of assuming it from the source code alone.
    /// </summary>
    public static bool UsedAvaloniaNativeMacBranch { get; private set; }

    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace();

        // UsePlatformDetect() is supposed to pick Avalonia.Native on macOS
        // automatically via reflection (Type.GetType("Avalonia.Native...")),
        // but that reflection-based lookup is unreliable in a
        // PublishSingleFile self-contained build: real-machine testing
        // showed the native macOS backend (and with it, the entire
        // NativeMenu/NSApplication integration — About/Settings/Hilfe/the
        // real app name in the system menu bar) silently failing to
        // activate even with the Avalonia.Native package referenced and
        // restored. Calling UseAvaloniaNative() directly is a real,
        // statically-resolved method call the single-file bundler can see
        // and embed correctly, instead of a runtime assembly-load-by-name
        // that can silently miss inside the bundle.
        if (OperatingSystem.IsMacOS())
        {
            UsedAvaloniaNativeMacBranch = true;
            return builder.UseAvaloniaNative().UseSkia();
        }

        return builder.UsePlatformDetect();
    }
}
