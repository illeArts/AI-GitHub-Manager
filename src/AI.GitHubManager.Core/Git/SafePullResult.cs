namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Structured, UI-ready result of a <see cref="SafePullService"/> run. Always
/// carries enough information for the UI to render a specific message and the
/// correct set of actions — never just raw console text.
/// </summary>
public sealed record SafePullResult(
    SafePullState State,
    bool Success,
    string Message,
    string? PullOutput,
    string? BackupStashRef,
    string? BackupStashMessage,
    IReadOnlyList<string> ConflictFiles)
{
    public static SafePullResult CleanPull(string pullOutput) =>
        new(SafePullState.CleanPullSucceeded, true,
            "Keine lokalen Änderungen — Pull direkt ausgeführt.",
            pullOutput, null, null, Array.Empty<string>());

    public static SafePullResult SucceededWithRestore(string pullOutput, string stashMessage) =>
        new(SafePullState.PullSucceededAndChangesRestored, true,
            "Pull erfolgreich. Lokale Änderungen wurden aus der Schutzsicherung wiederhergestellt.",
            pullOutput, null, stashMessage, Array.Empty<string>());

    public static SafePullResult FailedAndRestored(string pullError, string stashMessage) =>
        new(SafePullState.PullFailedAndRestored, false,
            $"Pull fehlgeschlagen — lokaler Zustand wurde vollständig wiederhergestellt.\n\nUrsprünglicher Fehler:\n{pullError}",
            pullError, null, stashMessage, Array.Empty<string>());

    public static SafePullResult RestoreConflictResult(string stashRef, string stashMessage, IReadOnlyList<string> conflictFiles) =>
        new(SafePullState.RestoreConflict, false,
            "Pull war erfolgreich, aber die Wiederherstellung der lokalen Änderungen hat Konflikte erzeugt. " +
            "Remote-Stand und lokale Änderungen konnten nicht automatisch zusammengeführt werden. " +
            "Die Schutzsicherung wurde NICHT gelöscht.",
            null, stashRef, stashMessage, conflictFiles);

    public static SafePullResult BackupFailed(string reason) =>
        new(SafePullState.BackupCreationFailed, false,
            $"Schutzsicherung der lokalen Änderungen konnte nicht erstellt werden — Pull wurde NICHT ausgeführt.\n\n{reason}",
            null, null, null, Array.Empty<string>());

    public static SafePullResult RestoreFailedResult(string stashRef, string stashMessage, string reason) =>
        new(SafePullState.RestoreFailed, false,
            $"Pull ist fehlgeschlagen UND die Wiederherstellung der Schutzsicherung ist fehlgeschlagen. " +
            $"Die Schutzsicherung wurde NICHT gelöscht — bitte manuell prüfen.\n\n{reason}",
            null, stashRef, stashMessage, Array.Empty<string>());

    public static SafePullResult AbortedDueToLocalChanges(IReadOnlyList<string> changedFiles) =>
        new(SafePullState.AbortedDueToLocalChanges, false,
            "Lokale Änderungen gefunden. Pull wurde abgebrochen (Einstellung: nur warnen).",
            null, null, null, changedFiles);

    public static SafePullResult FastForwardNotPossibleResult(string pullOutput) =>
        new(SafePullState.FastForwardNotPossible, false,
            "Fast-Forward-Pull nicht möglich (Branches sind divergiert). Kein automatischer Merge wurde durchgeführt.",
            pullOutput, null, null, Array.Empty<string>());

    public static SafePullResult NotARepositoryResult(string reason) =>
        new(SafePullState.NotARepository, false, reason, null, null, null, Array.Empty<string>());

    /// <summary>Worktree was already clean, so the pull failure alone (e.g. network
    /// error, no fast-forward) is reported — nothing needed to be restored because
    /// nothing was ever changed.</summary>
    public static SafePullResult CleanPullFailed(string pullOutput) =>
        new(SafePullState.PullFailedAndRestored, false,
            $"Pull fehlgeschlagen. Der Arbeitsordner war bereits sauber, es gab nichts wiederherzustellen.\n\n{pullOutput}",
            pullOutput, null, null, Array.Empty<string>());
}
