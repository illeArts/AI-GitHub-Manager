namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Well-known Git/GitHub error categories.
/// </summary>
public enum GitErrorKind
{
    Unknown,
    AuthenticationFailed,
    RepositoryNotFound,
    WorkflowScopeMissing,
    NonFastForward,
    UnrelatedHistories,
    MergeConflict,
    NothingToCommit,
    LocalChangesOverwritten,
    IndexLocked,
    NetworkError,

    /// <summary>"fatal: not possible to fast-forward, aborting" — a Pull-specific
    /// diverged-branches failure, distinct from a rejected Push (NonFastForward).</summary>
    PullNotFastForward,

    /// <summary>No upstream branch configured for the current branch.</summary>
    NoUpstream,

    /// <summary>HEAD is not on any branch (Teil B9).</summary>
    DetachedHead,

    /// <summary>A merge, rebase, or cherry-pick is already in progress and blocks the requested action.</summary>
    InterruptedMergeOrRebase,

    /// <summary>File system permission or access error (Teil B9: "Datei- oder Berechtigungsfehler").</summary>
    PermissionError,
}

/// <summary>
/// Structured representation of a parsed Git error, with both a German and
/// an English user-facing message and hint (Teil A1: the language switch
/// must cover error translations too, not just static UI labels).
/// </summary>
public sealed record GitErrorInfo(
    GitErrorKind Kind,
    string UserMessage,
    string? Hint,
    string UserMessageEn,
    string? HintEn)
{
    public string Message(bool english) => english ? UserMessageEn : UserMessage;
    public string? HintText(bool english) => english ? HintEn : Hint;
}

/// <summary>
/// Parses raw Git / GitHub CLI error output into a structured <see cref="GitErrorInfo"/>
/// (Teil B9). All matching is case-insensitive against the combined stderr+stdout of a
/// failed command. The original raw output is never altered or discarded by this class —
/// callers keep it available separately ("Technische Details anzeigen").
/// </summary>
public static class GitErrorParser
{
    /// <summary>
    /// Returns a <see cref="GitErrorInfo"/> for the first recognised pattern,
    /// or <c>Unknown</c> if nothing matches.
    /// </summary>
    public static GitErrorInfo Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return Unknown(output);

        // ── Index locked ────────────────────────────────────────────────────
        if (Contains(output, "index.lock") && Contains(output, "File exists"))
            return new GitErrorInfo(
                GitErrorKind.IndexLocked,
                "Eine andere Git-Operation läuft noch (index.lock vorhanden).",
                "Warte kurz und versuche es erneut. Wenn das Problem bleibt, schließe alle Git-Clients und lösche .git/index.lock manuell.",
                "Another Git operation is still running (index.lock present).",
                "Wait a moment and try again. If the problem persists, close all Git clients and remove .git/index.lock manually.");

        // ── Interrupted merge/rebase/cherry-pick ─────────────────────────────
        if (Contains(output, "you have not concluded your merge") ||
            Contains(output, "unmerged files") ||
            (Contains(output, "rebase") && Contains(output, "in progress")) ||
            Contains(output, "cherry-pick is now empty") ||
            Contains(output, "it looks like you may be committing a merge"))
            return new GitErrorInfo(
                GitErrorKind.InterruptedMergeOrRebase,
                "Ein Merge, Rebase oder Cherry-Pick ist bereits unterbrochen und läuft noch.",
                "Erst den laufenden Vorgang abschließen oder abbrechen (z. B. git merge --abort), bevor etwas Neues gestartet wird.",
                "A merge, rebase, or cherry-pick is already interrupted and still in progress.",
                "Finish or abort the running operation first (e.g. git merge --abort) before starting something new.");

        // ── Detached HEAD ─────────────────────────────────────────────────────
        if (Contains(output, "you are not currently on a branch") ||
            Contains(output, "detached HEAD") ||
            Contains(output, "HEAD detached"))
            return new GitErrorInfo(
                GitErrorKind.DetachedHead,
                "Du befindest dich derzeit auf keinem Branch (\"detached HEAD\").",
                "Wechsle zuerst zu einem Branch (z. B. \"Branch wechseln\" → main), bevor du fortfährst.",
                "You are currently not on any branch (\"detached HEAD\").",
                "Switch to a branch first (e.g. \"Switch branch\" → main) before continuing.");

