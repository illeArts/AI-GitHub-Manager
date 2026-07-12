namespace AI.GitHubManager.Core.Export;

public static class ExportPath
{
    public static string NormalizeRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            throw new ArgumentException("Der Archivpfad muss relativ sein.", nameof(path));
        var normalized = path.Replace('\\', '/').TrimStart('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(p => p is "." or ".."))
            throw new ArgumentException("Unsicherer Archivpfad.", nameof(path));
        return string.Join('/', parts);
    }

    public static string ForArchive(string relativePath, string sourceRoot, bool includeRoot)
    {
        var safe = NormalizeRelative(relativePath);
        if (!includeRoot) return safe;
        var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourceRoot));
        return NormalizeRelative($"{rootName}/{safe}");
    }
}
