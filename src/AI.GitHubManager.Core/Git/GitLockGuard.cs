using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Safe handling of <c>.git/index.lock</c> plus detection of unrelated
/// interrupted-operation markers (MERGE_HEAD, CHERRY_PICK_HEAD, REBASE_HEAD,
/// BISECT_LOG). This class never deletes anything blindly:
///
/// 1. Interrupted-operation markers are only ever reported, never removed.
/// 2. <c>index.lock</c> is only ever removed when (a) no active git process
///    could be detected for this repository, and (b) the lock file is older
///    than <see cref="MinimumOrphanAge"/> — both re-verified immediately
///    before deletion to close the race between check and act.
/// 3. After removing a lock, <c>git status</c> must succeed before the
///    caller is told it's safe to proceed.
/// </summary>
public sealed class GitLockGuard
{
    /// <summary>
    /// A lock file must be at least this old before it is even considered for
    /// automatic removal. Guards against the (rare) case where an external
    /// process just created the lock a moment ago and hasn't shown up in a
    /// process-list scan yet.
    /// </summary>
    private static readonly TimeSpan MinimumOrphanAge = TimeSpan.FromSeconds(5);

    private readonly CommandRunner _runner;
    private readonly IGitProcessDetector _processDetector;

    public GitLockGuard(CommandRunner runner, IGitProcessDetector? processDetector = null)
    {
        _runner = runner;
        _processDetector = processDetector ?? GitProcessDetectorFactory.CreateDefault();
    }

    /// <summary>
    /// Detects an unrelated interrupted git operation (merge/cherry-pick/rebase/bisect).
    /// These are structurally different from a stale index.lock and must never be
    /// auto-cleaned alongside it — the user has to resolve or abort them deliberately.
    /// </summary>
    public GitInterruptedState? DetectInterruptedState(string repositoryPath)
    {
        var gitDir = Path.Combine(repositoryPath, ".git");
        if (!Directory.Exists(gitDir)) return null;

        if (File.Exists(Path.Combine(gitDir, "MERGE_HEAD")))
            return new GitInterruptedState(GitInterruptedStateKind.Merge,
                "Ein Merge ist unterbrochen (MERGE_HEAD vorhanden). Bitte manuell abschließen oder abbrechen (git merge --abort). " +
                "Dieser Zustand wird nicht automatisch bereinigt.");

        if (File.Exists(Path.Combine(gitDir, "CHERRY_PICK_HEAD")))
            return new GitInterruptedState(GitInterruptedStateKind.CherryPick,
                "Ein Cherry-Pick ist unterbrochen (CHERRY_PICK_HEAD vorhanden). Bitte manuell abschließen oder abbrechen " +
                "(git cherry-pick --abort). Dieser Zustand wird nicht automatisch bereinigt.");

        if (File.Exists(Path.Combine(gitDir, "REBASE_HEAD")) ||
            Directory.Exists(Path.Combine(gitDir, "rebase-merge")) ||
            Directory.Exists(Path.Combine(gitDir, "rebase-apply")))
            return new GitInterruptedState(GitInterruptedStateKind.Rebase,
                "Ein Rebase ist unterbrochen. Bitte manuell abschließen oder abbrechen (git rebase --abort). " +
                "Dieser Zustand wird nicht automatisch bereinigt.");

        if (File.Exists(Path.Combine(gitDir, "BISECT_LOG")))
            return new GitInterruptedState(GitInterruptedStateKind.Bisect,
                "Eine Bisect-Sitzung läuft (BISECT_LOG vorhanden). Bitte mit git bisect reset beenden. " +
                "Dieser Zustand wird nicht automatisch bereinigt.");

        return null;
    }

