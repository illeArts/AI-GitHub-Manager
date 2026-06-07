namespace AI.GitHubManager.Core.Git;

/// <summary>
/// Analyses git-status --porcelain output and flags untracked files that almost
/// certainly do not belong in a source repository: no-extension scratch files,
/// ad-hoc scripts (.bat, .ps1, .sh), HTML/log/temp files at the repo root, etc.
/// </summary>
public static class SuspiciousFileChecker
{
    // Well-known files that legitimately have no extension
    private static readonly HashSet<string> AllowedNoExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        "Makefile", "Dockerfile", "Jenkinsfile", "Vagrantfile", "Brewfile",
        "Procfile", "Rakefile", "Gemfile", "LICENSE", "README",
        "CHANGELOG", "CONTRIBUTING", "AUTHORS", "NOTICE", "TODO",
        "COPYING", "CODEOWNERS", "OWNERS", ".editorconfig", ".gitattributes",
        ".gitmodules", ".gitkeep"
    };

    // Extensions that are suspicious anywhere in the repo
    private static readonly HashSet<string> AlwaysSuspicious = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".cmd", ".ps1", ".tmp", ".bak", ".orig",
        ".log", ".cache", ".swp", ".swo"
    };

    // Extensions that are only suspicious when placed directly in the repo root
    private static readonly HashSet<string> SuspiciousAtRoot = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".txt", ".csv", ".xlsx", ".docx", ".pdf", ".zip"
    };

    /// <summary>
    /// Returns the paths of files that are flagged as suspicious.
    /// Only looks at untracked lines (starting with "??") in the porcelain output.
    /// </summary>
    public static IReadOnlyList<string> FindSuspicious(IEnumerable<string> porcelainLines)
    {
        var result = new List<string>();

        foreach (var raw in porcelainLines)
        {
            if (raw.Length < 4) continue;

            // porcelain v1: first two chars = XY status, third = space, rest = path
            var xy = raw[..2];

            // Only check untracked files; already-tracked modifications are fine
            if (xy != "??") continue;

            var path = raw[3..].Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(path)) continue;

            var fileName = Path.GetFileName(path);
            var ext      = Path.GetExtension(fileName);      // "" if none
            var dir      = Path.GetDirectoryName(path) ?? string.Empty;
            bool isAtRoot = string.IsNullOrEmpty(dir);

            // 1. No extension → suspicious unless explicitly allowed
            if (string.IsNullOrEmpty(ext))
            {
                if (!AllowedNoExtension.Contains(fileName))
                    result.Add(path);
                continue;
            }

            // 2. Always-suspicious extensions
            if (AlwaysSuspicious.Contains(ext))
            {
                result.Add(path);
                continue;
            }

            // 3. Root-only suspicious extensions
            if (isAtRoot && SuspiciousAtRoot.Contains(ext))
            {
                result.Add(path);
            }
        }

        return result;
    }
}
