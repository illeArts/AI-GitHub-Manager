namespace AI.GitHubManager.Core.Export;

public sealed class ExportPlanService
{
    public Task<ExportPlan> CreateAsync(
        string sourceRoot, string destinationPath, ExportProfile profile,
        IEnumerable<string>? customExclusions = null,
        ISet<string>? sensitiveFilesToExclude = null,
        CancellationToken cancellationToken = default)
    {
        sourceRoot = Path.GetFullPath(sourceRoot);
        destinationPath = Path.GetFullPath(destinationPath);
        if (!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException(sourceRoot);
        if (!destinationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Das Ziel muss eine ZIP-Datei sein.", nameof(destinationPath));
        if (File.Exists(destinationPath)) throw new IOException("Das Zielarchiv existiert bereits.");
        if (IsInside(destinationPath, sourceRoot))
            throw new ArgumentException("Das Zielarchiv darf nicht innerhalb des Quellordners liegen.", nameof(destinationPath));

        var patterns = profile.ExclusionPatterns.Concat(customExclusions ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var sensitiveExclusions = sensitiveFilesToExclude ?? new HashSet<string>();
        var entries = new List<ExportPlanEntry>();
        var pending = new Stack<string>();
        pending.Push(sourceRoot);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(path);
                var relative = ExportPath.NormalizeRelative(Path.GetRelativePath(sourceRoot, path));
                var rule = patterns.FirstOrDefault(p => ExportRuleMatcher.IsMatch(relative, p));
                var isLink = (attributes & FileAttributes.ReparsePoint) != 0;
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (isLink) entries.Add(new(path, relative, ExportPath.ForArchive(relative, sourceRoot, profile.IncludeRootDirectory), 0, File.GetLastWriteTimeUtc(path), false, "Symbolischer Link wird aus Sicherheitsgründen nicht verfolgt."));
                    else if (rule is null) pending.Push(path);
                    else AddExcludedDirectoryFiles(path, sourceRoot, profile, rule, entries, cancellationToken);
                    continue;
                }

                var info = new FileInfo(path);
                var sensitive = SensitiveFileScanner.Find(relative) is not null;
                var reason = isLink ? "Symbolischer Link wird aus Sicherheitsgründen nicht archiviert."
                    : rule is not null ? $"Ausgeschlossen durch Muster: {rule}"
                    : sensitiveExclusions.Contains(relative) ? "Sensible Datei bewusst ausgeschlossen." : null;
                entries.Add(new(path, relative, ExportPath.ForArchive(relative, sourceRoot, profile.IncludeRootDirectory), info.Length, info.LastWriteTimeUtc, sensitive, reason));
            }
        }
        return Task.FromResult(new ExportPlan(sourceRoot, destinationPath, profile, entries.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToArray(), DateTimeOffset.UtcNow));
    }

    private static void AddExcludedDirectoryFiles(string directory, string root, ExportProfile profile, string rule, List<ExportPlanEntry> entries, CancellationToken token)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count > 0)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                token.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(path);
                var rel = ExportPath.NormalizeRelative(Path.GetRelativePath(root, path));
                var archive = ExportPath.ForArchive(rel, root, profile.IncludeRootDirectory);
                var isLink = (attributes & FileAttributes.ReparsePoint) != 0;
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (isLink) entries.Add(new(path, rel, archive, 0, File.GetLastWriteTimeUtc(path), false, "Symbolischer Link wird aus Sicherheitsgründen nicht verfolgt."));
                    else pending.Push(path);
                }
                else
                {
                    var info = new FileInfo(path);
                    entries.Add(new(path, rel, archive, info.Length, info.LastWriteTimeUtc, SensitiveFileScanner.Find(rel) is not null, $"Ausgeschlossen durch Muster: {rule}"));
                }
            }
        }
    }

    private static bool IsInside(string file, string directory)
    {
        var relative = Path.GetRelativePath(directory, file);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}
