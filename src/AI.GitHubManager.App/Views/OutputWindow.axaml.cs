using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.App.ViewModels;

namespace AI.GitHubManager.App.Views;

/// <summary>
/// Separate, freely resizable output/diagnostics window (Teil: "Ausgabe /
/// Fehleranalyse … separat öffnen"). Mirrors the main window's Log property
/// live (subscribes to PropertyChanged rather than taking a one-time
/// snapshot), so it keeps showing new output while the user works. Adds
/// what the compact in-window box can't: free resizing, a full-text search/
/// filter box, a word-wrap toggle, and saving to a .txt file with a
/// timestamp header.
/// </summary>
public partial class OutputWindow : Window
{
    private readonly MainWindowViewModel? _vm;

    public OutputWindow() : this(null) { }

    public OutputWindow(MainWindowViewModel? vm)
    {
        _vm = vm;
        InitializeComponent();
        ApplyContent();

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;

        Closed += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        };

        RefreshDisplayedText();
    }

    private void ApplyContent()
    {
        Title = L.T("Ausgabe / Fehleranalyse", "Output / Diagnostics");
        TitleText.Text = L.T("Ausgabe / Fehleranalyse", "Output / Diagnostics");
        SearchBox.Watermark = L.T("Im Text suchen…", "Search text…");
        WrapCheckBox.Content = L.T("Zeilenumbruch", "Word wrap");
        CopyButton.Content = L.T("Kopieren", "Copy");
        ClearButton.Content = L.T("Leeren", "Clear");
        SaveButton.Content = L.T("Speichern unter…", "Save as…");
        CloseButton.Content = L.T("Schließen", "Close");
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(MainWindowViewModel.Log))
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshDisplayedText);
    }

    private void RefreshDisplayedText()
    {
        var fullText = _vm?.Log ?? string.Empty;
        var query = SearchBox.Text;

        if (string.IsNullOrWhiteSpace(query))
        {
            ContentBox.Text = fullText;
            MatchCountText.Text = string.Empty;
            return;
        }

        // Simple, honest line-filter search — no fabricated match count,
        // no fuzzy matching. Shows only lines containing the search text
        // (case-insensitive), preserving original line order.
        var lines = fullText.Split('\n');
        var matches = lines.Where(l => l.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        ContentBox.Text = string.Join('\n', matches);
        MatchCountText.Text = L.T($"{matches.Length} von {lines.Length} Zeilen enthalten \"{query}\"",
                                   $"{matches.Length} of {lines.Length} lines contain \"{query}\"");
    }

    private void OnSearchChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e) => RefreshDisplayedText();

    private void OnWrapChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ContentBox.TextWrapping = WrapCheckBox.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap;

    private async void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(_vm?.Log ?? string.Empty);
    }

    private void OnClearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm?.ClearLogCommand.Execute(null);
    }

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L.T("Ausgabe speichern", "Save output"),
            SuggestedFileName = $"ai-github-manager-output-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(L.T("Textdatei", "Text file")) { Patterns = new[] { "*.txt", "*.log" } }
            }
        });
        if (file is null) return;

        var header = L.T($"Exportiert am {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{new string('-', 40)}\n\n",
                          $"Exported on {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{new string('-', 40)}\n\n");
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(header + (_vm?.Log ?? string.Empty));
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
