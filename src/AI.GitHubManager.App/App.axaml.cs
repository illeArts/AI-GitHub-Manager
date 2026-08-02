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
            window.DataContext = new MainWindowViewModel();
            desktop.MainWindow = window;

            // On macOS, use the native top-of-screen menu bar instead of the
            // in-window Menu control. The in-window menu is hidden via
            // MainWindow.axaml.cs after InitializeComponent.
            if (OperatingSystem.IsMacOS())
                SetupMacNativeMenu(window, desktop);
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
    //
    // Replacing the whole NativeMenu (as the previous version did, with only
    // a flat Settings + Help entry) drops macOS's automatically supplied
    // About/Quit app menu, which is why this needs to be rebuilt explicitly
    // rather than just appended to.
    private static void SetupMacNativeMenu(MainWindow owner, IClassicDesktopStyleApplicationLifetime desktop)
    {
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

        var helpSubMenu = new NativeMenu();
        helpSubMenu.Add(helpItem);

        var helpMenu = new NativeMenuItem(L.T("Hilfe", "Help")) { Menu = helpSubMenu };

        // ── Root menu ─────────────────────────────────────────────────────────
        var menu = new NativeMenu();
        menu.Add(appMenu);
        menu.Add(helpMenu);
        NativeMenu.SetMenu(Current!, menu);

        // Keep native menu labels in sync with language changes (the keyboard
        // gestures themselves are language-independent and never change).
        L.Changed += () =>
        {
            aboutItem.Header    = L.T("Über AI GitHub Manager",        "About AI GitHub Manager");
            settingsItem.Header = L.T("Einstellungen…",                "Settings…");
            quitItem.Header     = L.T("AI GitHub Manager beenden",     "Quit AI GitHub Manager");
            helpMenu.Header     = L.T("Hilfe",                         "Help");
            helpItem.Header     = L.T("Hilfe / Befehle",               "Help / Commands");
        };
    }
}
