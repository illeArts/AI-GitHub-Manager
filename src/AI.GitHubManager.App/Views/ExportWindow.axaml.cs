using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AI.GitHubManager.Core.Export;

namespace AI.GitHubManager.App.Views;

public partial class ExportWindow : Window
{
    private readonly ExportPlanService _planner = new();
    private readonly ProjectExportService _exporter = new();
    private readonly ZipValidationService _validator = new();
    private readonly WorktreeDetector _worktrees = new();
    private ExportPlan? _plan;
    private CancellationTokenSource? _cts;

    public ExportWindow() : this(null) { }

    public ExportWindow(string? initialSource)
    {
        InitializeComponent();
        ProfileBox.ItemsSource = ExportProfiles.All;
        ProfileBox.SelectedIndex = 1;
        SourceBox.Text = initialSource ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(initialSource)) SetDefaultDestination(initialSource);
        UpdateGitInfo();
    }

    private async void OnPickSource(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Quellordner auswählen", AllowMultiple = false });
        var path = result.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        SourceBox.Text = path; SetDefaultDestination(path); InvalidatePlan(); UpdateGitInfo();
    }

    private async void OnPickDestination(object? sender, RoutedEventArgs e)
    {
        var suggested = Path.GetFileName(DestinationBox.Text);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "ZIP-Ziel auswählen", SuggestedFileName = string.IsNullOrWhiteSpace(suggested) ? "Projekt-Clean.zip" : suggested,
            FileTypeChoices = [new FilePickerFileType("ZIP-Archiv") { Patterns = ["*.zip"] }], DefaultExtension = "zip", ShowOverwritePrompt = true
        });
        if (file is not null) { DestinationBox.Text = file.Path.LocalPath; InvalidatePlan(); }
    }

    private async void OnPreview(object? sender, RoutedEventArgs e) => await BuildPreviewAsync();

    private async Task<bool> BuildPreviewAsync()
    {
        try
        {
            StatusText.Text = "Ordner wird gescannt …";
            var profile = ProfileBox.SelectedItem as ExportProfile ?? ExportProfiles.Get(ExportProfileKind.CleanSource);
            var patterns = (CustomPatternsBox.Text ?? "").Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _plan = await _planner.CreateAsync(SourceBox.Text ?? "", DestinationBox.Text ?? "", profile, patterns);
            var sensitive = _plan.Entries.Where(x => x.IsIncluded && x.IsSensitive).Select(x => x.RelativePath).ToArray();
            SensitiveList.ItemsSource = sensitive;
            SensitiveList.SelectedItems?.Clear();
            SensitiveConfirmation.IsChecked = false;
            var large = _plan.Included.Where(x => x.Length >= 100L * 1024 * 1024).OrderByDescending(x => x.Length).ToArray();
            var sb = new StringBuilder();
            sb.AppendLine($"Enthalten:    {_plan.Included.Count:N0} Dateien · {FormatBytes(_plan.IncludedBytes)}");
            sb.AppendLine($"Ausgeschlossen: {_plan.Excluded.Count:N0} Dateien · {FormatBytes(_plan.ExcludedBytes)}");
            sb.AppendLine($"Sensibel:     {sensitive.Length:N0}"); sb.AppendLine($"Groß (≥100 MB): {large.Length:N0}");
            if (large.Length > 0) { sb.AppendLine(); sb.AppendLine("Große Dateien:"); foreach (var x in large) sb.AppendLine($"{FormatBytes(x.Length),10}  {x.RelativePath}"); }
            sb.AppendLine(); sb.AppendLine("Ausgeschlossen:"); foreach (var x in _plan.Excluded.Take(500)) sb.AppendLine($"{x.RelativePath}  [{x.ExclusionReason}]");
            PreviewText.Text = sb.ToString(); StatusText.Text = "Vorschau bereit."; return true;
        }
        catch (Exception ex) { _plan = null; StatusText.Text = ex.Message; return false; }
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (_plan is null && !await BuildPreviewAsync()) return;
        var selected = SensitiveList.SelectedItems?.Cast<string>().ToHashSet(StringComparer.Ordinal) ?? [];
        if (selected.Count > 0)
        {
            var profile = ProfileBox.SelectedItem as ExportProfile ?? ExportProfiles.Get(ExportProfileKind.CleanSource);
            var patterns = (CustomPatternsBox.Text ?? "").Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _plan = await _planner.CreateAsync(SourceBox.Text!, DestinationBox.Text!, profile, patterns, selected);
        }
        if (_plan!.Included.Any(x => x.IsSensitive) && SensitiveConfirmation.IsChecked != true)
        {
            StatusText.Text = "Bitte sensible Dateien markieren oder deren Aufnahme ausdrücklich bestätigen.";
            return;
        }
        SetBusy(true); _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<ExportProgress>(p => { Progress.Maximum = Math.Max(1, p.TotalFiles); Progress.Value = p.CompletedFiles; StatusText.Text = $"{p.CompletedFiles}/{p.TotalFiles}: {p.CurrentPath}"; });
            var result = await _exporter.ExportAsync(_plan, progress, _cts.Token);
            var validation = await _validator.ValidateAsync(_plan, _cts.Token);
            StatusText.Text = validation.Success ? $"ZIP erstellt und validiert: {result.FileCount:N0} Dateien, {FormatBytes(result.BytesWritten)}" : "Validierung fehlgeschlagen: " + string.Join("; ", validation.Errors);
            OpenFolderButton.IsEnabled = validation.Success;
        }
        catch (OperationCanceledException) { StatusText.Text = "Export abgebrochen; unvollständiges Archiv wurde entfernt."; }
        catch (Exception ex) { StatusText.Text = $"Export fehlgeschlagen: {ex.Message}"; }
        finally { SetBusy(false); _cts?.Dispose(); _cts = null; }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => _cts?.Cancel();
    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        var path = Path.GetDirectoryName(DestinationBox.Text);
        if (!string.IsNullOrWhiteSpace(path)) Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
    private void OnProfileChanged(object? sender, SelectionChangedEventArgs e) => InvalidatePlan();
    private void OnPatternsChanged(object? sender, TextChangedEventArgs e) => InvalidatePlan();
    private void InvalidatePlan() { _plan = null; if (StatusText is not null) StatusText.Text = "Eingaben geändert – Vorschau aktualisieren."; }
    private void SetDefaultDestination(string source) => DestinationBox.Text = Path.Combine(Directory.GetParent(source)?.FullName ?? source, Path.GetFileName(Path.TrimEndingDirectorySeparator(source)) + "-Clean.zip");
    private void UpdateGitInfo()
    {
        try { var info = _worktrees.Detect(SourceBox.Text ?? ""); GitInfoText.Text = info.IsGitRepository ? $"Repository: {info.RepositoryPath}\nBranch: {info.Branch}\nTyp: {(info.IsWorktree ? "Git-Worktree" : "normales Arbeitsverzeichnis")}" : info.Message; WorktreeWarning.Text = info.IsWorktree ? info.Message : ""; }
        catch (Exception ex) { GitInfoText.Text = ex.Message; }
    }
    private void SetBusy(bool busy) { ExportButton.IsEnabled = !busy; CancelButton.IsEnabled = busy; }
    private static string FormatBytes(long value) { string[] u = ["B","KB","MB","GB","TB"]; double n=value; var i=0; while(n>=1024&&i<u.Length-1){n/=1024;i++;} return $"{n:0.##} {u[i]}"; }
}
