using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Projects;

namespace AI.GitHubManager.Core.Diagnostics;

/// <summary>
/// Runs all pre-push checks for a given project and returns a structured result.
/// Checks (in order):
///   1. Git installed
///   2. GitHub CLI installed
///   3. GitHub authenticated
///   4. Local folder exists and is a Git repository
///   5. Remote origin is set and matches the project's configured URL
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

        // ── 3. GitHub authenticated ──────────────────────────────────────────
        var auth = await Safe(() => _gh.AuthStatusAsync());
        items.Add(auth.ok
            ? Ok("Authentifizierung",    "Eingeloggt.")
            : Error("Authentifizierung", "Nicht bei GitHub eingeloggt. Bitte 'GitHub Login' ausführen."));

        // Resolve local path for this platform
        var localPath = ResolveLocalPath(project);

        // ── 4. Local folder / Git repository ────────────────────────────────
        if (string.IsNullOrWhiteSpace(localPath))
        {
            items.Add(Error("Lokaler Ordner", "Kein lokaler Pfad konfiguriert. Bitte Ordner auswählen."));
            return new SyncPreflightResult(items);
        }

        if (!Directory.Exists(localPath))
        {
            items.Add(Error("Lokaler Ordner", $"Ordner nicht gefunden: {localPath}"));
            return new SyncPreflightResult(items);
        }

        var status = await _git.GetStatusAsync(localPath, cancellationToken);
        if (!status.IsRepository)
        {
            items.Add(Error("Git-Repository", status.ErrorMessage ?? "Der Ordner ist kein Git-Repository."));
            return new SyncPreflightResult(items, remoteOrigin: string.Empty);
        }

        items.Add(Ok("Git-Repository", "Gültig."));

        var branch      = status.Branch;
        var remoteOrigin = status.RemoteOrigin;

        // ── 5. Remote origin matches project URL ─────────────────────────────
        if (string.IsNullOrWhiteSpace(remoteOrigin))
        {
            items.Add(Error("Remote origin", "Remote 'origin' ist nicht gesetzt."));
        }
        else if (!string.IsNullOrWhiteSpace(project.RemoteUrl))
        {
            var normalLocal   = NormalizeUrl(remoteOrigin);
            var normalProject = NormalizeUrl(project.RemoteUrl);
            items.Add(normalLocal.Equals(normalProject, StringComparison.OrdinalIgnoreCase)
                ? Ok("Remote origin",      remoteOrigin)
                : Warn("Remote origin",    $"Konfiguriert: '{project.RemoteUrl}', tatsächlich: '{remoteOrigin}'. Bitte 'Remote setzen' ausführen."));
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
        if (status.ChangedFiles.Count > 0)
        {
            items.Add(Warn("Uncommitted Changes",
                $"{status.ChangedFiles.Count} Datei(en) haben ungespeicherte Änderungen. Werden beim Push committet."));
        }
        else
        {
            items.Add(Ok("Uncommitted Changes", "Keine."));
        }

        // ── 8. Merge conflict ────────────────────────────────────────────────
        var mergeHeadPath = Path.Combine(localPath, ".git", "MERGE_HEAD");
        if (File.Exists(mergeHeadPath))
        {
            items.Add(Error("Merge-Konflikt",
                "MERGE_HEAD vorhanden – ein Merge ist nicht abgeschlossen. Bitte Konflikt lösen."));
        }
        else
        {
            items.Add(Ok("Merge-Konflikt", "Kein aktiver Konflikt."));
        }

        // ── 9. Workflow scope ────────────────────────────────────────────────
        bool hasWorkflowFiles = HasWorkflowFiles(localPath);
        if (hasWorkflowFiles)
        {
            // `gh auth status` output contains "workflow" in the scopes line when granted
            bool workflowScope = auth.output.Contains("workflow", StringComparison.OrdinalIgnoreCase);
            items.Add(workflowScope
                ? Ok("Workflow-Scope",    "workflow-Scope vorhanden.")
                : Error("Workflow-Scope", "workflow-Files gefunden, aber workflow-Scope fehlt. Bitte 'GitHub Rechte: repo + workflow' ausführen."));
        }
        // (no check item when no workflow files — not relevant)

        return new SyncPreflightResult(items, branch, remoteOrigin);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Strip trailing .git and trailing slash for URL comparison.
    /// </summary>
    private static string NormalizeUrl(string url)
        => url.TrimEnd('/').TrimEnd(['.', 'g', 'i', 't']).TrimEnd('/');

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
