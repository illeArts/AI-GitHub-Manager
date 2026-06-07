namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Detects well-known transient Git errors and fixes them automatically,
/// so the calling operation can be retried without user interaction.
/// </summary>
public static class GitErrorRecovery
{
    /// <summary>
    /// Inspects the output of a failed Git command and attempts an automatic fix.
    /// </summary>
    /// <param name="repositoryPath">Absolute path to the Git working directory.</param>
    /// <param name="stderr">The combined stderr/stdout from the failed command.</param>
    /// <returns>
    /// <c>(true, description)</c> when a fix was applied and a retry makes sense;
    /// <c>(false, null)</c> when the error is not recoverable here.
    /// </returns>
    public static Task<(bool Recovered, string? Message)> TryRecoverAsync(
        string repositoryPath, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return Task.FromResult<(bool, string?)>((false, null));

        // ── index.lock ──────────────────────────────────────────────────────
        if (stderr.Contains("index.lock", StringComparison.OrdinalIgnoreCase) &&
            stderr.Contains("File exists", StringComparison.OrdinalIgnoreCase))
        {
            return TryDeleteLockFileAsync(repositoryPath, ".git/index.lock",
                "index.lock entfernt (abgestürzter Git-Prozess). Befehl wird wiederholt…");
        }

        // ── MERGE_HEAD lock ─────────────────────────────────────────────────
        if (stderr.Contains("MERGE_HEAD", StringComparison.OrdinalIgnoreCase) &&
            stderr.Contains("exists", StringComparison.OrdinalIgnoreCase))
        {
            return TryDeleteLockFileAsync(repositoryPath, ".git/MERGE_HEAD",
                "MERGE_HEAD entfernt (unterbrochener Merge). Befehl wird wiederholt…");
        }

        // ── CHERRY_PICK_HEAD lock ───────────────────────────────────────────
        if (stderr.Contains("CHERRY_PICK_HEAD", StringComparison.OrdinalIgnoreCase))
        {
            return TryDeleteLockFileAsync(repositoryPath, ".git/CHERRY_PICK_HEAD",
                "CHERRY_PICK_HEAD entfernt (unterbrochener Cherry-Pick). Befehl wird wiederholt…");
        }

        // ── packed-refs.lock ────────────────────────────────────────────────
        if (stderr.Contains("packed-refs.lock", StringComparison.OrdinalIgnoreCase) &&
            stderr.Contains("File exists", StringComparison.OrdinalIgnoreCase))
        {
            return TryDeleteLockFileAsync(repositoryPath, ".git/packed-refs.lock",
                "packed-refs.lock entfernt. Befehl wird wiederholt…");
        }

        return Task.FromResult<(bool, string?)>((false, null));
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static Task<(bool Recovered, string? Message)> TryDeleteLockFileAsync(
        string repositoryPath, string relativeLockPath, string successMessage)
    {
        try
        {
            var lockFile = Path.Combine(repositoryPath, relativeLockPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(lockFile))
                return Task.FromResult<(bool, string?)>((false, null));

            File.Delete(lockFile);
            return Task.FromResult<(bool, string?)>((true, successMessage));
        }
        catch (Exception ex)
        {
            return Task.FromResult<(bool, string?)>((false, $"Auto-Fix fehlgeschlagen: {ex.Message}"));
        }
    }
}
