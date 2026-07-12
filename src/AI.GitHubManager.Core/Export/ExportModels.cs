namespace AI.GitHubManager.Core.Export;

public enum ExportProfileKind { WindowsExchange, CleanSource, AiAnalysis, CompleteFileArchive }

public sealed record ExportProfile(
    ExportProfileKind Kind, string Name, string Description,
    IReadOnlyList<string> ExclusionPatterns, bool IncludeRootDirectory = true);

public sealed record SensitiveFileFinding(string RelativePath, string MatchedPattern);

public sealed record ExportPlanEntry(
    string SourcePath, string RelativePath, string ArchivePath, long Length,
    DateTime LastWriteTimeUtc, bool IsSensitive, string? ExclusionReason = null)
{
    public bool IsIncluded => ExclusionReason is null;
}

public sealed record ExportPlan(
    string SourceRoot, string DestinationPath, ExportProfile Profile,
    IReadOnlyList<ExportPlanEntry> Entries, DateTimeOffset CreatedAt)
{
    public IReadOnlyList<ExportPlanEntry> Included => Entries.Where(x => x.IsIncluded).ToArray();
    public IReadOnlyList<ExportPlanEntry> Excluded => Entries.Where(x => !x.IsIncluded).ToArray();
    public long IncludedBytes => Included.Sum(x => x.Length);
    public long ExcludedBytes => Excluded.Sum(x => x.Length);
}

public sealed record ExportProgress(int CompletedFiles, int TotalFiles, string CurrentPath, long BytesWritten);
public sealed record ExportResult(bool Success, string DestinationPath, int FileCount, long BytesWritten, string? Error = null);
public sealed record ZipValidationResult(bool Success, IReadOnlyList<string> Errors);

public sealed record WorktreeInfo(bool IsGitRepository, bool IsWorktree, string RepositoryPath, string Branch, string Message);
