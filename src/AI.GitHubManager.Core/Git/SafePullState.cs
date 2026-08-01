namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Every possible outcome of a <see cref="SafePullService"/> run. Exhaustive
/// by design — the UI must handle every state explicitly instead of falling
/// back to generic console-text handling.
/// </summary>
public enum SafePullState
{
    /// <summary>Worktree was clean; a plain fast-forward pull succeeded.</summary>
    CleanPullSucceeded,

    /// <summary>Local changes were found and a protective stash backup was created.
    /// Intermediate state, reported alongside a terminal state.</summary>
    BackupCreated,

    /// <summary>Pull failed; local state (including the backup) was fully restored,
    /// nothing was lost. The original pull error is reported.</summary>
    PullFailedAndRestored,

    /// <summary>Pull succeeded and the protective backup was re-applied cleanly;
    /// the backup stash was then dropped.</summary>
    PullSucceededAndChangesRestored,

    /// <summary>Pull succeeded, but re-applying the backup produced conflicts.
    /// The backup is deliberately kept; no further automatic write happens.</summary>
    RestoreConflict,

    /// <summary>Local changes were detected but the protective backup itself
    /// could not be created — the pull was NOT attempted.</summary>
    BackupCreationFailed,

    /// <summary>Pull failed and restoring the pre-pull state also failed —
    /// worst case, surfaced loudly so the user can intervene manually.
    /// The backup stash (if any) is deliberately kept.</summary>
    RestoreFailed,

    /// <summary>User configured "warn only" mode and local changes were found —
    /// the pull was intentionally not attempted.</summary>
    AbortedDueToLocalChanges,

    /// <summary>Fast-forward-only pull is not possible (branches diverged) even
    /// though the worktree was clean; no automatic merge/rebase was attempted.</summary>
    FastForwardNotPossible,

    /// <summary>The path is not a valid git repository / other precondition failed.</summary>
    NotARepository,
}
