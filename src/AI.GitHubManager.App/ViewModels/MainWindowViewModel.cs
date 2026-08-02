using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AI.GitHubManager.App.Services;
using AI.GitHubManager.Core.Build;
using AI.GitHubManager.Core.Diagnostics;
using AI.GitHubManager.Core.EnvironmentRepair;
using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Operations;
using AI.GitHubManager.Core.Process;
using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Core.Remote;
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
    private readonly AuthenticationDiagnosticService _authDiagnostics;
    private readonly ProjectBuildService _projectBuild;
    private readonly InstallerBuildService _installerBuild;
    private readonly UpdateCheckService _updateCheck = new();
    private readonly SafePullService _safePull;
    private readonly JsonProjectStore _store = new();
    private readonly AppSettingsService _settings = AppSettingsService.Load();

    private ManagedProject? _selectedProject;
    private string _localPath = string.Empty;
    private string _commitMessage = "Update";
    private string _log = "Bereit.";
    private bool _isBusy;
    private string _updateNotice = string.Empty;
    private string? _updateDownloadUrl;
    private bool _canRepairEnvironmentToken;
    private bool _canSanitizeRemote;
    private bool _showInstallInnoSetup;
    private bool _canRemoveOrphanedGitLock;
    private GitOperationDefinition _selectedOperation = GitOperationCatalog.Update;

    private RelayCommand[] _allCommands = Array.Empty<RelayCommand>();

    public LocalizedStrings Strings => LocalizedStrings.Instance;
    public Func<Task<string?>>? FolderPickerFunc { get; set; }

    public MainWindowViewModel() : this(gitService: null) { }

    /// <summary>
    /// Test/DI seam: allows injecting a <see cref="GitService"/> already configured
    /// with a fake <see cref="IGitProcessDetector"/> (e.g. always-"no active process"
    /// or always-"active process"), so lock-detection tests are deterministic and do
    /// not depend on real git.exe processes that may be running concurrently on the
    /// test machine (including ones spawned by other tests in the same run). The
    /// parameterless constructor passes <c>null</c> and gets the real, production
    /// <see cref="GitService"/> with the platform's real process detector.
    /// </summary>
    internal MainWindowViewModel(GitService? gitService)
    {
        _git             = gitService ?? new GitService(_runner);
        _gh              = new GitHubCliService(_runner);
        _checks          = new EnvironmentCheckService(_git, _gh);
        _preflight       = new SyncPreflightService(_git, _gh);
        _authDiagnostics = new AuthenticationDiagnosticService(_gh);
        _projectBuild    = new ProjectBuildService(_runner);
        _installerBuild  = new InstallerBuildService(_runner);
        _safePull        = new SafePullService(_runner);

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
        var repairToken      = new RelayCommand(RepairEnvironmentTokenAsync, () => !IsBusy);
        var sanitizeRemote   = new RelayCommand(SanitizeRemoteAsync,         () => !IsBusy);
        var exportDiagnostics = new RelayCommand(ExportDiagnosticsAsync,     () => !IsBusy);
        var buildTestPush    = new RelayCommand(BuildTestAndPushAsync,      () => !IsBusy);
        var createInstaller  = new RelayCommand(CreateInstallerAsync,       () => !IsBusy);
        var installInnoSetup = new RelayCommand(() => { OpenInnoSetupDownload(); return Task.CompletedTask; }, () => !IsBusy);
        var removeOrphanedGitLock = new RelayCommand(RemoveOrphanedGitLockAsync, () => !IsBusy);
        var executeSelectedOperation = new RelayCommand(ExecuteSelectedOperationAsync, () => !IsBusy);

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
        RepairEnvironmentTokenCommand = repairToken;
        SanitizeRemoteCommand         = sanitizeRemote;
        ExportDiagnosticsCommand      = exportDiagnostics;
        BuildTestPushCommand          = buildTestPush;
        CreateInstallerCommand        = createInstaller;
        InstallInnoSetupCommand       = installInnoSetup;
        RemoveOrphanedGitLockCommand  = removeOrphanedGitLock;
        ExecuteSelectedOperationCommand = executeSelectedOperation;

        _allCommands = new[]
        {
            checkEnv, loadProjects, saveProject, gitStatus, pull,
            commitPush, refreshScope, setupGit, loginGitHub, installCli,
            pickFolder, addProject, removeProject, importGitHub,
            openUpdate, checkUpdateNow, repairToken, sanitizeRemote, exportDiagnostics,
            buildTestPush, createInstaller, installInnoSetup, removeOrphanedGitLock,
            executeSelectedOperation
        };

        // Restore the last safely-persisted operation selection (Teil B1/C).
        // Fault-tolerant: falls back to "Aktualisieren" for missing/unknown/
        // dangerous stored ids (AppSettingsService.GetSelectedOperationOrDefault).
        _selectedOperation = _settings.GetSelectedOperationOrDefault();

        // Operation description texts are bilingual fields read at get-time
        // (L.IsEnglish) — refresh all of them whenever the language changes.
        L.Changed += RaiseSelectedOperationPropertiesChanged;

        RefreshInnoSetupAvailability();
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
    public ICommand RepairEnvironmentTokenCommand { get; }
    public ICommand SanitizeRemoteCommand         { get; }
    public ICommand ExportDiagnosticsCommand      { get; }
    public ICommand BuildTestPushCommand          { get; }
    public ICommand CreateInstallerCommand        { get; }
    public ICommand InstallInnoSetupCommand       { get; }
    public ICommand RemoveOrphanedGitLockCommand  { get; }
    public ICommand ExecuteSelectedOperationCommand { get; }

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

    // ── Understandable Git operation model (Teil B1–B5) ─────────────────────

    /// <summary>Normal operations shown in the main dropdown, in a fixed, documented order.</summary>
    public IReadOnlyList<GitOperationDefinition> AvailableOperations => GitOperationCatalog.NormalOperations;

    /// <summary>
    /// "Erweiterte Befehle" (Teil B2) — never in the normal dropdown, shown only
    /// in a separate, collapsed/deactivated area of the UI with extra confirmation.
    /// </summary>
    public IReadOnlyList<GitOperationDefinition> AdvancedOperations => GitOperationCatalog.AdvancedOperations;

    /// <summary>
    /// The currently selected normal operation. Default is "Aktualisieren"
    /// (Teil B1/C). Setting it persists the choice only when safe to do so
    /// (Teil B7/C) — dangerous/advanced operations never live in this
    /// property in the first place, since they aren't part of
    /// <see cref="AvailableOperations"/>.
    /// </summary>
    public GitOperationDefinition SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            if (!SetProperty(ref _selectedOperation, value)) return;

            if (_settings.SetSelectedOperationIfSafe(value))
                _settings.Save();

            RaiseSelectedOperationPropertiesChanged();
        }
    }

    private void RaiseSelectedOperationPropertiesChanged()
    {
        // Re-raising the collections (not just the description texts) makes
        // Avalonia rebuild the dropdown's items, which is what makes the
        // GitOperationTitleConverter re-evaluate for every entry — keeping
        // the dropdown list itself, not just the panel below it, in sync
        // with the selected language.
        OnPropertyChanged(nameof(AvailableOperations));
        OnPropertyChanged(nameof(AdvancedOperations));
        OnPropertyChanged(nameof(SelectedOperationTitle));
        OnPropertyChanged(nameof(SelectedOperationDescription));
        OnPropertyChanged(nameof(SelectedOperationSuitableFor));
        OnPropertyChanged(nameof(SelectedOperationWhatChanges));
        OnPropertyChanged(nameof(SelectedOperationWhatStays));
        OnPropertyChanged(nameof(SelectedOperationRisk));
        OnPropertyChanged(nameof(SelectedOperationRiskLevelText));
        OnPropertyChanged(nameof(SelectedOperationCommand));
        OnPropertyChanged(nameof(SelectedOperationPreflightSteps));
        OnPropertyChanged(nameof(SelectedOperationPreflightSummary));
        OnPropertyChanged(nameof(HasSelectedOperationPreflightPreview));
    }

    // Explanation panel shown directly under the dropdown (Teil B4) — always
    // derived from the single GitOperationDefinition source of truth, never
    // duplicated as separate hard-coded strings.
    public string SelectedOperationTitle       => SelectedOperation.Title(L.IsEnglish);
    public string SelectedOperationDescription => SelectedOperation.Description(L.IsEnglish);
    public string SelectedOperationSuitableFor => SelectedOperation.SuitableFor(L.IsEnglish);
    public string SelectedOperationWhatChanges => SelectedOperation.WhatChanges(L.IsEnglish);
    public string SelectedOperationWhatStays   => SelectedOperation.WhatStays(L.IsEnglish);
    public string SelectedOperationRisk        => SelectedOperation.Risk(L.IsEnglish);
    public string SelectedOperationCommand     => SelectedOperation.TechnicalCommand;

    public string SelectedOperationRiskLevelText => RiskLevelText(SelectedOperation.RiskLevel);

    /// <summary>
    /// "Was passiert jetzt?" step list for the selected operation (Teil B6),
    /// shown in the main explanation panel — empty for operations without a
    /// canned preview yet (nothing fabricated).
    /// </summary>
    public IReadOnlyList<string> SelectedOperationPreflightSteps =>
        OperationPreflightPreview.Steps(SelectedOperation, L.IsEnglish);

    /// <summary>"Zusammenfassung" line for the selected operation (Teil B6), or null.</summary>
    public string? SelectedOperationPreflightSummary =>
        OperationPreflightPreview.Summary(SelectedOperation, L.IsEnglish);

    public bool HasSelectedOperationPreflightPreview => SelectedOperationPreflightSteps.Count > 0;

    /// <summary>Risk level as visible text (Teil B5 — never color-only).</summary>
    public static string RiskLevelText(GitOperationRiskLevel level) => level switch
    {
        GitOperationRiskLevel.Safe      => L.T("Sicher",     "Safe"),
        GitOperationRiskLevel.Caution   => L.T("Vorsicht",   "Caution"),
        GitOperationRiskLevel.Advanced  => L.T("Erweitert",  "Advanced"),
        GitOperationRiskLevel.Dangerous => L.T("Gefährlich", "Dangerous"),
        _ => L.T("Unbekannt", "Unknown"),
    };

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

    /// <summary>
    /// True when the last check found a state where an invalid GH_TOKEN/GITHUB_TOKEN
    /// is hiding a valid `gh` keyring login. Controls the repair button's visibility.
    /// </summary>
    public bool CanRepairEnvironmentToken
    {
        get => _canRepairEnvironmentToken;
        private set => SetProperty(ref _canRepairEnvironmentToken, value);
    }

    /// <summary>True when the current remote origin contains embedded credentials or a placeholder.</summary>
    public bool CanSanitizeRemote
    {
        get => _canSanitizeRemote;
        private set => SetProperty(ref _canSanitizeRemote, value);
    }

    /// <summary>
    /// True only on Windows, and only while Inno Setup 6 is not found at any of
    /// its well-known install locations. Controls visibility of the proactive
    /// "Install Inno Setup" button: shown when missing, hidden as soon as it's
    /// found (checked at startup and re-checked whenever the environment check
    /// or "Create Installer" run).
    /// </summary>
    public bool ShowInstallInnoSetup
    {
        get => _showInstallInnoSetup;
        private set => SetProperty(ref _showInstallInnoSetup, value);
    }

    /// <summary>
    /// True when the last check found a <c>.git/index.lock</c> that looks orphaned
    /// (no active git process detected, old enough to be safe). Controls visibility
    /// of the "Verwaiste Git-Sperre sicher entfernen" button. Never set true for a
    /// lock that might still belong to an active process — see <c>GitLockGuard</c>.
    /// </summary>
    public bool CanRemoveOrphanedGitLock
    {
        get => _canRemoveOrphanedGitLock;
        private set => SetProperty(ref _canRemoveOrphanedGitLock, value);
    }

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
            RefreshInnoSetupAvailability();

            if (SelectedProject is not null &&
                !string.IsNullOrWhiteSpace(SelectedProject.GetPathForCurrentPlatform()))
            {
                var preflight = await _preflight.CheckAsync(SelectedProject);
                Log = preflight.ToLogText();
                CanRepairEnvironmentToken = preflight.Items.Any(i => i.RepairActionId == "repair-environment-token");
                CanSanitizeRemote         = preflight.Items.Any(i => i.RepairActionId == "sanitize-remote");
            }
            else
            {
                var check = await _checks.CheckAsync();
                var notOk = L.T("FEHLT", "MISSING");
                var loginNotOk = L.T("NICHT OK", "NOT OK");
                Log = $"Git: {(check.GitInstalled ? "OK" : notOk)}\n{check.GitVersion}" +
                      $"\n\nGitHub CLI: {(check.GitHubCliInstalled ? "OK" : notOk)}\n{check.GitHubCliVersion}" +
                      $"\n\n{L.T("Login", "Login")}: {(check.GitHubAuthenticated ? "OK" : loginNotOk)}\n{check.GitHubAuthOutput}";
                CanRepairEnvironmentToken = check.Authentication?.State == AuthenticationState.EnvironmentTokenOverridesValidKeyring;
                CanSanitizeRemote = false;
            }

            // Runs last so a detected lock/interrupted-state banner it may prepend to
            // Log survives (an earlier Log = ... assignment above would otherwise wipe
            // it out, and the banner needs the final Log text to prepend onto).
            RefreshGitLockAvailability();
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
        Log = L.T("Projekt gespeichert.", "Project saved.");
    }

    /// <summary>
    /// Runs the currently selected operation from the dropdown (Teil B1).
    /// Only the operations already wired to a real, tested command in this
    /// milestone (Status, Aktualisieren, Commit erstellen und hochladen —
    /// see <see cref="GitOperationDefinition.IsExecutable"/>) actually run
    /// something; everything else in the model exists as a fully described
    /// entry but reports plainly that execution follows in a later
    /// milestone (Vorabprüfungen/Schutzmechanismen), rather than silently
    /// doing nothing or pretending to run.
    /// </summary>
    private async Task ExecuteSelectedOperationAsync()
    {
        var operation = SelectedOperation;

        switch (operation.Id)
        {
            case "status":
                await GitStatusAsync();
                return;
            case "update":
                await PullAsync();
                return;
            case "commit-and-upload":
                await CommitPushAsync();
                return;
        }

        Log = L.T(
            $"„{operation.TitleDe}\" ({operation.TechnicalCommand}) ist beschrieben, aber in diesem " +
            "Meilenstein noch nicht ausführbar. Vorabprüfungen und die technische Ausführung folgen in " +
            "einem späteren Schritt. Nutze für die bereits vorhandenen Aktionen die Schaltflächen " +
            "\"Status\", \"Aktualisieren\" bzw. \"Commit erstellen und hochladen\".",
            $"\"{operation.TitleEn}\" ({operation.TechnicalCommand}) is described but not yet executable " +
            "in this milestone. Preflight checks and technical execution follow in a later step. Use the " +
            "existing \"Status\", \"Update\", or \"Create commit and upload\" actions for now.");
    }

    private async Task GitStatusAsync()
    {
        await Busy(async () =>
        {
            var status = await _git.GetStatusAsync(LocalPath);
            var sb = new StringBuilder();
            sb.AppendLine(status.IsRepository
                ? "Repository: OK"
                : L.T("Repository: FEHLT/UNGÜLTIG", "Repository: MISSING/INVALID"));
            sb.AppendLine($"Branch: {status.Branch}");
            sb.AppendLine($"Remote: {status.RemoteOrigin}");
            if (!string.IsNullOrWhiteSpace(status.ErrorMessage)) sb.AppendLine(status.ErrorMessage);
            sb.AppendLine();
            sb.AppendLine(L.T("Geänderte Dateien:", "Changed files:"));
            foreach (var file in status.ChangedFiles) sb.AppendLine(file);
            if (status.ChangedFiles.Count == 0) sb.AppendLine(L.T("Keine Änderungen.", "No changes."));
            Log = sb.ToString();
        });
    }

    private async Task PullAsync()
    {
        await Busy(async () =>
        {
            if (!_settings.SafePullEnabled)
            {
                // Legacy path retained only for completeness; the default and
                // recommended path is the safe pull below.
                var legacyResult = await _git.PullAsync(LocalPath, SelectedProject?.DefaultBranch, SelectedProject?.RemoteUrl);
                Log = legacyResult.Success
                    ? legacyResult.CombinedOutput
                    : EnrichWithErrorHint(legacyResult.CombinedOutput);
                RefreshGitLockAvailability();
                return;
            }

            var result = await _safePull.PullAsync(LocalPath, warnOnlyOnLocalChanges: false);
            Log = FormatSafePullResult(result);
            RefreshGitLockAvailability();
        });
    }

    /// <summary>Renders a SafePullResult into a user-facing log message.
    /// Every state is handled explicitly — nothing falls through to raw console text.</summary>
    private string FormatSafePullResult(SafePullResult result)
    {
        return result.State switch
        {
            SafePullState.CleanPullSucceeded =>
                L.T("✅ Aktualisierung erfolgreich — keine lokalen Änderungen betroffen.",
                    "✅ Update successful — no local changes affected.") +
                "\n\n" + (result.PullOutput ?? result.Message),

            SafePullState.PullSucceededAndChangesRestored =>
                L.T("✅ Aktualisierung erfolgreich — lokale Änderungen wurden danach wiederhergestellt.",
                    "✅ Update successful — local changes were restored afterwards.") +
                "\n\n" + (result.PullOutput ?? string.Empty),

            // The raw pull output (e.g. "fatal: Not possible to fast-forward, aborting.")
            // is routed through GitErrorParser so the exact same bilingual cause/hint text
            // is shown here as for every other Git error (Teil B9 consistency requirement).
            SafePullState.PullFailedAndRestored =>
                EnrichWithErrorHint(result.PullOutput ?? result.Message),

            SafePullState.RestoreConflict =>
                L.T("⚠️ Aktualisierung teilweise erfolgreich — Wiederherstellung der lokalen Änderungen ergab Konflikte.",
                    "⚠️ Update partially successful — restoring local changes produced conflicts.") +
                "\n\n" + result.Message + "\n\n" +
                L.T("Betroffene Dateien:\n", "Affected files:\n") +
                string.Join("\n", result.ConflictFiles) +
                "\n\n" + L.T(
                    "Was jetzt möglich ist: Konflikte anzeigen, Sicherung behalten, oder Wiederherstellung erneut versuchen. " +
                    "Es wurden keine lokalen Änderungen verworfen.",
                    "What's possible now: view conflicts, keep the backup, or retry the restore. " +
                    "No local changes were discarded."),

            SafePullState.BackupCreationFailed =>
                L.T("❌ Aktualisierung abgebrochen — Schutzsicherung der lokalen Änderungen fehlgeschlagen.",
                    "❌ Update aborted — creating the safety backup of local changes failed.") +
                "\n\n" + result.Message,

            SafePullState.RestoreFailed =>
                L.T("❌ Aktualisierung fehlgeschlagen und Wiederherstellung der Sicherung ebenfalls fehlgeschlagen.",
                    "❌ Update failed and restoring the backup also failed.") +
                "\n\n" + result.Message,

            SafePullState.AbortedDueToLocalChanges =>
                L.T("⏸️ Aktualisierung abgebrochen — es wurden lokale Änderungen gefunden (Einstellung: nur warnen).",
                    "⏸️ Update cancelled — local changes were found (setting: warn only).") +
                "\n\n" + result.Message + "\n\n" + string.Join("\n", result.ConflictFiles),

            // Pull-specific diverged-branches failure — literal Teil B9 example
            // ("fatal: Not possible to fast-forward, aborting."). Routed through the
            // same bilingual error pipeline as every other Git error.
            SafePullState.FastForwardNotPossible =>
                EnrichWithErrorHint(result.PullOutput ?? result.Message),

            SafePullState.NotARepository =>
                L.T("❌ Kein gültiges Git-Repository im ausgewählten Ordner.",
                    "❌ No valid Git repository in the selected folder.") +
                "\n\n" + result.Message,

            SafePullState.WriteBlockedByLockGuard =>
                L.T("⏸️ Aktualisierung blockiert — ein anderer Git-Vorgang ist noch aktiv.",
                    "⏸️ Update blocked — another Git operation is still active.") +
                "\n\n" + result.Message + "\n\n" + L.T(
                "Nichts wurde verändert — weder ein Stash noch ein Pull wurde ausgeführt. " +
                "Falls die Sperre verwaist ist, kann sie über \"Verwaiste Git-Sperre sicher entfernen\" " +
                "geprüft und entfernt werden.",
                "Nothing was changed — neither a stash nor a pull ran. " +
                "If the lock is orphaned, it can be checked and removed via " +
                "\"Safely remove orphaned git lock\"."),

            _ => result.Message,
        };
    }


    private async Task CommitPushAsync()
    {
        await Busy(async () =>
        {
            var result = await _git.CommitAndPushAsync(LocalPath, CommitMessage);
            Log = result.Success
                ? result.CombinedOutput
                : EnrichWithErrorHint(result.CombinedOutput);
            RefreshGitLockAvailability();
        });
    }

    /// <summary>
    /// Guided GitHub login: checks the actual authentication state first and
    /// only starts a real login flow when one is genuinely needed. A layperson
    /// never has to know what an environment variable, a token, or the Windows
    /// keyring is — the app tells them exactly what happened and what to do.
    /// </summary>
    private async Task LoginGitHubAsync()
    {
        await Busy(async () =>
        {
            var auth = await _authDiagnostics.DiagnoseAsync();

            switch (auth.State)
            {
                // Fall A: already logged in — nothing to do.
                case AuthenticationState.AuthenticatedViaKeyring:
                case AuthenticationState.Authenticated:
                case AuthenticationState.AuthenticatedViaEnvironmentToken:
                    Log = L.T($"✅ GitHub-Konto verbunden: {auth.ActiveAccount}\n\nKein erneuter Login erforderlich.",
                               $"✅ GitHub account connected: {auth.ActiveAccount}\n\nNo new login required.");
                    CanRepairEnvironmentToken = false;
                    break;

                // Fall B: a bad token is hiding a valid login — offer the one-click repair.
                case AuthenticationState.EnvironmentTokenOverridesValidKeyring:
                    Log = $"{auth.Summary}\n\n" +
                          L.T("Klicke auf \"Ungültigen Token entfernen und Anmeldung reparieren\", um das automatisch zu beheben.",
                              "Click \"Remove invalid token and repair login\" to fix this automatically.");
                    CanRepairEnvironmentToken = true;
                    break;

                // Fall D: logged in, but a required permission is missing.
                case AuthenticationState.MissingRequiredScopes:
                    Log = $"{auth.Summary}\n\n" +
                          L.T("Klicke auf \"GitHub Rechte: repo + workflow\", um die fehlende Berechtigung zu ergänzen — eine komplette Neuanmeldung ist nicht nötig.",
                              "Click \"GitHub Scopes: repo + workflow\" to add the missing permission — a full re-login isn't necessary.");
                    break;

                // Fall C: no valid login at all — start the real login flow.
                default:
                    var result = await _gh.OpenAuthLoginTerminalAsync();
                    Log = result.Success
                        ? L.T("GitHub-Login wurde in einem separaten Terminalfenster gestartet.\n\nDort den Browser-Code bestätigen. Danach hier 'Umgebung prüfen' drücken.",
                              "GitHub login was started in a separate terminal window.\n\nConfirm the browser code there. Then click 'Check Environment' here.")
                        : result.CombinedOutput;
                    break;
            }
        });
    }

    /// <summary>
    /// One-click repair for the reference bug: an invalid GH_TOKEN/GITHUB_TOKEN in the
    /// Windows user environment hiding an otherwise valid `gh` keyring login. Never touches
    /// the keyring itself, never modifies system (HKLM) variables, and re-verifies the
    /// result immediately afterwards so the app can enable push again without a restart.
    /// </summary>
    private async Task RepairEnvironmentTokenAsync()
    {
        await Busy(async () =>
        {
            var plan = GitHubEnvironmentRepairService.CreatePlan();

            var intro = L.T(
                "Ein ungültiger Token in der Windows-Umgebung verhindert die Nutzung Ihrer bereits gültigen GitHub-Anmeldung.\n\n" +
                "Der Token wird aus der Benutzer-Umgebung entfernt. Die sichere Anmeldung im Windows-Schlüsselspeicher bleibt erhalten.\n",
                "An invalid token in the Windows environment is preventing use of your already-valid GitHub login.\n\n" +
                "The token will be removed from the user environment. The secure login in the Windows credential store remains untouched.\n");

            var result = GitHubEnvironmentRepairService.Repair(plan);
            var auth = await _authDiagnostics.DiagnoseAsync();

            var sb = new StringBuilder();
            sb.AppendLine(intro);
            sb.AppendLine(result.Success
                ? L.T("Reparatur erfolgreich", "Repair successful")
                : L.T("Reparatur teilweise fehlgeschlagen", "Repair partially failed"));
            sb.AppendLine();
            sb.AppendLine(L.T("Behoben:", "Fixed:"));
            foreach (var step in result.Steps)
                sb.AppendLine($"{(step.Success ? "✅" : "❌")} {step.Name} ({step.Scope}): {step.Message}");
            sb.AppendLine();
            sb.AppendLine($"{L.T("Erneute Prüfung:", "Re-checked:")} {auth.Summary}");
            if (!string.IsNullOrWhiteSpace(auth.TechnicalDetails))
            {
                sb.AppendLine();
                sb.AppendLine(L.T("Technische Details:", "Technical details:"));
                sb.AppendLine(auth.TechnicalDetails);
            }

            Log = sb.ToString();
            CanRepairEnvironmentToken = auth.State == AuthenticationState.EnvironmentTokenOverridesValidKeyring;
        });
    }

    /// <summary>
    /// One-click repair for a remote URL that embeds credentials (or an unresolved
    /// placeholder like DEIN_VORHANDENER_TOKEN) — resets it to the canonical,
    /// credential-free form. The token value itself is never shown or logged.
    /// </summary>
    private async Task SanitizeRemoteAsync()
    {
        await Busy(async () =>
        {
            if (string.IsNullOrWhiteSpace(LocalPath))
            {
                Log = L.T("Kein lokaler Ordner ausgewählt.", "No local folder selected.");
                return;
            }

            var status = await _git.GetStatusAsync(LocalPath);
            if (!status.IsRepository || string.IsNullOrWhiteSpace(status.RemoteOrigin))
            {
                Log = L.T("Keine Remote-URL gefunden.", "No remote URL found.");
                return;
            }

            var sanitized = RemoteUrlNormalizer.Sanitize(status.RemoteOrigin);
            var result = await _git.SetRemoteOriginAsync(LocalPath, sanitized);
            Log = result.Success
                ? L.T($"Remote sicher bereinigt.\n\nNeue Remote-URL: {sanitized}",
                      $"Remote cleaned up safely.\n\nNew remote URL: {sanitized}")
                : EnrichWithErrorHint(result.CombinedOutput);
            CanSanitizeRemote = !result.Success;
        });
    }

    /// <summary>
    /// Writes a redacted diagnostic report a user can hand to a developer or paste
    /// into an AI chat for help. Never contains tokens, passwords, or other secrets.
    /// </summary>
    private async Task ExportDiagnosticsAsync()
    {
        await Busy(async () =>
        {
            var gitVersion = await _git.VersionAsync();
            var ghVersion = await _gh.VersionAsync();
            var auth = await _authDiagnostics.DiagnoseAsync();

            GitStatusResult? status = null;
            if (!string.IsNullOrWhiteSpace(LocalPath))
                status = await _git.GetStatusAsync(LocalPath);

            var osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            var appVersion = GetType().Assembly.GetName().Version?.ToString() ?? L.T("unbekannt", "unknown");

            var report = DiagnosticReportService.Generate(
                appVersion,
                osDescription,
                gitVersion.CombinedOutput.Trim(),
                ghVersion.CombinedOutput.Trim(),
                LocalPath,
                status?.RemoteOrigin,
                status?.Branch ?? string.Empty,
                auth);

            var fileName = $"AI-GitHub-Manager-Diagnose_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var path = Path.Combine(Path.GetTempPath(), fileName);
            await File.WriteAllTextAsync(path, report);

            Log = L.T($"Diagnosebericht gespeichert:\n{path}\n\n",
                      $"Diagnostic report saved:\n{path}\n\n") +
                  L.T("Enthält keine Tokens oder Passwörter — kann sicher an einen Entwickler weitergegeben werden.\n\n",
                      "Contains no tokens or passwords — safe to share with a developer.\n\n") +
                  "──────────────────────────────\n\n" + report;
        });
    }

    /// <summary>
    /// Self-service release step: builds, then tests, then commits+pushes — in that
    /// order, stopping immediately on the first failure. Never pushes code that
    /// doesn't build or whose tests fail. Intended for .NET projects (like this
    /// manager's own repository); other project types get a clear "no build
    /// system found" message instead of a confusing failure.
    /// </summary>
    private async Task BuildTestAndPushAsync()
    {
        await Busy(async () =>
        {
            if (string.IsNullOrWhiteSpace(LocalPath))
            {
                Log = L.T("Kein lokaler Ordner ausgewählt.", "No local folder selected.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine(L.T("Schritt 1/3: Build …", "Step 1/3: Build …"));
            var build = await _projectBuild.BuildAsync(LocalPath);
            sb.AppendLine(build.CombinedOutput.Trim());
            if (!build.Success)
            {
                sb.AppendLine();
                sb.AppendLine(L.T("❌ Build fehlgeschlagen. Test und Push wurden übersprungen.",
                                   "❌ Build failed. Test and push were skipped."));
                Log = sb.ToString();
                return;
            }

            sb.AppendLine();
            sb.AppendLine(L.T("Schritt 2/3: Test …", "Step 2/3: Test …"));
            var test = await _projectBuild.TestAsync(LocalPath);
            sb.AppendLine(test.CombinedOutput.Trim());
            if (!test.Success)
            {
                sb.AppendLine();
                sb.AppendLine(L.T("❌ Tests fehlgeschlagen. Push wurde übersprungen.",
                                   "❌ Tests failed. Push was skipped."));
                Log = sb.ToString();
                return;
            }

            sb.AppendLine();
            sb.AppendLine(L.T("Schritt 3/3: Commit + Push …", "Step 3/3: Commit + Push …"));
            var push = await _git.CommitAndPushAsync(LocalPath, CommitMessage);
            sb.AppendLine(push.Success ? push.CombinedOutput.Trim() : EnrichWithErrorHint(push.CombinedOutput));
            sb.AppendLine();
            sb.AppendLine(push.Success
                ? L.T("✅ Build, Test und Push erfolgreich.", "✅ Build, test, and push successful.")
                : L.T("⚠️ Build und Test erfolgreich, aber Push fehlgeschlagen.",
                      "⚠️ Build and test succeeded, but push failed."));

            Log = sb.ToString();
            RefreshGitLockAvailability();
        });
    }

    /// <summary>
    /// Creates a distributable installer for the current project on this platform
    /// (Windows: publish + Inno Setup; macOS: build-installer-mac.sh). Can take
    /// several minutes — the log is only updated once the whole step finishes.
    /// </summary>
    private async Task CreateInstallerAsync()
    {
        await Busy(async () =>
        {
            if (string.IsNullOrWhiteSpace(LocalPath))
            {
                Log = L.T("Kein lokaler Ordner ausgewählt.", "No local folder selected.");
                return;
            }

            if (!_installerBuild.IsSupportedOnCurrentPlatform)
            {
                Log = L.T("Installer-Erstellung wird auf diesem Betriebssystem nicht unterstützt (nur Windows/macOS).",
                          "Installer creation isn't supported on this operating system (Windows/macOS only).");
                return;
            }

            Log = L.T("Installer wird erstellt … das kann einige Minuten dauern.",
                      "Creating installer … this can take a few minutes.");

            var result = await _installerBuild.CreateInstallerAsync(LocalPath);

            Log = (result.Success
                ? L.T("✅ Installer erstellt.\n\n", "✅ Installer created.\n\n")
                : L.T("❌ Installer-Erstellung fehlgeschlagen.\n\n", "❌ Installer creation failed.\n\n"))
                + result.CombinedOutput.Trim();

            RefreshInnoSetupAvailability();
        });
    }

    private async Task InstallGitHubCliAsync()
    {
        await Busy(async () =>
        {
            var result = await _gh.OpenInstallGitHubCliTerminalAsync();
            Log = result.Success
                ? L.T("GitHub-CLI-Installation wurde in einem separaten Terminalfenster gestartet.\n\nNach der Installation Visual Studio/App neu starten und dann GitHub Login ausführen.",
                      "GitHub CLI installation was started in a separate terminal window.\n\nAfter installation, restart Visual Studio/the app and then run GitHub Login.")
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
                Log = L.T("Ordner ausgewählt und Projekt gespeichert.", "Folder selected and project saved.");
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
            var project = new ManagedProject { Name = string.IsNullOrWhiteSpace(folderName) ? L.T("Neues Projekt", "New Project") : folderName };

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
            Log = L.T($"Projekt '{project.Name}' hinzugefügt.", $"Project '{project.Name}' added.");
        });
    }

    private async Task RemoveProjectAsync()
    {
        if (SelectedProject is null) return;
        var name = SelectedProject.Name;
        Projects.Remove(SelectedProject);
        SelectedProject = Projects.FirstOrDefault();
        await _store.SaveAsync(Projects);
        Log = L.T($"Projekt '{name}' entfernt.", $"Project '{name}' removed.");
    }

    private async Task ImportGitHubReposAsync()
    {
        await Busy(async () =>
        {
            var repos = await _gh.ListRepositoriesAsync();
            if (repos.Count == 0)
            {
                Log = L.T("Keine Repositories gefunden. Bitte erst GitHub Login ausführen.",
                          "No repositories found. Please run GitHub Login first.");
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
                ? L.T($"{added} Repositories von GitHub importiert.", $"{added} repositories imported from GitHub.")
                : L.T("Alle Repositories bereits vorhanden.", "All repositories already present.");
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
        if (result.IsUpdateAvailable && result.IsCompatibleAssetAvailable)
        {
            // Never fall back to the release page URL here — a direct asset was
            // actually matched to this platform/architecture by ReleaseAssetSelector.
            _updateDownloadUrl = result.DirectDownloadUrl;
            var assetLabel = string.IsNullOrEmpty(result.AssetName) ? _updateDownloadUrl : result.AssetName;
            UpdateNotice = L.T($"⬆ Update verfügbar: v{result.LatestVersion}  (aktuell: v{result.CurrentVersion})",
                                $"⬆ Update available: v{result.LatestVersion}  (current: v{result.CurrentVersion})");
            foreach (var cmd in _allCommands) cmd.RaiseCanExecuteChanged();

            if (!silent)
                Log = L.T($"Neue Version gefunden: v{result.LatestVersion}\n\nPaket für {result.OperatingSystem}/{result.Architecture}: {assetLabel}\n\nJetzt herunterladen → {_updateDownloadUrl}",
                          $"New version found: v{result.LatestVersion}\n\nPackage for {result.OperatingSystem}/{result.Architecture}: {assetLabel}\n\nDownload now → {_updateDownloadUrl}");
        }
        else if (result.IsUpdateAvailable && !result.IsCompatibleAssetAvailable)
        {
            // A newer version exists, but no asset matches this OS/architecture.
            // Never offer a download for a different platform — open the release
            // page instead so the user can decide manually.
            _updateDownloadUrl = null;
            UpdateNotice = L.T($"⬆ Update verfügbar: v{result.LatestVersion} (kein passendes Paket)",
                                $"⬆ Update available: v{result.LatestVersion} (no compatible package)");
            foreach (var cmd in _allCommands) cmd.RaiseCanExecuteChanged();

            if (!silent)
            {
                Log = L.T(
                    $"Neue Version v{result.LatestVersion} gefunden, aber für {result.OperatingSystem}/{result.Architecture} " +
                    "ist in diesem Release derzeit kein passendes Paket verfügbar. Die Release-Seite wurde geöffnet.",
                    $"New version v{result.LatestVersion} found, but no compatible package is currently available for " +
                    $"{result.OperatingSystem}/{result.Architecture}. The release page was opened.");
                TryOpenUrl(result.ReleasePageUrl);
            }
        }
        else if (!silent)
        {
            UpdateNotice = string.Empty;
            Log = string.IsNullOrEmpty(result.ErrorMessage)
                ? L.T($"App ist aktuell (v{result.CurrentVersion}).", $"App is up to date (v{result.CurrentVersion}).")
                : L.T($"Update-Check: {result.ErrorMessage}", $"Update check: {result.ErrorMessage}");
        }
    }

    private void TryOpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* best-effort only; the message already told the user the release URL */ }
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
            Log = L.T($"Download konnte nicht geöffnet werden: {ex.Message}\n\n{_updateDownloadUrl}",
                      $"Could not open download: {ex.Message}\n\n{_updateDownloadUrl}");
        }
    }

    /// <summary>
    /// Re-checks whether Inno Setup 6 is installed and updates the visibility
    /// of the proactive "Install Inno Setup" button accordingly. Called at
    /// startup, after every environment check, and after every "Create
    /// Installer" run — so the button disappears on its own once Inno Setup
    /// has been installed, without requiring an app restart.
    /// </summary>
    private void RefreshInnoSetupAvailability()
    {
        ShowInstallInnoSetup = !_installerBuild.IsInnoSetupInstalled;
    }

    /// <summary>
    /// Re-checks <c>.git/index.lock</c> for the currently selected project (filesystem-only,
    /// no process spawned) and updates the visibility of the "Verwaiste Git-Sperre sicher
    /// entfernen" button. Called on every environment check and after every write operation,
    /// so the button appears/disappears on its own without requiring a restart.
    /// </summary>
    private void RefreshGitLockAvailability()
    {
        if (string.IsNullOrWhiteSpace(LocalPath))
        {
            CanRemoveOrphanedGitLock = false;
            return;
        }

        var check = _git.CheckLockStatus(LocalPath);
        CanRemoveOrphanedGitLock = check.Status == GitLockStatus.OrphanedRemovable;

        // A detected lock or interrupted state must never be silently contradicted by
        // an unrelated "Alle kritischen Checks bestanden. Push möglich." text further
        // up (from SyncPreflightService/EnvironmentCheckService, neither of which know
        // anything about .git/index.lock). An earlier fix appended a note to the *end*
        // of Log, which turned out to be easy to miss in practice: the log view doesn't
        // auto-scroll down, so the warning could sit below the visible area while the
        // stale success line stayed on screen. Prepending a clear banner above
        // everything else fixes that regardless of scroll position.
        var banner = BuildLockBanner(check);
        if (!string.IsNullOrEmpty(banner))
        {
            // With a banner now shown above it, SyncPreflightService's unconditional
            // "Alle kritischen Checks bestanden. Push möglich." reads as a direct
            // contradiction even though the banner already relativizes it. Soften that
            // one specific line (a plain, no-op string replace for any other log shape,
            // e.g. the no-project EnvironmentCheckService branch, which never contains it).
            Log = Log.Replace(
                "→ Alle kritischen Checks bestanden. Push möglich.",
                "✓ Basisprüfung erfolgreich. Zusätzliche Warnungen siehe oben.");

            Log = banner + "\n\n———\n\n" + Log;
        }
    }

    /// <summary>Builds a top-of-log banner describing a detected interrupted git
    /// operation and/or index.lock state, or <c>string.Empty</c> if everything is clear.</summary>
    private string BuildLockBanner(GitLockCheckResult check)
    {
        var parts = new List<string>();

        if (check.InterruptedState is not null)
            parts.Add("⚠ " + check.InterruptedState.Message);

        switch (check.Status)
        {
            case GitLockStatus.OrphanedRemovable:
                parts.Add(L.T(
                    "🔓 Verwaiste Git-Sperre erkannt.\n\n" +
                    "Der Repository-Zustand ist grundsätzlich gültig, jedoch wurde eine verwaiste " +
                    ".git/index.lock-Datei gefunden.\n\n" +
                    "Die Sperre kann über „Verwaiste Git-Sperre sicher entfernen“ entfernt werden. " +
                    "Push/Pull sollte erst danach ausgeführt werden.",
                    "🔓 Orphaned Git lock detected.\n\n" +
                    "The repository state is otherwise valid, but an orphaned .git/index.lock file " +
                    "was found.\n\n" +
                    "The lock can be removed via \"Safely remove orphaned Git lock\". " +
                    "Pull/Push should only be run afterwards."));
                break;

            case GitLockStatus.ActiveProcessDetected:
                parts.Add(L.T(
                    "⏳ Git-Sperre erkannt (.git/index.lock) – aktiver Git-Prozess möglich.\n\n" + check.Message,
                    "⏳ Git lock detected (.git/index.lock) – an active git process may be involved.\n\n" + check.Message));
                break;

            case GitLockStatus.RepositoryInvalidAfterRemoval:
            case GitLockStatus.RemovalFailed:
                parts.Add("⚠ " + check.Message);
                break;
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Manual, user-triggered removal of a verified-orphaned <c>.git/index.lock</c>.
    /// Re-verifies immediately before deleting (the check-then-act race is closed inside
    /// GitLockGuard) and refuses if an active git process is detected or the lock no
    /// longer looks orphaned. Serializes against any other write operation on this repo.
    /// </summary>
    private async Task RemoveOrphanedGitLockAsync()
    {
        await Busy(async () =>
        {
            if (string.IsNullOrWhiteSpace(LocalPath))
            {
                Log = L.T("Kein lokaler Ordner ausgewählt.", "No local folder selected.");
                return;
            }

            var result = await _git.RemoveOrphanedGitLockAsync(LocalPath);
            Log = result.Status switch
            {
                GitLockStatus.RemovedSuccessfully =>
                    L.T("✅ ", "✅ ") + result.Message,
                GitLockStatus.ActiveProcessDetected =>
                    L.T("⏳ ", "⏳ ") + result.Message,
                GitLockStatus.RepositoryInvalidAfterRemoval =>
                    L.T("⚠️ ", "⚠️ ") + result.Message,
                _ =>
                    L.T("❌ ", "❌ ") + result.Message,
            };

            RefreshGitLockAvailability();
        });
    }

    /// <summary>
    /// Opens the official Inno Setup download page in the user's default
    /// browser. Never downloads or executes an installer silently — the user
    /// stays in control of installing third-party software on their machine.
    /// </summary>
    private void OpenInnoSetupDownload()
    {
        try
        {
            Process.Start(new ProcessStartInfo(InstallerBuildService.InnoSetupDownloadUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log = L.T($"Download-Seite konnte nicht geöffnet werden: {ex.Message}\n\n{InstallerBuildService.InnoSetupDownloadUrl}",
                      $"Could not open download page: {ex.Message}\n\n{InstallerBuildService.InnoSetupDownloadUrl}");
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
        sb.AppendLine(L.T("── Fehleranalyse ────────────────────────────", "── Error analysis ────────────────────────────"));
        sb.AppendLine($"{L.T("Ursache:", "Cause:")} {info.Message(L.IsEnglish)}");
        var hint = info.HintText(L.IsEnglish);
        if (!string.IsNullOrWhiteSpace(hint))
            sb.AppendLine($"{L.T("Lösung: ", "Solution:")} {hint}");
        sb.AppendLine(L.T("(Technische Rohdaten oben unverändert erhalten.)",
                           "(Raw technical output preserved unchanged above.)"));
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
