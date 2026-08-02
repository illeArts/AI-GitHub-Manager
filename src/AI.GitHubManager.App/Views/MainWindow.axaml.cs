using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.App.ViewModels;

namespace AI.GitHubManager.App.Views;

public partial class MainWindow : Window
{
    private readonly AppSettingsService _settings;

    public MainWindow() : this(AppSettingsService.Load()) { }

    public MainWindow(AppSettingsService settings)
    {
        _settings = settings;
        _settings.ApplyLanguage();  // sets L.Language before InitializeComponent
        InitializeComponent();

        // On macOS the native menu bar (App.axaml.cs) handles Einstellungen/Hilfe/Über.
        // Hide the in-window menu to avoid duplication and non-idiomatic macOS UX.
        if (OperatingSystem.IsMacOS())
            MainMenuBar.IsVisible = false;

        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.FolderPickerFunc = async () =>
            {
                var result = await StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions
                    {
                        Title         = L.T("Projektordner auswählen", "Select project folder"),
                        AllowMultiple = false
                    });
                return result.FirstOrDefault()?.Path.LocalPath;
            };

            // Teil B7/D: the actual confirmation UI for advanced/dangerous
            // operations. Kept out of the ViewModel so it stays unit-testable
            // without a real window.
            vm.ConfirmAdvancedOperationFunc = async request =>
                await new ConfirmDangerousOperationWindow(request).ShowDialog<AdvancedOperationConfirmationResult?>(this);
        }
    }

    // ── Menu handlers ─────────────────────────────────────────────────────────

    private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => new SettingsWindow(_settings).ShowDialog(this);

    private void OnHelpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => new HelpWindow().ShowDialog(this);

    private void OnAboutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => new AboutWindow().ShowDialog(this);

    private void OnExportProjectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var path = (DataContext as MainWindowViewModel)?.LocalPath;
        new ExportWindow(path).ShowDialog(this);
    }
}