    /// <summary>Pure, synchronous, filesystem-only check — safe to call frequently
    /// (e.g. from the UI's environment check) without side effects.</summary>
    public GitLockCheckResult CheckIndexLock(string repositoryPath)
    {
        var interrupted = DetectInterruptedState(repositoryPath);

        var normalized = RepositoryLockService.NormalizeKey(repositoryPath);
        var lockPath = Path.Combine(normalized, ".git", "index.lock");

        if (!File.Exists(lockPath))
            return new GitLockCheckResult(GitLockStatus.NoLockPresent, "Keine index.lock vorhanden.", interrupted, null);

        if (_processDetector.IsGitProcessActiveFor(normalized))
            return new GitLockCheckResult(
                GitLockStatus.ActiveProcessDetected,
                "Ein aktiver Git-Prozess wurde erkannt (oder konnte nicht sicher ausgeschlossen werden). " +
                "Die Sperre bleibt bestehen, bis dieser Prozess beendet ist. Bitte kurz warten und erneut prüfen.",
                interrupted, lockPath);

        var age = SafeGetAge(lockPath);
        if (age is null || age < MinimumOrphanAge)
            return new GitLockCheckResult(
                GitLockStatus.ActiveProcessDetected,
                "Die Sperrdatei ist noch zu neu, um sicher als verwaist zu gelten. Bitte kurz warten und erneut prüfen.",
                interrupted, lockPath);

        return new GitLockCheckResult(
            GitLockStatus.OrphanedRemovable,
            "Kein aktiver Git-Prozess erkannt und die Sperre ist alt genug — sie wirkt verwaist und kann sicher entfernt werden.",
            interrupted, lockPath);
    }

    /// <summary>
    /// Re-verifies the lock is still clearly orphaned (closing the check-then-act race),
    /// removes only the single <c>index.lock</c> file, then runs <c>git status</c> and
    /// only reports success if the repository is in a valid state afterwards.
    /// </summary>
    public async Task<GitLockCheckResult> RemoveOrphanedLockAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var recheck = CheckIndexLock(repositoryPath);
        if (recheck.Status != GitLockStatus.OrphanedRemovable)
            return recheck; // refuse unless still clearly orphaned right now

        try
        {
            File.Delete(recheck.LockFilePath!);
        }
        catch (Exception ex)
        {
            return new GitLockCheckResult(
                GitLockStatus.RemovalFailed,
                $"Entfernen der verwaisten Sperre fehlgeschlagen ({ex.GetType().Name}). Die Datei wurde nicht verändert.",
                recheck.InterruptedState, recheck.LockFilePath);
        }

        var status = await _runner.RunAsync("git", ["status", "--porcelain"], repositoryPath, cancellationToken);
        if (!status.Success)
            return new GitLockCheckResult(
                GitLockStatus.RepositoryInvalidAfterRemoval,
                "Die verwaiste Sperre wurde entfernt, aber 'git status' meldet danach einen Fehler. " +
                "Bitte das Repository manuell prüfen, bevor weitere Aktionen ausgeführt werden.",
                recheck.InterruptedState, recheck.LockFilePath);

        return new GitLockCheckResult(
            GitLockStatus.RemovedSuccessfully,
            "Die verwaiste Git-Sperre wurde sicher entfernt; 'git status' zeigt danach einen gültigen Repository-Zustand.",
            recheck.InterruptedState, recheck.LockFilePath);
    }

    /// <summary>
    /// Convenience combining both checks for a write operation about to start:
    /// returns <c>null</c> when it's safe to proceed, or a <see cref="CommandResult"/>
    /// explaining why the operation was aborted (interrupted state, an active process,
    /// or a failed/incomplete orphan removal). An orphaned lock is removed automatically
    /// here since it has already passed every safety check.
    /// </summary>
    public async Task<CommandResult?> EnsureWritableAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return null; // let the caller's own repository-existence check report this

        var interrupted = DetectInterruptedState(repositoryPath);
        if (interrupted is not null)
            return new CommandResult(-1, string.Empty, "⛔ " + interrupted.Message, "git", "lock-guard");

        var check = CheckIndexLock(repositoryPath);
        switch (check.Status)
        {
            case GitLockStatus.NoLockPresent:
                return null;

            case GitLockStatus.ActiveProcessDetected:
                return new CommandResult(-1, string.Empty, "⏳ " + check.Message, "git", "lock-guard");

            case GitLockStatus.OrphanedRemovable:
                var removal = await RemoveOrphanedLockAsync(repositoryPath, cancellationToken);
                if (removal.Status == GitLockStatus.RemovedSuccessfully)
                    return null;
                return new CommandResult(-1, string.Empty, "🔒 " + removal.Message, "git", "lock-guard");

            default:
                return new CommandResult(-1, string.Empty, check.Message, "git", "lock-guard");
        }
    }

    private static TimeSpan? SafeGetAge(string path)
    {
        try { return DateTime.UtcNow - File.GetLastWriteTimeUtc(path); }
        catch { return null; }
    }
}
