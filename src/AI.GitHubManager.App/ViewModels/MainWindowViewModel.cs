using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.Core.Diagnostics;
using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Process;
using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Core.Update;
using AI.GitHubManager.Data;

namespace AI.GitHubManager.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly CommandRunner _runner = new();
    private readonly GitService _git;
    private readonly GitHubCliService _gh;
    private readonly EnvironmentCheckService _checks;
    private readonly SyncPreflightService _preflight;
    private readonly UpdateCheckService _updateCheck = new();
    private readonly JsonProjectStore _store = new();

    private ManagedProject? _selectedProject;
    private string _localPath = string.Empty;
    private string _commitMessage = "Update";
    private string _log = "Bereit.";
    private bool _isBusy;
    private string _updateNotice = string.Empty;
    private string? _updateDownloadUrl;

    private RelayCommand[] _allCommands = Array.Empty<RelayCommand>();

    public LocalizedStrings Strings => LocalizedStrings.Instance;
    public Func<Task<string?>>? FolderPickerFunc { get; set; }

    public MainWindowViewModel()
    {
        _git       = new GitService(_runner);
        _gh        = new GitHubCliService(_runner);
        _checks    = new EnvironmentCheckService(_git, _gh);
        _preflight = new SyncPreflightService(_git, _gh);

        Projects = new ObservableCollection<ManagedProject>();

        var checkEnv         = new RelayCommand(CheckEnvironmentAsync,      () => !IsBusy);
        var loadProjects     = new RelayCommand(LoadProjectsAsync,           () => !IsBusy);
        var saveProject      = new RelayCommand(SaveProjectAsync,            () => !IsBusy);
        var gitStatus        = new RelayCommand(GitStatusAsync,              () => !IsBusy);
        var pull             = new RelayCommand(PullAsync,                   () => !IsBusy);
        var commitPush       = new RelayCommand(CommitPushAsync,             () => !IsBusy);
        var refreshScope     = new RelayCommand(RefreshWorkflowScopeAsync,   () => !IsBusy);
        var setupGit         = new RelayCommand(SetupGitAsync,               () => !IsBusy);
        var loginGitHub      = new RelayCommand(LoginGitHubAsync,            () => !IsBusy);
        var installCli       = new RelayCommand(InstallGitHubCliAsync,       () => !IsBusy);
        var pickFolder       = new RelayCommand(PickFolderAsync,             () => !IsBusy);
        var addProject       = new RelayCommand(AddProjectAsync,             () => !IsBusy);
        var removeProject    = new RelayCommand(RemoveProjectAsync,          () => SelectedProject != null && !IsBusy);
        var importGitHub     = new RelayCommand(ImportGitHubReposAsync,      () => !IsBusy);
        var openUpdate       = new RelayCommand(() => { OpenUpdateDownload(); return Task.CompletedTask; }, () => !string.IsNullOrEmpty(_updateDownloadUrl));
        var checkUpdateNow   = new RelayCommand(CheckForUpdateAsync,         () => !IsBusy);

        CheckEnvironmentCommand     = checkEnv;
        LoadProjectsCommand         = loadProjects;
        SaveProjectCommand          = saveProject;
        GitStatusCommand            = gitStatus;
        PullCommand                 = pull;
        CommitPushCommand           = commitPush;
        RefreshWorkflowScopeCommand = refreshScope;
        SetupGitCommand             = setupGit;
        LoginGitHubCommand          = loginGitHub;
        InstallGitHubCliCommand     = installCli;
        PickFolderCommand           = pickFolder;
        AddProjectCommand           = addProject;
        RemoveProjectCommand        = removeProject;
        ImportGitHubReposCommand    = importGitHub;
        OpenUpdateCommand           = openUpdate;
        CheckForUpdateCommand       = checkUpdateNow;

        _allCommands = new[]
        {
            checkEnv, loadProjects, saveProject, gitStatus, pull,
            commitPush, refreshScope, setupGit, loginGitHub, installCli,
            pickFolder, addProject, removeProject, importGitHub,
            openUpdate, checkUpdateNow
        };

        _ = LoadProjectsAsync();
        _ = RunStartupUpdateCheckAsync();
    }

    // ── Public properties ────────────────────────────────────────────────────

    public ObservableCollection<ManagedProject> Projects { get; }

    public ICommand CheckEnvironmentCommand     { get; }
    public ICommand LoadProjectsCommand         { get; }
    public ICommand SaveProjectCommand          { get; }
    public ICommand GitStatusCommand            { get; }
    public ICommand PullCommand                 { get; }
    public ICommand CommitPushCommand           { get; }
    public ICommand RefreshWorkflowScopeCommand { get; }
    public ICommand SetupGitCommand             { get; }
    public ICommand LoginGitHubCommand          { get; }
    public ICommand InstallGitHubCliCommand     { get; }
    public ICommand PickFolderCommand           { get; }
    public ICommand AddProjectCommand           { get; }
    public ICommand RemoveProjectCommand        { get; }
    public ICommand ImportGitHubReposCommand    { get; }
    public ICommand OpenUpdateCommand           { get; }
    public ICommand CheckForUpdateCommand       { get; }

    public ManagedProject? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                LocalPath = value?.GetPathForCurrentPlatform() ?? string.Empty;
                foreach (var cmd in _allCommands) cmd.RaiseCanExecuteChanged();
            }
        }
    }

    public string LocalPath     { get => _localPath;     set => SetProperty(ref _localPath, value); }
    public string CommitMessage { get => _commitMessage; set => SetProperty(ref _commitMessage, value); }
    public string Log           { get => _log;           set => SetProperty(ref _log, value); }

    /// <summary>Non-empty when a newer release is available. Bound to the update banner.</summary>
    public string UpdateNotice
    {
        get => _updateNotice;
        private set
        {
            if (SetProperty(ref _updateNotice, value))
                OnPropertyChanged(nameof(HasUpdateNotice));
        }
    }

    /// <summary>Controls update banner visibility.</summary>
    public bool HasUpdateNotice => !string.IsNullOrEmpty(_updateNotice);

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                foreach (var cmd in _allCommands) cmd.RaiseCanExecuteChanged();
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    private async Task CheckEnvironmentAsync()
    {
        await Busy(async () =>
        {
            if (SelectedProject is not null &&
                !string.IsNullOrWhiteSpace(SelectedProject.GetPathForCurrentPlatform()))
            {
                var preflight = await _preflight.CheckAsync(SelectedProject);
                Log = preflight.ToLogText();
            }
            else
            {
                var check = await _checks.CheckAsync();
                Log = $"Git: {(check.GitInstalled ? "OK" : "FEHLT")}\n{check.GitVersion}" +
                      $"\n\nGitHub CLI: {(check.GitHubCliInstalled ? "OK" : "FEHLT")}\n{check.GitHubCliVersion}" +
                      $"\n\nLogin: {(check.GitHubAuthenticated ? "OK" : "NICHT OK")}\n{check.GitHubAuthOutput}";
            }
        });
    }

    private async Task LoadProjectsAsync()
    {
        var loaded = await _store.LoadAsync();
        Projects.Clear();
        foreach (var p in loaded) Projects.Add(p);
        SelectedProject ??= Projects.FirstOrDefault();
    }

    private async Task SaveProjectAsync()
    {
        if (SelectedProject is null) return;
        ApplyPathToSelectedProject();
        SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
        await _store.SaveAsync(Projects);
        Log = "Projekt gespeichert.";
    }

    private async Task GitStatusAsync()
    {
        await Busy(async () =>
        {
            var status = await _git.GetStatusAsync(LocalPath);
            var sb = new StringBuilder();
            sb.AppendLine(status.IsRepository ? "Repository: OK" : "Repository: FEHLT/UNGÜLTIG");
            sb.AppendLine($"Branch: {status.Branch}");
            sb.AppendLine($"Remote: {status.RemoteOrigin}");
            if (!string.IsNullOrWhiteSpace(status.ErrorMessage)) sb.AppendLine(status.ErrorMessage);
            sb.AppendLine();
            sb.AppendLine("Geänderte Dateien:");
            foreach (var file in status.ChangedFiles) sb.AppendLine(file);
            if (status.ChangedFiles.Count == 0) sb.AppendLine("Keine Änderungen.");
            Log = sb.ToString();
        });
    }

    private async Task PullAsync()
    {
        await Busy(async () =>
        {
            var result = await _git.PullAsync(LocalPath, SelectedProject?.DefaultBranch, SelectedProject?.RemoteUrl);
            Log = result.Success
                ? result.CombinedOutput
                : EnrichWithErrorHint(result.CombinedOutput);
        });
    }

    private async Task CommitPushAsync()
    {
        await Busy(async () =>
        {
            var result = await _git.CommitAndPushAsync(LocalPath, CommitMessage);
            Log = result.Success
                ? result.CombinedOutput
                : EnrichWithErrorHint(result.CombinedOutput);
        });
    }

    private async Task LoginGitHubAsync()
    {
        await Busy(async () =>
        {
            var result = await _gh.OpenAuthLoginTerminalAsync();
            Log = result.Success
                ? "GitHub-Login wurde in einem separaten Terminalfenster gestartet.\n\nDort den Browser-Code bestätigen. Danach hier 'Umgebung prüfen' drücken."
                : result.CombinedOutput;
        });
    }

    private async Task InstallGitHubCliAsync()
    {
        await Busy(async () =>
        {
            var result = await _gh.OpenInstallGitHubCliTerminalAsync();
            Log = result.Success
                ? "GitHub-CLI-Installation wurde in einem separaten Terminalfenster gestartet.\n\nNach der Installation Visual Studio/App neu starten und dann GitHub Login ausführen."
                : result.CombinedOutput;
        });
    }

    private async Task RefreshWorkflowScopeAsync()
    {
        await Busy(async () =>
        {
            var result = await _gh.RefreshWorkflowScopeAsync();
            Log = result.CombinedOutput;
        });
    }

    private async Task SetupGitAsync()
    {
        await Busy(async () =>
        {
            var result = await _gh.SetupGitAsync();
            Log = result.CombinedOutput;
        });
    }

    private async Task PickFolderAsync()
    {
        if (FolderPickerFunc is null) return;
        var path = await FolderPickerFunc();
        if (!string.IsNullOrWhiteSpace(path))
        {
            LocalPath = path;
            if (SelectedProject is not null)
            {
                ApplyPathToSelectedProject();
                SelectedProject.UpdatedAt = DateTimeOffset.UtcNow;
                await _store.SaveAsync(Projects);
                Log = "Ordner ausgewählt und Projekt gespeichert.";
            }
        }
    }

    private async Task AddProjectAsync()
    {
        if (FolderPickerFunc is null) return;
        var path = await FolderPickerFunc();
        if (string.IsNullOrWhiteSpace(path)) return;

        await Busy(async () =>
        {
            var folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var project = new ManagedProject { Name = string.IsNullOrWhiteSpace(folderName) ? "Neues Projekt" : folderName };

            if (OperatingSystem.IsWindows())    project.WindowsPath = path;
            else if (OperatingSystem.IsMacOS()) project.MacPath     = path;
            else                                project.LinuxPath   = path;

            var status = await _git.GetStatusAsync(path);
            if (!string.IsNullOrWhiteSpace(status.RemoteOrigin))
            {
                project.RemoteUrl = status.RemoteOrigin;
                var parsed = TryParseGitHubUrl(project.RemoteUrl);
                if (parsed.HasValue)
                    project.Owner = parsed.Value.Owner;
            }

            if (!string.IsNullOrWhiteSpace(status.Branch))
                project.DefaultBranch = status.Branch;

            Projects.Add(project);
            SelectedProject = project;
            await _store.SaveAsync(Projects);
            Log = $"Projekt '{project.Name}' hinzugefügt.";
        });
    }

    private async Task RemoveProjectAsync()
    {
        if (SelectedProject is null) return;
        var name = SelectedProject.Name;
        Projects.Remove(SelectedProject);
        SelectedProject = Projects.FirstOrDefault();
        await _store.SaveAsync(Projects);
        Log = $"Projekt '{name}' entfernt.";
    }

    private async Task ImportGitHubReposAsync()
    {
        await Busy(async () =>
        {
            var repos = await _gh.ListRepositoriesAsync();
            if (repos.Count == 0)
            {
                Log = "Keine Repositories gefunden. Bitte erst GitHub Login ausführen.";
                return;
            }

            int added = 0;
            foreach (var repo in repos)
            {
                var url = repo.Url.TrimEnd('/');
                bool alreadyExists = Projects.Any(p =>
                    p.RemoteUrl.TrimEnd('/').TrimSuffix(".git").Equals(
                        url.TrimSuffix(".git"), StringComparison.OrdinalIgnoreCase));

                if (alreadyExists) continue;

                Projects.Add(new ManagedProject
                {
                    Name          = repo.Name,
                    Owner         = repo.Owner,
                    RemoteUrl     = url + ".git",
                    DefaultBranch = repo.DefaultBranch
                });
                added++;
            }

            await _store.SaveAsync(Projects);
            Log = added > 0
                ? $"{added} Repositories von GitHub importiert."
                : "Alle Repositories bereits vorhanden.";
        });
    }

    // ── Update check ─────────────────────────────────────────────────────────

    /// <summary>Silent background check on startup — never shows errors to the user.</summary>
    private async Task RunStartupUpdateCheckAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var result = await _updateCheck.CheckAsync(cts.Token);
            ApplyUpdateResult(result, silent: true);
        }
        catch { /* Never crash the app for an update check */ }
    }

    private async Task CheckForUpdateAsync()
    {
        await Busy(async () =>
        {
            var result = await _updateCheck.CheckAsync();
            ApplyUpdateResult(result, silent: false);
        });
    }

    private void ApplyUpdateResult(UpdateCheckResult result, bool silent)
    {
        if (result.IsUpdateAvailable)
        {
            _updateDownloadUrl = result.DirectDownloadUrl ?? result.ReleasePageUrl;
            UpdateNotice = $"⬆ Update verfügbar: v{result.LatestVersion}  (aktuell: v{result.CurrentVersion})";
            foreach (var cmd in _allCommands) cmd.RaiseCanExecuteChanged();

            if (!silent)
                Log = $"Neue Version gefunden: v{result.LatestVersion}\n\nJetzt herunterladen → {_updateDownloadUrl}";
        }
        else if (!silent)
        {
            UpdateNotice = string.Empty;
            Log = string.IsNullOrEmpty(result.ErrorMessage)
                ? $"App ist aktuell (v{result.CurrentVersion})."
                : $"Update-Check: {result.ErrorMessage}";
        }
    }

    private void OpenUpdateDownload()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_updateDownloadUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log = $"Download konnte nicht geöffnet werden: {ex.Message}\n\n{_updateDownloadUrl}";
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string EnrichWithErrorHint(string rawOutput)
    {
        var info = GitErrorParser.Parse(rawOutput);
        if (info.Kind == GitErrorKind.Unknown)
            return rawOutput;

        var sb = new StringBuilder();
        sb.AppendLine(rawOutput.TrimEnd());
        sb.AppendLine();
        sb.AppendLine("── Fehleranalyse ────────────────────────────");
        sb.AppendLine($"Ursache: {info.UserMessage}");
        if (!string.IsNullOrWhiteSpace(info.Hint))
            sb.AppendLine($"Lösung:  {info.Hint}");
        return sb.ToString().TrimEnd();
    }

    private void ApplyPathToSelectedProject()
    {
        if (SelectedProject is null) return;
        if (OperatingSystem.IsWindows())    SelectedProject.WindowsPath = LocalPath;
        else if (OperatingSystem.IsMacOS()) SelectedProject.MacPath     = LocalPath;
        else                                SelectedProject.LinuxPath   = LocalPath;
    }

    private static (string Owner, string Repo)? TryParseGitHubUrl(string url)
    {
        var m = Regex.Match(url, @"github\.com[/:]([^/]+)/([^/\.]+)");
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : null;
    }

    private async Task Busy(Func<Task> action)
    {
        if (IsBusy) return;
        try   { IsBusy = true; await action(); }
        catch (Exception ex) { Log = ex.ToString(); }
        finally { IsBusy = false; }
    }
}

file static class StringExtensions
{
    public static string TrimSuffix(this string s, string suffix) =>
        s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? s[..^suffix.Length] : s;
}
