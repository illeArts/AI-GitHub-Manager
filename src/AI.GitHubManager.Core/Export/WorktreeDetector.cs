namespace AI.GitHubManager.Core.Export;

public sealed class WorktreeDetector
{
    public WorktreeInfo Detect(string sourceRoot)
    {
        var git = Path.Combine(Path.GetFullPath(sourceRoot), ".git");
        if (Directory.Exists(git)) return new(true, false, sourceRoot, ReadBranch(Path.Combine(git, "HEAD")), "Normales Git-Arbeitsverzeichnis.");
        if (!File.Exists(git)) return new(false, false, string.Empty, string.Empty, "Kein Git-Repository erkannt.");
        var text = File.ReadAllText(git).Trim();
        if (!text.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase)) return new(false, false, string.Empty, string.Empty, "Ungültige .git-Verweisdatei.");
        var gitDirText = text[7..].Trim();
        var gitDir = Path.GetFullPath(gitDirText, sourceRoot);
        var branch = ReadBranch(Path.Combine(gitDir, "HEAD"));
        var commonDirFile = Path.Combine(gitDir, "commondir");
        var commonDir = File.Exists(commonDirFile) ? Path.GetFullPath(File.ReadAllText(commonDirFile).Trim(), gitDir) : gitDir;
        return new(true, true, commonDir, branch, "Git-Worktree erkannt. Dieses ZIP ist nicht zur Synchronisierung zwischen Computern geeignet und kein eigenständiges Git-Wiederherstellungsbackup.");
    }

    private static string ReadBranch(string headPath)
    {
        if (!File.Exists(headPath)) return string.Empty;
        var head = File.ReadAllText(headPath).Trim();
        const string prefix = "ref: refs/heads/";
        return head.StartsWith(prefix, StringComparison.Ordinal) ? head[prefix.Length..] : head[..Math.Min(12, head.Length)];
    }
}
