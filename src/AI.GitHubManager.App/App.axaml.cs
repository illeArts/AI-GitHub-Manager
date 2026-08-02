using System.Diagnostics;
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
    public App()
    {
        // Avalonia.Native reads the application menu while its platform is being
        // set up.  It is therefore essential that this menu already exists in
        // the App constructor; attaching one to Application.Current later does
        // not notify an application-level native-menu exporter.
        if (OperatingSystem.IsMacOS())
            NativeMenu.SetMenu(this, CreateMacApplicationMenu());
    }

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
                SetupMacNativeMenu(window);
        }
        base.OnFrameworkInitializationCompleted();
    }

    // ── macOS native menu bar ─────────────────────────────────────────────────
    //
    // Builds the macOS menu structure using the two levels expected by
    // Avalonia.Native:
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
    // The application menu is attached in the constructor (before the native
    // platform exporter is initialized).  The window-level menu below supplies
    // the additional root entries in the system menu bar.
    private static void SetupMacNativeMenu(MainWindow owner)
    {
        var helpItem = new NativeMenuItem(L.T("Hilfe öffnen", "Open Help"))
        {
            Gesture = new KeyGesture(Key.OemQuestion, KeyModifiers.Meta)
        };
        helpItem.Click += (_, _) => new HelpWindow().ShowDialog(owner);

        var gitHubItem = CreateExternalLinkItem("GitHub-Projekt öffnen", "Open GitHub project", "https://github.com/illeArts/AI-GitHub-Manager");
        var releaseNotesItem = CreateExternalLinkItem("Release Notes öffnen", "Open release notes", "https://github.com/illeArts/AI-GitHub-Manager/releases");
        var helpMenu = new NativeMenuItem(L.T("Hilfe", "Help"))
        {
            Menu = new NativeMenu { helpItem, gitHubItem, releaseNotesItem }
        };

        NativeMenu.SetMenu(owner, new NativeMenu { helpMenu });

        L.Changed += () =>
        {
            helpMenu.Header = L.T("Hilfe", "Help");
            helpItem.Header = L.T("Hilfe öffnen", "Open Help");
            gitHubItem.Header = L.T("GitHub-Projekt öffnen", "Open GitHub project");
            releaseNotesItem.Header = L.T("Release Notes öffnen", "Open release notes");
        };
    }

    private static NativeMenu CreateMacApplicationMenu()
    {
        var settings = AppSettingsService.Load();
        var aboutItem = new NativeMenuItem(L.T("Über AI GitHub Manager", "About AI GitHub Manager"));
        aboutItem.Click += (_, _) => ShowAbout();

        var settingsItem = new NativeMenuItem(L.T("Einstellungen…", "Settings…"))
        {
            Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta)
        };
        settingsItem.Click += (_, _) => ShowSettings(settings);

        L.Changed += () =>
        {
            aboutItem.Header = L.T("Über AI GitHub Manager", "About AI GitHub Manager");
            settingsItem.Header = L.T("Einstellungen…", "Settings…");
        };

        return new NativeMenu { aboutItem, new NativeMenuItemSeparator(), settingsItem };
    }

    private static MainWindow? GetMainWindow() =>
        (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

    private static void ShowAbout()
    {
        var dialog = new AboutWindow();
        if (GetMainWindow() is { } owner) dialog.ShowDialog(owner);
        else dialog.Show();
    }

    private static void ShowSettings(AppSettingsService settings)
    {
        var dialog = new SettingsWindow(settings);
        if (GetMainWindow() is { } owner) dialog.ShowDialog(owner);
        else dialog.Show();
    }

    private static NativeMenuItem CreateExternalLinkItem(string de, string en, string url)
    {
        var item = new NativeMenuItem(L.T(de, en));
        item.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* Opening a browser is best effort. */ }
        };
        return item;
    }
}
