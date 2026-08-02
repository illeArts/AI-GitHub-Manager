using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AI.GitHubManager.App.Services;
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

        ApplyStrings();
        L.Changed += ApplyStrings;
        Closed += (_, _) => L.Changed -= ApplyStrings;

        ProfileBox.ItemsSource = ExportProfiles.All;
        ProfileBox.SelectedIndex = 1;
        SourceBox.Text = initialSource ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(initialSource)) SetDefaultDestination(initialSource);
        UpdateGitInfo();
    }

    private void ApplyStrings()
    {
        Title = L.T("Projekt exportieren · Clean Export", "Export project · Clean Export");
        HeaderTitle.Text = L.T("Projekt exportieren", "Export project");
        HeaderSubtitle.Text = L.T("Clean Export · Export und Zusatzarchiv, keine Git-Synchronisierung",
                                   "Clean Export · Export and extra archive, no Git synchronization");
        SourceLabel.Text = L.T("Quelle", "Source");
        PickSourceButton.Content = L.T("Ordner wählen", "Browse...");
        DestinationLabel.Text = L.T("Zielarchiv", "Destination archive");
        PickDestinationButton.Content = L.T("Ziel wählen", "Choose destination");
        ProfileLabel.Text = L.T("Profil", "Profile");
        CustomPatternsLabel.Text = L.T("Eigene Ausschlüsse (ein Muster pro Zeile)", "Custom exclusions (one pattern per line)");
        PreviewLabel.Text = L.T("Vorschau", "Preview");
        SensitiveLabel.Text = L.T("Möglicherweise sensible Dateien (markiert = ausschließen)",
                                   "Potentially sensitive files (checked = exclude)");
        SensitiveConfirmation.Content = L.T(
            "Ich bestätige bewusst, dass nicht ausgeschlossene sensible Dateien in das ZIP aufgenommen werden.",
            "I intentionally confirm that sensitive files not excluded will be included in the ZIP.");
        RefreshPreviewButton.Content = L.T("Vorschau aktualisieren", "Refresh preview");
        CancelButton.Content = L.T("Abbrechen", "Cancel");
        ExportButton.Content = L.T("ZIP erstellen", "Create ZIP");
        OpenFolderButton.Content = L.T("Zielordner öffnen", "Open destination folder");
        if (string.IsNullOrEmpty(StatusText.Text) || StatusText.Text == "Bereit." || StatusText.Text == "Ready.")
            StatusText.Text = L.T("Bereit.", "Ready.");
    }

    private async void OnPickSource(object? sender, RoutedEventArgs e)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = L.T("Quellordner auswählen", "Select source folder"), AllowMultiple = false
        });
        var path = result.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        SourceBox.Text = path; SetDefaultDestination(path); InvalidatePlan(); UpdateGitInfo();
    }

    private async void OnPickDestination(object? sender, RoutedEventArgs e)
    {
        var suggested = Path.GetFileName(DestinationBox.Text);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L.T("ZIP-Ziel auswählen", "Choose ZIP destination"),
            SuggestedFileName = string.IsNullOrWhiteSpace(suggested) ? "Projekt-Clean.zip" : suggested,
            FileTypeChoices = [new FilePickerFileType(L.T("ZIP-Archiv", "ZIP archive")) { Patterns = ["*.zip"] }],
            DefaultExtension = "zip", ShowOverwritePrompt = true
        });
        if (file is not null) { DestinationBox.Text = file.Path.LocalPath; InvalidatePlan(); }
    }

    private async void OnPreview(object? sender, RoutedEventArgs e) => await BuildPreviewAsync();

    private async Task<bool> BuildPreviewAsync()
    {
        try
        {
            StatusText.Text = L.T("Ordner wird gescannt …", "Scanning folder …");
            var profile = ProfileBox.SelectedItem as ExportProfile ?? ExportProfiles.Get(ExportProfileKind.CleanSource);
            var patterns = (CustomPatternsBox.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _plan = await _planner.CreateAsync(SourceBox.Text ?? "", DestinationBox.Text ?? "", profile, patterns);
            var sensitive = _plan.Entries.Where(x => x.IsIncluded && x.IsSensitive).Select(x => x.RelativePath).ToArray();
            SensitiveList.ItemsSource = sensitive;
            SensitiveList.SelectedItems?.Clear();
            SensitiveConfirmation.IsChecked = false;
            var large = _plan.Included.Where(x => x.Length >= 100L * 1024 * 1024).OrderByDescending(x => x.Length).ToArray();
            var sb = new StringBuilder();
            sb.AppendLine($"{L.T("Enthalten:", "Included:"),-14}{_plan.Included.Count:N0} {L.T("Dateien", "files")} · {FormatBytes(_plan.IncludedBytes)}");
            sb.AppendLine($"{L.T("Ausgeschlossen:", "Excluded:"),-14}{_plan.Excluded.Count:N0} {L.T("Dateien", "files")} · {FormatBytes(_plan.ExcludedBytes)}");
            sb.AppendLine($"{L.T("Sensibel:", "Sensitive:"),-14}{sensitive.Length:N0}");
            sb.AppendLine($"{L.T("Groß (≥100 MB):", "Large (≥100 MB):"),-14}{large.Length:N0}");
            if (large.Length > 0)
            {
                sb.AppendLine(); sb.AppendLine(L.T("Große Dateien:", "Large files:"));
                foreach (var x in large) sb.AppendLine($"{FormatBytes(x.Length),10}  {x.RelativePath}");
            }
            sb.AppendLine(); sb.AppendLine(L.T("Ausgeschlossen:", "Excluded:"));
            foreach (var x in _plan.Excluded.Take(500)) sb.AppendLine($"{x.RelativePath}  [{x.ExclusionReason}]");
            PreviewText.Text = sb.ToString();
            StatusText.Text = L.T("Vorschau bereit.", "Preview ready.");
            return true;
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
            var patterns = (CustomPatternsBox.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _plan = await _planner.CreateAsync(SourceBox.Text!, DestinationBox.Text!, profile, patterns, selected);
        }
        if (_plan!.Included.Any(x => x.IsSensitive) && SensitiveConfirmation.IsChecked != true)
        {
            StatusText.Text = L.T(
                "Bitte sensible Dateien markieren oder deren Aufnahme ausdrücklich bestätigen.",
                "Please mark sensitive files or explicitly confirm their inclusion.");
            return;
        }
        SetBusy(true); _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<ExportProgress>(p =>
            {
                Progress.Maximum = Math.Max(1, p.TotalFiles);
                Progress.Value = p.CompletedFiles;
                StatusText.Text = $"{p.CompletedFiles}/{p.TotalFiles}: {p.CurrentPath}";
            });
            var result = await _exporter.ExportAsync(_plan, progress, _cts.Token);
            var validation = await _validator.ValidateAsync(_plan, _cts.Token);
            StatusText.Text = validation.Success
                ? L.T("ZIP erstellt und validiert: ", "ZIP created and validated: ") + $"{result.FileCount:N0} {L.T("Dateien", "files")}, {FormatBytes(result.BytesWritten)}"
                : L.T("Validierung fehlgeschlagen: ", "Validation failed: ") + string.Join("; ", validation.Errors);
            OpenFolderButton.IsEnabled = validation.Success;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = L.T("Export abgebrochen; unvollständiges Archiv wurde entfernt.",
                                   "Export cancelled; incomplete archive was removed.");
        }
        catch (Exception ex)
        {
            StatusText.Text = L.T("Export fehlgeschlagen: ", "Export failed: ") + ex.Message;
        }
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

    private void InvalidatePlan()
    {
        _plan = null;
        if (StatusText is not null)
            StatusText.Text = L.T("Eingaben geändert – Vorschau aktualisieren.", "Inputs changed – refresh the preview.");
    }

    private void SetDefaultDestination(string source) => DestinationBox.Text = Path.Combine(
        Directory.GetParent(source)?.FullName ?? source,
        Path.GetFileName(Path.TrimEndingDirectorySeparator(source)) + "-Clean.zip");

    private void UpdateGitInfo()
    {
        try
        {
            var info = _worktrees.Detect(SourceBox.Text ?? "");
            GitInfoText.Text = info.IsGitRepository
                ? $"{L.T("Repository:", "Repository:")} {info.RepositoryPath}\n{L.T("Branch:", "Branch:")} {info.Branch}\n{L.T("Typ:", "Type:")} " +
                  (info.IsWorktree ? L.T("Git-Worktree", "Git worktree") : L.T("normales Arbeitsverzeichnis", "regular working directory"))
                : info.Message;
            WorktreeWarning.Text = info.IsWorktree ? info.Message : "";
        }
        catch (Exception ex) { GitInfoText.Text = ex.Message; }
    }

    private void SetBusy(bool busy) { ExportButton.IsEnabled = !busy; CancelButton.IsEnabled = busy; }

    private static string FormatBytes(long value)
    {
        string[] u = ["B", "KB", "MB", "GB", "TB"];
        double n = value; var i = 0;
        while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; }
        return $"{n:0.##} {u[i]}";
    }
}
