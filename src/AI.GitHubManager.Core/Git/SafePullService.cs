using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Performs a "safe pull": before touching the working tree with a real
/// <c>git pull</c>, any local changes (tracked and untracked) are backed up
/// into a uniquely-tagged git stash. The pull only ever runs as
/// <c>git pull --ff-only</c> — never an automatic merge or rebase. After a
/// successful pull the backup is re-applied with <c>git stash apply</c>
/// (never <c>stash pop</c>), and only dropped once the restore is verified.
///
/// Safety invariants (see AI GitHub Manager issue #4):
///  - Never <c>git reset --hard</c>.
///  - Never deletes local files.
///  - Never force-pushes (this class doesn't push at all).
///  - Never blindly assumes <c>stash@{0}</c> — the exact stash created by
///    this run is located via its unique backup message after creation.
///  - Never touches a pre-existing user stash.
///  - On any ambiguity, aborts instead of guessing.
/// </summary>
public sealed class SafePullService
{
    private const string BackupTag = "AI GitHub Manager auto-backup";

    private readonly CommandRunner _runner;
    private readonly RepositoryLockService _lockService;
    private readonly GitLockGuard _lockGuard;

    public SafePullService(CommandRunner runner) : this(runner, RepositoryLockService.Shared, new GitLockGuard(runner)) { }

    /// <summary>Test/DI seam: same shared lock service as <see cref="GitService"/> by
    /// default, so a safe pull and a concurrent commit+push on the same repository
    /// still serialize against each other even though they're different classes.</summary>
    public SafePullService(CommandRunner runner, RepositoryLockService lockService, GitLockGuard lockGuard)
    {
        _runner = runner;
        _lockService = lockService;
        _lockGuard = lockGuard;
    }

    /// <param name="repositoryPath">Local working copy path.</param>
    /// <param name="warnOnlyOnLocalChanges">
    /// When true (the "only warn and abort" setting), local changes cause the
    /// pull to be aborted immediately instead of creating a protective backup.
    /// </param>
    public async Task<SafePullResult> PullAsync(
        string repositoryPath,
        bool warnOnlyOnLocalChanges,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return SafePullResult.NotARepositoryResult("Lokaler Ordner existiert nicht.");

        using var _ = await _lockService.AcquireAsync(repositoryPath, cancellationToken);
        var lockGuardResult = await _lockGuard.EnsureWritableAsync(repositoryPath, cancellationToken);
        if (lockGuardResult is not null)
            return SafePullResult.WriteBlockedByLockGuardResult(lockGuardResult.CombinedOutput);

        var insideCheck = await Git(repositoryPath, ["rev-parse", "--is-inside-work-tree"], cancellationToken);
        if (!insideCheck.Success)
            return SafePullResult.NotARepositoryResult("Der Ordner ist kein Git-Repository.");

        var status = await Git(repositoryPath, ["status", "--porcelain"], cancellationToken);
        if (!status.Success)
            return SafePullResult.NotARepositoryResult("Git-Status konnte nicht gelesen werden:\n" + status.CombinedOutput);

        var changedFiles = status.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToArray();

        var isClean = changedFiles.Length == 0;

        if (isClean)
        {
            var pull = await Git(repositoryPath, ["pull", "--ff-only"], cancellationToken);
            if (pull.Success)
                return SafePullResult.CleanPull(pull.CombinedOutput);

            if (IsFastForwardImpossible(pull))
                return SafePullResult.FastForwardNotPossibleResult(pull.CombinedOutput);

            return SafePullResult.CleanPullFailed(pull.CombinedOutput);
        }

        if (warnOnlyOnLocalChanges)
            return SafePullResult.AbortedDueToLocalChanges(changedFiles);

        // ── 1. Create a uniquely tagged protective backup ──────────────────────
        var uniqueId = Guid.NewGuid().ToString("N");
        var backupMessage = $"{BackupTag} {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} {uniqueId}";

        var stashPush = await Git(repositoryPath, ["stash", "push", "--include-untracked", "--message", backupMessage], cancellationToken);
        if (!stashPush.Success)
            return SafePullResult.BackupFailed(stashPush.CombinedOutput);

        // "No local changes to save" can happen in rare races (e.g. ignored-only
        // changes) — treat as backup failure rather than guessing.
        var located = await FindStashByUniqueIdAsync(repositoryPath, uniqueId, cancellationToken);
        if (located is null)
            return SafePullResult.BackupFailed(
                "Die Schutzsicherung wurde erstellt, konnte aber danach nicht eindeutig identifiziert werden. " +
                "Aus Sicherheitsgründen wurde der Pull nicht ausgeführt.\n\n" + stashPush.CombinedOutput);

        var (stashHash, stashRef) = located.Value;

        // ── 2. Fast-forward-only pull, never an automatic merge/rebase ─────────
        var pullResult = await Git(repositoryPath, ["pull", "--ff-only"], cancellationToken);

        if (!pullResult.Success)
        {
            // ── 3a. Pull failed → restore exactly what we backed up ────────────
            var restore = await RestoreAsync(repositoryPath, stashHash, backupMessage, cancellationToken);
            return restore.Applied
                ? SafePullResult.FailedAndRestored(pullResult.CombinedOutput, backupMessage)
                : restore.HasConflicts
                    ? SafePullResult.RestoreConflictResult(stashHash, backupMessage, restore.ConflictFiles)
                    : SafePullResult.RestoreFailedResult(stashHash, backupMessage, restore.FailureReason ?? "Unbekannter Fehler beim Wiederherstellen.");
        }

        // ── 3b. Pull succeeded → re-apply the backup, drop only on full success ─
        var reapply = await RestoreAsync(repositoryPath, stashHash, backupMessage, cancellationToken);
        if (reapply.Applied)
            return SafePullResult.SucceededWithRestore(pullResult.CombinedOutput, backupMessage);

        if (reapply.HasConflicts)
            return SafePullResult.RestoreConflictResult(stashHash, backupMessage, reapply.ConflictFiles);

        return SafePullResult.RestoreFailedResult(stashHash, backupMessage, reapply.FailureReason ?? "Unbekannter Fehler beim Wiederherstellen.");
    }

