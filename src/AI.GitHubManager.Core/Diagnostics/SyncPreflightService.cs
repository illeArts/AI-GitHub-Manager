using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Projects;
using AI.GitHubManager.Core.Remote;

namespace AI.GitHubManager.Core.Diagnostics;

/// <summary>
/// Runs all pre-push checks for a given project and returns a structured result.
/// Checks (in order):
///   1. Git installed
///   2. GitHub CLI installed
///   3. GitHub authenticated (full diagnosis — distinguishes "not logged in" from
///      "a bad GH_TOKEN/GITHUB_TOKEN is hiding a valid keyring login")
///   4. Local folder exists and is a Git repository
///   5. Remote origin is set, credential-free, and matches the project's configured URL
///   6. Active branch detected
///   7. No uncommitted changes (warning only)
///   8. No merge conflict markers (.git/MERGE_HEAD)
///   9. Workflow files present → workflow scope check
/// </summary>
public sealed class SyncPreflightService
{
    private readonly GitService       _git;
    private readonly GitHubCliService _gh;

    public SyncPreflightService(GitService git, GitHubCliService gh)
    {
        _git = git;
        _gh  = gh;
    }

    public async Task<SyncPreflightResult> CheckAsync(
        ManagedProject project,
        CancellationToken cancellationToken = default)
    {
        var items = new List<PreflightItem>();

        // ── 1. Git installed ─────────────────────────────────────────────────
        var gitVer = await Safe(() => _git.VersionAsync());
        items.Add(gitVer.ok
            ? Ok("Git",      gitVer.output)
            : Error("Git",   "Git nicht gefunden. Bitte installieren."));

        // ── 2. GitHub CLI installed ──────────────────────────────────────────
        var ghVer = await Safe(() => _gh.VersionAsync());
        items.Add(ghVer.ok
            ? Ok("GitHub CLI",    ghVer.output)
            : Error("GitHub CLI", "GitHub CLI (gh) nicht gefunden. Bitte installieren."));

        // Resolve local path for this platform (needed before the scope check,
        // since workflow-file presence determines which scopes are required).
        var localPath = ResolveLocalPath(project);
        bool needsWorkflowScope = !string.IsNullOrWhiteSpace(localPath)
                                   && Directory.Exists(localPath)
                                   && HasWorkflowFiles(localPath);
        var requiredScopes = needsWorkflowScope
            ? AuthenticationDiagnosticService.WorkflowScopes
            : AuthenticationDiagnosticService.DefaultScopes;

        // ── 3. GitHub authenticated (structured diagnosis) ──────────────────
        var authService = new AuthenticationDiagnosticService(_gh);
        var auth = await authService.DiagnoseAsync(requiredScopes, cancellationToken);
        items.Add(BuildAuthItem(auth));

        // ── 4. Local folder / Git repository ────────────────────────────────
        if (string.IsNullOrWhiteSpace(localPath))
        {
            items.Add(Error("Lokaler Ordner", "Kein lokaler Pfad konfiguriert. Bitte Ordner auswählen."));
            return new SyncPreflightResult(items, authentication: auth);
        }

        if (!Directory.Exists(localPath))
        {
            items.Add(Error("Lokaler Ordner", $"Ordner nicht gefunden: {localPath}"));
            return new SyncPreflightResult(items, authentication: auth);
        }

        var status = await _git.GetStatusAsync(localPath, cancellationToken);
        if (!status.IsRepository)
        {
            items.Add(Error("Git-Repository", status.ErrorMessage ?? "Der Ordner ist kein Git-Repository."));
            return new SyncPreflightResult(items, remoteOrigin: string.Empty, authentication: auth);
        }

        items.Add(Ok("Git-Repository", "Gültig."));

        var branch       = status.Branch;
        var remoteOrigin = status.RemoteOrigin;
        var remoteInfo   = RemoteUrlNormalizer.Parse(remoteOrigin);

        // ── 5. Remote origin: presence, safety, and equivalence ─────────────
        if (string.IsNullOrWhiteSpace(remoteOrigin))
        {
            items.Add(Error("Remote origin", "Remote 'origin' ist nicht gesetzt."));
        }
        else if (RemoteUrlNormalizer.ContainsCredentials(remoteOrigin) || RemoteUrlNormalizer.ContainsPlaceholderToken(remoteOrigin))
        {
            items.Add(new PreflightItem(
                "Remote origin",
                PreflightSeverity.Warning,
                "⚠️ Die Remote-URL enthält Zugangsdaten. Dies ist unsicher.",
                CanAutoRepair: true,
                RepairActionId: "sanitize-remote"));
        }
        else if (!string.IsNullOrWhiteSpace(project.RemoteUrl))
        {
            items.Add(RemoteUrlNormalizer.AreEquivalent(remoteOrigin, project.RemoteUrl)
                ? Ok("Remote origin", remoteOrigin)
                : Warn("Remote origin", $"Konfiguriert: '{project.RemoteUrl}', tatsächlich: '{remoteOrigin}'. Bitte 'Remote setzen' ausführen."));
        }
        else
        {
            items.Add(Ok("Remote origin", remoteOrigin));
        }

        // ── 6. Active branch ─────────────────────────────────────────────────
        items.Add(string.IsNullOrWhiteSpace(branch)
            ? Warn("Branch", "Kein aktiver Branch erkannt (leeres Repo?)")
            : Ok("Branch", branch));

        // ── 7. Uncommitted changes (warning, not error) ──────────────────────
        items.Add(status.ChangedFiles.Count > 0
            ? Warn("Uncommitted Changes", $"{status.ChangedFiles.Count} Datei(en) haben ungespeicherte Änderungen. Werden beim Push committet.")
            : Ok("Uncommitted Changes", "Keine."));

        // ── 8. Merge conflict ────────────────────────────────────────────────
        var mergeHeadPath = Path.Combine(localPath, ".git", "MERGE_HEAD");
        items.Add(File.Exists(mergeHeadPath)
            ? Error("Merge-Konflikt", "MERGE_HEAD vorhanden – ein Merge ist nicht abgeschlossen. Bitte Konflikt lösen.")
            : Ok("Merge-Konflikt", "Kein aktiver Konflikt."));

        // ── 9. Workflow scope ────────────────────────────────────────────────
        if (needsWorkflowScope)
        {
            bool workflowScopeMissing = auth.State == AuthenticationState.MissingRequiredScopes
                                         && auth.MissingScopes.Contains("workflow", StringComparer.OrdinalIgnoreCase);
            items.Add(workflowScopeMissing
                ? Error("Workflow-Scope", "workflow-Files gefunden, aber workflow-Scope fehlt. Bitte 'GitHub Rechte: repo + workflow' ausführen.")
                : Ok("Workflow-Scope", "workflow-Scope vorhanden."));
        }

        return new SyncPreflightResult(items, branch, remoteOrigin, authentication: auth, remoteInfo: remoteInfo);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PreflightItem BuildAuthItem(AuthenticationDiagnosis auth) => auth.State switch
    {
        AuthenticationState.EnvironmentTokenOverridesValidKeyring => new PreflightItem(
            "Authentifizierung", PreflightSeverity.Error,
            $"{auth.Summary} Automatische Reparatur verfügbar.",
            CanAutoRepair: true, RepairActionId: "repair-environment-token"),

        AuthenticationState.InvalidEnvironmentToken => new PreflightItem(
            "Authentifizierung", PreflightSeverity.Error,
            $"{auth.Summary} Bitte 'GitHub Login' ausführen."),

        AuthenticationState.MissingRequiredScopes => new PreflightItem(
            "Authentifizierung", PreflightSeverity.Error, auth.Summary),

        AuthenticationState.GitHubCliUnavailable => new PreflightItem(
            "Authentifizierung", PreflightSeverity.Error, auth.Summary),

        AuthenticationState.AuthenticationCheckFailed => new PreflightItem(
            "Authentifizierung", PreflightSeverity.Error, auth.Summary),

        AuthenticationState.NotAuthenticated => new PreflightItem(
            "Authentifizierung", PreflightSeverity.Error,
            "Nicht bei GitHub eingeloggt. Bitte 'GitHub Login' ausführen."),

        _ => new PreflightItem("Authentifizierung", PreflightSeverity.Ok, auth.Summary)
    };

    private static string ResolveLocalPath(ManagedProject project)
    {
        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(project.WindowsPath))
            return project.WindowsPath;
        if (OperatingSystem.IsMacOS() && !string.IsNullOrWhiteSpace(project.MacPath))
            return project.MacPath;
        if (!string.IsNullOrWhiteSpace(project.LinuxPath))
            return project.LinuxPath;
        return string.Empty;
    }

    private static bool HasWorkflowFiles(string localPath)
    {
        var workflowDir = Path.Combine(localPath, ".github", "workflows");
        return Directory.Exists(workflowDir) &&
               Directory.EnumerateFiles(workflowDir, "*.yml").Any();
    }

    private static async Task<(bool ok, string output)> Safe(
        Func<Task<Process.CommandResult>> action)
    {
        try
        {
            var r = await action();
            return (r.Success, r.CombinedOutput.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static PreflightItem Ok   (string label, string msg) => new(label, PreflightSeverity.Ok,      msg);
    private static PreflightItem Warn (string label, string msg) => new(label, PreflightSeverity.Warning, msg);
    private static PreflightItem Error(string label, string msg) => new(label, PreflightSeverity.Error,   msg);
}