        // ── No upstream ───────────────────────────────────────────────────────
        if (Contains(output, "no upstream branch") ||
            Contains(output, "has no upstream branch") ||
            Contains(output, "set-upstream"))
            return new GitErrorInfo(
                GitErrorKind.NoUpstream,
                "Für diesen Branch ist kein Upstream-Branch (Remote-Gegenstück) eingerichtet.",
                "Beim ersten Push für einen neuen Branch muss einmalig ein Upstream gesetzt werden (git push -u origin <branch>).",
                "This branch has no upstream (remote counterpart) configured.",
                "The first push for a new branch needs to set an upstream once (git push -u origin <branch>).");

        // ── Authentication failed ────────────────────────────────────────────
        if (Contains(output, "Authentication failed") ||
            Contains(output, "could not read Username") ||
            Contains(output, "Invalid username or password"))
            return new GitErrorInfo(
                GitErrorKind.AuthenticationFailed,
                "GitHub-Authentifizierung fehlgeschlagen.",
                "Führe 'GitHub Login' aus und dann 'Git Credentials reparieren'.",
                "GitHub authentication failed.",
                "Run 'GitHub Login' and then 'Repair Git Credentials'.");

        // ── Repository not found ─────────────────────────────────────────────
        if (Contains(output, "repository not found") ||
            Contains(output, "Repository not found") ||
            (Contains(output, "repository") && Contains(output, "not found")) ||
            Contains(output, "does not exist"))
            return new GitErrorInfo(
                GitErrorKind.RepositoryNotFound,
                "Repository nicht gefunden.",
                "Prüfe ob das Repository auf GitHub existiert und du Zugriff hast.",
                "Repository not found.",
                "Check whether the repository exists on GitHub and that you have access.");

        // ── Workflow scope missing ───────────────────────────────────────────
        if (Contains(output, "refusing to allow") &&
            Contains(output, "workflow"))
            return new GitErrorInfo(
                GitErrorKind.WorkflowScopeMissing,
                "Kein Recht, Workflow-Dateien (.github/workflows) zu pushen.",
                "Klicke 'GitHub Rechte: repo + workflow' und autorisiere den Scope erneut.",
                "No permission to push workflow files (.github/workflows).",
                "Click 'GitHub Scopes: repo + workflow' and re-authorize the scope.");

        // ── Pull-specific fast-forward failure ("not possible to fast-forward,
        // aborting") — distinct from a rejected Push below. ───────────────────
        if (Contains(output, "not possible to fast-forward"))
            return new GitErrorInfo(
                GitErrorKind.PullNotFastForward,
                "Die Aktualisierung konnte nicht automatisch abgeschlossen werden.",
                "Dein lokaler Branch und der Online-Branch enthalten unterschiedliche Änderungen. " +
                "Nächste Schritte: Änderungen vergleichen, lokale Änderungen zwischenspeichern, " +
                "Merge durchführen, oder Vorgang abbrechen.",
                "The update could not be completed automatically.",
                "Your local branch and the online branch contain different changes. " +
                "Next steps: compare changes, stash local changes, perform a merge, or cancel.");

        // ── Non-fast-forward (Push rejection) ────────────────────────────────
        if (Contains(output, "non-fast-forward") ||
            Contains(output, "[rejected]") ||
            Contains(output, "Updates were rejected"))
            return new GitErrorInfo(
                GitErrorKind.NonFastForward,
                "Push abgelehnt – der Remote-Branch hat neuere Commits.",
                "Führe zuerst 'Pull' aus, um die Remote-Änderungen zu integrieren, dann erneut pushen.",
                "Push rejected – the remote branch has newer commits.",
                "Run 'Pull' first to integrate the remote changes, then push again.");

        // ── Unrelated histories ──────────────────────────────────────────────
        if (Contains(output, "unrelated histories"))
            return new GitErrorInfo(
                GitErrorKind.UnrelatedHistories,
                "Die lokale und die Remote-History haben keinen gemeinsamen Vorfahren.",
                "Führe einen Pull mit --allow-unrelated-histories aus, wenn du sicher bist, dass du diese Repos zusammenführen willst.",
                "The local and remote history don't share a common ancestor.",
                "Run a pull with --allow-unrelated-histories if you're sure you want to merge these repos.");

