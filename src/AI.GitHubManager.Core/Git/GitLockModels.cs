namespace AI.GitHubManager.Core.Git;

/// <summary>Kind of interrupted git operation found in a repository's <c>.git</c> directory.
/// Never auto-cleaned — the app only ever reports these so the user can resolve or abort
/// them deliberately (e.g. <c>git merge --abort</c>, <c>git rebase --abort</c>).</summary>
public enum GitInterruptedStateKind
{
    Merge,
    CherryPick,
    Rebase,
    Bisect,
}

/// <summary>A structured, never-auto-cleaned interrupted-operation marker (MERGE_HEAD,
/// CHERRY_PICK_HEAD, REBASE_HEAD/rebase-merge/rebase-apply, BISECT_LOG).</summary>
public sealed record GitInterruptedState(GitInterruptedStateKind Kind, string Message);

/// <summary>Outcome of checking (and possibly acting on) <c>.git/index.lock</c>.</summary>
public enum GitLockStatus
{
    /// <summary>No <c>index.lock</c> file present — nothing to do.</summary>
    NoLockPresent,

    /// <summary>A lock file is present and an active git process was detected
    /// (or could not be safely ruled out). The lock is left untouched.</summary>
    ActiveProcessDetected,

    /// <summary>A lock file is present, no active git process was detected, and the
    /// lock file is old enough to be treated as orphaned. Safe to remove.</summary>
    OrphanedRemovable,

    /// <summary>An orphaned lock was removed and <c>git status</c> confirmed a valid
    /// repository state afterwards.</summary>
    RemovedSuccessfully,

    /// <summary>Removing the lock file itself failed (e.g. permissions).</summary>
    RemovalFailed,

    /// <summary>The lock was removed, but <c>git status</c> afterwards did not report
    /// a valid repository state. The user is told to check the repository manually.</summary>
    RepositoryInvalidAfterRemoval,
}

/// <summary>Result of a lock check or removal attempt. <see cref="InterruptedState"/> is
/// populated independently of <see cref="Status"/> whenever an unrelated interrupted
/// operation (merge/rebase/cherry-pick/bisect) is also detected, since that must never
/// be silently cleaned up alongside an index.lock fix.</summary>
public sealed record GitLockCheckResult(
    GitLockStatus Status,
    string Message,
    GitInterruptedState? InterruptedState,
    string? LockFilePath);
