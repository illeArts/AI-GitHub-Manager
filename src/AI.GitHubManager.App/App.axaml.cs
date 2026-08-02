using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.App.ViewModels;
using AI.GitHubManager.App.Views;

namespace AI.GitHubManager.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var vm = new MainWindowViewModel();
            window.DataContext = vm;
            desktop.MainWindow = window;

            // On macOS, use the native top-of-screen menu bar instead of the
            // in-window Menu control. The in-window menu is hidden via
            // MainWindow.axaml.cs after InitializeComponent.
            if (OperatingSystem.IsMacOS())
                SetupMacNativeMenu(window, desktop, vm);
        }
        base.OnFrameworkInitializationCompleted();
    }

    // ── macOS native menu bar ─────────────────────────────────────────────────
    //
    // Builds a proper macOS application menu structure:
    //   [AI GitHub Manager]      — macOS replaces this label with the real app
    //     Über AI GitHub Manager      name automatically; it must be the FIRST
    //     ────────────────            top-level item, or macOS falls back to a
    //     Einstellungen…      ⌘,      bare "Quit" menu and loses About/⌘,.
    //     ────────────────
    //     AI GitHub Manager beenden  ⌘Q
    //   [Hilfe]
    //     Hilfe / Befehle            ⌘?
    //     GitHub-Projekt öffnen
    //
    // Wrapped in try/catch and logged directly into the main window's visible
    // output — this setup previously failed silently (see docs/testing) with
    // no visible error, which made it impossible to tell from a screenshot
    // alone whether the code even ran. A startup diagnostic block is written
    // to the Log so real-machine testing gives a concrete artifact instead of
    // "still doesn't work" screenshots.
    private static void SetupMacNativeMenu(MainWindow owner, IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel vm)
    {
        var diagnostics = new StringBuilder();
        diagnostics.AppendLine("── macOS-Menü-Diagnose (Startup) ──────────────────────");
        diagnostics.AppendLine($"Application.Current-Typ: {Current?.GetType().FullName ?? "null"}");
        diagnostics.AppendLine($"UseAvaloniaNative aktiv (Program.cs-Flag): {Program.UsedAvaloniaNativeMacBranch}");
        diagnostics.AppendLine($"Betriebssystem: {RuntimeInformation.OSDescription}");
        diagnostics.AppendLine($"Prozessarchitektur: {RuntimeInformation.ProcessArchitecture}");

        var asm = Assembly.GetExecutingAssembly();
        // Assembly.Location is always empty for single-file publishes (which
        // is what verify-macos-app-bundle.sh and the release workflow both
        // use) — AppContext.BaseDirectory is the reliable equivalent there.
        diagnostics.AppendLine($"Basisverzeichnis: {AppContext.BaseDirectory}");
        var commitHash = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GitCommitHash")?.Value ?? "unknown";
        diagnostics.AppendLine($"Git-Commit im Build: {commitHash}");

        try
        {
            // Setting Name explicitly: per Avalonia docs, when NOT running from
            // a bundle the app menu header comes from Application.Name; when
            // running from a bundle, Info.plist's CFBundleName normally takes
            // precedence instead. Setting both leaves no gap between the two
            // code paths.
            Current!.Name = "AI GitHub Manager";
            diagnostics.AppendLine($"Application.Name gesetzt auf: \"{Current!.Name}\"");

            var settings = AppSettingsService.Load();

            // ── App menu (first item — macOS renames its header to the app name) ──
            var aboutItem = new NativeMenuItem(L.T("Über AI GitHub Manager", "About AI GitHub Manager"));
            aboutItem.Click += (_, _) => new AboutWindow().ShowDialog(owner);

            var settingsItem = new NativeMenuItem(L.T("Einstellungen…", "Settings…"))
            {
                Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta)
            };
            settingsItem.Click += (_, _) => new SettingsWindow(settings).ShowDialog(owner);

            var quitItem = new NativeMenuItem(L.T("AI GitHub Manager beenden", "Quit AI GitHub Manager"))
            {
                Gesture = new KeyGesture(Key.Q, KeyModifiers.Meta)
            };
            quitItem.Click += (_, _) => desktop.Shutdown();

            var appSubMenu = new NativeMenu();
            appSubMenu.Add(aboutItem);
            appSubMenu.Add(new NativeMenuItemSeparator());
            appSubMenu.Add(settingsItem);
            appSubMenu.Add(new NativeMenuItemSeparator());
            appSubMenu.Add(quitItem);

            var appMenu = new NativeMenuItem("AI GitHub Manager") { Menu = appSubMenu };

            // ── Help menu ─────────────────────────────────────────────────────────
            var helpItem = new NativeMenuItem(L.T("Hilfe / Befehle", "Help / Commands"))
            {
                Gesture = new KeyGesture(Key.OemQuestion, KeyModifiers.Meta)
            };
            helpItem.Click += (_, _) => new HelpWindow().ShowDialog(owner);

            var gitHubItem = new NativeMenuItem(L.T("GitHub-Projekt öffnen", "Open GitHub project"));
            gitHubItem.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/illeArts/AI-GitHub-Manager") { UseShellExecute = true }); }
                catch { /* best-effort — never crash the app over a browser launch failure */ }
            };

            var helpSubMenu = new NativeMenu();
            helpSubMenu.Add(helpItem);
            helpSubMenu.Add(gitHubItem);

            var helpMenu = new NativeMenuItem(L.T("Hilfe", "Help")) { Menu = helpSubMenu };

            // ── Root menu ─────────────────────────────────────────────────────────
            var menu = new NativeMenu();
            menu.Add(appMenu);
            menu.Add(helpMenu);
            NativeMenu.SetMenu(Current!, menu);

            diagnostics.AppendLine($"NativeMenu.SetMenu ausgeführt — Einträge im Root-Menü: {menu.Count()}");
            diagnostics.AppendLine($"Root-Menü-Einträge: {string.Join(", ", menu.Select(i => (i as NativeMenuItem)?.Header ?? i.GetType().Name))}");
            diagnostics.AppendLine("Status: kein Fehler beim Aufbau des nativen Menüs.");

            // Keep native menu labels in sync with language changes (the keyboard
            // gestures themselves are language-independent and never change).
            L.Changed += () =>
            {
                aboutItem.Header    = L.T("Über AI GitHub Manager",        "About AI GitHub Manager");
                settingsItem.Header = L.T("Einstellungen…",                "Settings…");
                quitItem.Header     = L.T("AI GitHub Manager beenden",     "Quit AI GitHub Manager");
                helpMenu.Header     = L.T("Hilfe",                         "Help");
                helpItem.Header     = L.T("Hilfe / Befehle",               "Help / Commands");
                gitHubItem.Header   = L.T("GitHub-Projekt öffnen",         "Open GitHub project");
            };
        }
        catch (Exception ex)
        {
            diagnostics.AppendLine($"FEHLER beim Aufbau des nativen macOS-Menüs: {ex.GetType().FullName}: {ex.Message}");
            diagnostics.AppendLine(ex.StackTrace ?? "(kein Stacktrace)");
        }

        diagnostics.AppendLine("────────────────────────────────────────────────────────");
        vm.Log = diagnostics.ToString().TrimEnd() + "\n\n" + vm.Log;
    }
}
