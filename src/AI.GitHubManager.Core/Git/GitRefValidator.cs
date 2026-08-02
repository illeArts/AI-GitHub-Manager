namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Validates branch names, commit refs, and remote names before they are
/// ever placed into a git argument list (Teil D: "Validierung von
/// Branch-/Remote-Namen"). This is a defense-in-depth check — argument
/// lists are already passed to the process as discrete array elements
/// (never through a shell), so this exists to reject obviously malformed
/// or flag-injection-shaped input (e.g. "--upload-pack=...") early, with a
/// clear error message, rather than silently passing it through to git.
/// </summary>
public static class GitRefValidator
{
    /// <summary>
    /// True when <paramref name="value"/> is safe to use as a single git ref
    /// argument (branch name, commit hash, tag). Rejects empty/whitespace
    /// input, anything starting with '-' (would be parsed as a flag by git),
    /// and control characters.
    /// </summary>
    public static bool IsValidRef(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        if (trimmed.StartsWith('-')) return false;
        if (trimmed.Contains("..")) return false; // avoid ambiguous range syntax being smuggled in
        if (trimmed.Any(char.IsControl)) return false;
        if (trimmed.Contains(' ')) return false;

        return true;
    }

    /// <summary>Returns a bilingual error message for an invalid ref, or null if valid.</summary>
    public static (string De, string En)? ValidationError(string? value)
    {
        if (IsValidRef(value)) return null;

        return (
            "Ungültiger Branch-/Commit-Name. Er darf nicht leer sein, nicht mit \"-\" beginnen und keine Leerzeichen enthalten.",
            "Invalid branch/commit name. It must not be empty, must not start with \"-\", and must not contain spaces.");
    }
}