    /// <summary>
    /// Applies the given stash (never pop) and, only if the apply completed
    /// without conflicts, drops that exact stash entry. Re-resolves the current
    /// stash@{N} reference right before dropping, since the index may have
    /// shifted since the stash was created.
    /// </summary>
    private async Task<(bool Applied, bool HasConflicts, IReadOnlyList<string> ConflictFiles, string? FailureReason)> RestoreAsync(
        string repositoryPath, string stashHash, string backupMessage, CancellationToken cancellationToken)
    {
        var apply = await Git(repositoryPath, ["stash", "apply", stashHash], cancellationToken);

        // `git stash apply` exits non-zero both for hard failures (e.g. a
        // corrupt stash) AND for textual merge conflicts (it still writes
        // conflict markers into the working tree in that case). We must tell
        // those apart: if conflict markers are actually present, this is a
        // content conflict, not an outright failure.
        var conflictFiles = await GetConflictFilesAsync(repositoryPath, cancellationToken);
        if (conflictFiles.Count > 0)
        {
            // fail-closed: keep the backup, do not drop, do not attempt anything else.
            return (false, true, conflictFiles, null);
        }

        if (!apply.Success)
        {
            return (false, false, Array.Empty<string>(), apply.CombinedOutput);
        }

        // Restore verified clean — now (and only now) drop the exact stash entry.
        var currentRef = await FindStashRefByUniqueIdAsync(repositoryPath, backupMessage, cancellationToken);
        if (currentRef is null)
        {
            // Applied fine, but we can't safely identify the ref to drop anymore.
            // Leaving an extra stash around is safe; silently deleting the wrong
            // one is not.
            return (true, false, Array.Empty<string>(), null);
        }

        var drop = await Git(repositoryPath, ["stash", "drop", currentRef], cancellationToken);
        // Even if drop fails, the working tree restore itself already succeeded —
        // report success; a leftover backup stash is a harmless side effect.
        _ = drop;
        return (true, false, Array.Empty<string>(), null);
    }

    private async Task<IReadOnlyList<string>> GetConflictFilesAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var status = await Git(repositoryPath, ["status", "--porcelain"], cancellationToken);
        if (!status.Success) return Array.Empty<string>();

        return status.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Length >= 2 && IsConflictMarker(line[..2]))
            .Select(line => line[3..].Trim())
            .ToArray();
    }

    private static bool IsConflictMarker(string twoLetterStatus) => twoLetterStatus is
        "UU" or "AA" or "DD" or "AU" or "UA" or "UD" or "DU";

    /// <summary>Finds the (commit hash, stash ref) pair for the stash carrying our unique id.</summary>
    private async Task<(string Hash, string Ref)?> FindStashByUniqueIdAsync(string repositoryPath, string uniqueId, CancellationToken cancellationToken)
    {
        var list = await Git(repositoryPath, ["stash", "list", "--format=%H%x09%gd%x09%gs"], cancellationToken);
        if (!list.Success) return null;

        foreach (var line in list.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;
            if (parts[2].Contains(uniqueId, StringComparison.Ordinal))
                return (parts[0].Trim(), parts[1].Trim());
        }
        return null;
    }

    private async Task<string?> FindStashRefByUniqueIdAsync(string repositoryPath, string backupMessage, CancellationToken cancellationToken)
    {
        // backupMessage always contains the unique id as its last token.
        var uniqueId = backupMessage.Split(' ').LastOrDefault() ?? backupMessage;
        var located = await FindStashByUniqueIdAsync(repositoryPath, uniqueId, cancellationToken);
        return located?.Ref;
    }

    private static bool IsFastForwardImpossible(CommandResult result) =>
        result.CombinedOutput.Contains("Not possible to fast-forward", StringComparison.OrdinalIgnoreCase) ||
        result.CombinedOutput.Contains("diverged", StringComparison.OrdinalIgnoreCase);

    private Task<CommandResult> Git(string workingDirectory, IEnumerable<string> args, CancellationToken cancellationToken) =>
        _runner.RunAsync("git", args, workingDirectory, cancellationToken);
}
