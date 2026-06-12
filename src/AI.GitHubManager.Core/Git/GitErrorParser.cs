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
    NetworkError
}

/// <summary>
/// Structured representation of a parsed Git error.
/// </summary>
public sealed record GitErrorInfo(
    GitErrorKind Kind,
    string UserMessage,
    string? Hint);

/// <summary>
/// Parses raw Git / GitHub CLI error output into a structured <see cref="GitErrorInfo"/>.
/// All matching is case-insensitive against the combined stderr+stdout of a failed command.
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
                "Warte kurz und versuche es erneut. Wenn das Problem bleibt, schließe alle Git-Clients und lösche .git/index.lock manuell.");

        // ── Authentication failed ────────────────────────────────────────────
        if (Contains(output, "Authentication failed") ||
            Contains(output, "could not read Username") ||
            Contains(output, "Invalid username or password"))
            return new GitErrorInfo(
                GitErrorKind.AuthenticationFailed,
                "GitHub-Authentifizierung fehlgeschlagen.",
                "Führe 'GitHub Login' aus und dann 'Git Credentials reparieren'.");

        // ── Repository not found ─────────────────────────────────────────────
        if (Contains(output, "repository not found") ||
            Contains(output, "Repository not found") ||
            (Contains(output, "repository") && Contains(output, "not found")) ||
            Contains(output, "does not exist"))
            return new GitErrorInfo(
                GitErrorKind.RepositoryNotFound,
                "Repository nicht gefunden.",
                "Prüfe ob das Repository auf GitHub existiert und du Zugriff hast.");

        // ── Workflow scope missing ───────────────────────────────────────────
        if (Contains(output, "refusing to allow") &&
            Contains(output, "workflow"))
            return new GitErrorInfo(
                GitErrorKind.WorkflowScopeMissing,
                "Kein Recht, Workflow-Dateien (.github/workflows) zu pushen.",
                "Klicke 'GitHub Rechte: repo + workflow' und autorisiere den Scope erneut.");

        // ── Non-fast-forward ─────────────────────────────────────────────────
        if (Contains(output, "non-fast-forward") ||
            Contains(output, "[rejected]") ||
            Contains(output, "Updates were rejected"))
            return new GitErrorInfo(
                GitErrorKind.NonFastForward,
                "Push abgelehnt – der Remote-Branch hat neuere Commits.",
                "Führe zuerst 'Pull' aus, um die Remote-Änderungen zu integrieren, dann erneut pushen.");

        // ── Unrelated histories ──────────────────────────────────────────────
        if (Contains(output, "unrelated histories"))
            return new GitErrorInfo(
                GitErrorKind.UnrelatedHistories,
                "Die lokale und die Remote-History haben keinen gemeinsamen Vorfahren.",
                "Führe einen Pull mit --allow-unrelated-histories aus, wenn du sicher bist, dass du diese Repos zusammenführen willst.");

        // ── Merge conflict ───────────────────────────────────────────────────
        if (Contains(output, "CONFLICT") ||
            Contains(output, "Automatic merge failed") ||
            Contains(output, "merge conflict"))
            return new GitErrorInfo(
                GitErrorKind.MergeConflict,
                "Merge-Konflikte vorhanden.",
                "Öffne die betroffenen Dateien, löse die Konflikte (<<<<<<< Marker), führe dann Commit + Push aus.");

        // ── Nothing to commit ────────────────────────────────────────────────
        if (Contains(output, "nothing to commit") ||
            Contains(output, "nothing added to commit"))
            return new GitErrorInfo(
                GitErrorKind.NothingToCommit,
                "Keine Änderungen zum Committen vorhanden.",
                null);

        // ── Local changes would be overwritten ──────────────────────────────
        if (Contains(output, "would be overwritten by"))
            return new GitErrorInfo(
                GitErrorKind.LocalChangesOverwritten,
                "Lokale Änderungen würden durch den Pull überschrieben.",
                "Committe oder verwerfe deine lokalen Änderungen, dann führe Pull erneut aus.");

        // ── Network / resolve error ──────────────────────────────────────────
        if (Contains(output, "Could not resolve host") ||
            Contains(output, "Failed to connect") ||
            Contains(output, "SSL certificate problem") ||
            Contains(output, "Couldn't connect to server"))
            return new GitErrorInfo(
                GitErrorKind.NetworkError,
                "Netzwerkfehler – GitHub ist nicht erreichbar.",
                "Prüfe deine Internetverbindung und ob github.com erreichbar ist.");

        return Unknown(output);
    }

    /// <summary>
    /// Returns a user-friendly one-liner for a known error kind,
    /// or null when kind is Unknown.
    /// </summary>
    public static string? FriendlyMessage(GitErrorKind kind) => kind switch
    {
        GitErrorKind.AuthenticationFailed    => "Authentifizierung fehlgeschlagen",
        GitErrorKind.RepositoryNotFound      => "Repository nicht gefunden",
        GitErrorKind.WorkflowScopeMissing    => "Workflow-Scope fehlt",
        GitErrorKind.NonFastForward          => "Push abgelehnt (nicht fast-forward)",
        GitErrorKind.UnrelatedHistories      => "Unrelated histories",
        GitErrorKind.MergeConflict           => "Merge-Konflikt",
        GitErrorKind.NothingToCommit         => "Nichts zu committen",
        GitErrorKind.LocalChangesOverwritten => "Lokale Änderungen würden überschrieben",
        GitErrorKind.IndexLocked             => "index.lock vorhanden",
        GitErrorKind.NetworkError            => "Netzwerkfehler",
        _                                    => null
    };

    // ── Private helpers ──────────────────────────────────────────────────────

    private static GitErrorInfo Unknown(string output) =>
        new(GitErrorKind.Unknown, output.Trim(), null);

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
