using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

            // On macOS, use the native menu bar instead of the in-window Menu control.
            // The in-window menu is hidden via MainWindow.axaml.cs after InitializeComponent.
            if (OperatingSystem.IsMacOS())
                SetupMacNativeMenu(window);
        }
        base.OnFrameworkInitializationCompleted();
    }

    // ── macOS native menu bar ─────────────────────────────────────────────────

    private static void SetupMacNativeMenu(MainWindow owner)
    {
        var settings = AppSettingsService.Load();

        // ── Settings ──────────────────────────────────────────────
        var settingsItem = new NativeMenuItem(L.T("Einstellungen", "Settings"));
        settingsItem.Click += (_, _) => new SettingsWindow(settings).ShowDialog(owner);

        // ── Help sub-menu ─────────────────────────────────────────
        var helpItem = new NativeMenuItem(L.T("Hilfe / Befehle", "Help / Commands"));
        helpItem.Click += (_, _) => new HelpWindow().ShowDialog(owner);

        var aboutItem = new NativeMenuItem(L.T("Über AI GitHub Manager", "About AI GitHub Manager"));
        aboutItem.Click += (_, _) => new AboutWindow().ShowDialog(owner);

        var helpSubMenu = new NativeMenu();
        helpSubMenu.Add(helpItem);
        helpSubMenu.Add(new NativeMenuItemSeparator());
        helpSubMenu.Add(aboutItem);

        var helpMenu = new NativeMenuItem(L.T("Hilfe", "Help")) { Menu = helpSubMenu };

        // ── Root menu ─────────────────────────────────────────────
        var menu = new NativeMenu();
        menu.Add(settingsItem);
        menu.Add(helpMenu);
        NativeMenu.SetMenu(Current!, menu);

        // Keep native menu labels in sync with language changes
        L.Changed += () =>
        {
            settingsItem.Header = L.T("Einstellungen", "Settings");
            helpMenu.Header     = L.T("Hilfe",         "Help");
            helpItem.Header     = L.T("Hilfe / Befehle",           "Help / Commands");
            aboutItem.Header    = L.T("Über AI GitHub Manager",    "About AI GitHub Manager");
        };
    }
}