        // ── Merge conflict ───────────────────────────────────────────────────
        if (Contains(output, "CONFLICT") ||
            Contains(output, "Automatic merge failed") ||
            Contains(output, "merge conflict"))
            return new GitErrorInfo(
                GitErrorKind.MergeConflict,
                "Merge-Konflikte vorhanden.",
                "Öffne die betroffenen Dateien, löse die Konflikte (<<<<<<< Marker), führe dann Commit + Push aus.",
                "Merge conflicts present.",
                "Open the affected files, resolve the conflicts (<<<<<<< markers), then run Commit + Push.");

        // ── Nothing to commit ────────────────────────────────────────────────
        if (Contains(output, "nothing to commit") ||
            Contains(output, "nothing added to commit"))
            return new GitErrorInfo(
                GitErrorKind.NothingToCommit,
                "Keine Änderungen zum Committen vorhanden.",
                null,
                "No changes to commit.",
                null);

        // ── Local changes would be overwritten ──────────────────────────────
        if (Contains(output, "would be overwritten by"))
            return new GitErrorInfo(
                GitErrorKind.LocalChangesOverwritten,
                "Lokale Änderungen würden durch den Pull überschrieben.",
                "Committe oder verwerfe deine lokalen Änderungen, dann führe Pull erneut aus.",
                "Local changes would be overwritten by the pull.",
                "Commit or discard your local changes, then run pull again.");

        // ── File / permission error ──────────────────────────────────────────
        if (Contains(output, "Permission denied") ||
            Contains(output, "Access is denied") ||
            Contains(output, "Operation not permitted") ||
            Contains(output, "EACCES"))
            return new GitErrorInfo(
                GitErrorKind.PermissionError,
                "Datei- oder Berechtigungsfehler.",
                "Prüfe, ob die Datei von einem anderen Programm geöffnet ist oder ob dir die Schreibrechte für diesen Ordner fehlen.",
                "File or permission error.",
                "Check whether the file is open in another program, or whether you're missing write permission for this folder.");

        // ── Network / resolve error ──────────────────────────────────────────
        if (Contains(output, "Could not resolve host") ||
            Contains(output, "Failed to connect") ||
            Contains(output, "SSL certificate problem") ||
            Contains(output, "Couldn't connect to server"))
            return new GitErrorInfo(
                GitErrorKind.NetworkError,
                "Netzwerkfehler – GitHub ist nicht erreichbar.",
                "Prüfe deine Internetverbindung und ob github.com erreichbar ist.",
                "Network error – GitHub is unreachable.",
                "Check your internet connection and whether github.com is reachable.");

        return Unknown(output);
    }

    /// <summary>
    /// Returns a user-friendly one-liner for a known error kind,
    /// or null when kind is Unknown.
    /// </summary>
    public static string? FriendlyMessage(GitErrorKind kind) => kind switch
    {
        GitErrorKind.AuthenticationFailed      => "Authentifizierung fehlgeschlagen",
        GitErrorKind.RepositoryNotFound        => "Repository nicht gefunden",
        GitErrorKind.WorkflowScopeMissing      => "Workflow-Scope fehlt",
        GitErrorKind.NonFastForward            => "Push abgelehnt (nicht fast-forward)",
        GitErrorKind.PullNotFastForward        => "Aktualisierung nicht möglich (divergierte Branches)",
        GitErrorKind.UnrelatedHistories        => "Unrelated histories",
        GitErrorKind.MergeConflict             => "Merge-Konflikt",
        GitErrorKind.NothingToCommit           => "Nichts zu committen",
        GitErrorKind.LocalChangesOverwritten   => "Lokale Änderungen würden überschrieben",
        GitErrorKind.IndexLocked               => "index.lock vorhanden",
        GitErrorKind.NetworkError               => "Netzwerkfehler",
        GitErrorKind.NoUpstream                => "Kein Upstream-Branch",
        GitErrorKind.DetachedHead              => "Detached HEAD",
        GitErrorKind.InterruptedMergeOrRebase  => "Unterbrochener Merge/Rebase",
        GitErrorKind.PermissionError           => "Datei-/Berechtigungsfehler",
        _                                       => null
    };

    // ── Private helpers ──────────────────────────────────────────────────────

    private static GitErrorInfo Unknown(string output) =>
        new(GitErrorKind.Unknown, output.Trim(), null, output.Trim(), null);

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
