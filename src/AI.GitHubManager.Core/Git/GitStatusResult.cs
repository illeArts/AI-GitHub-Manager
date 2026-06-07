namespace AI.GitHubManager.Core.Git;

public sealed record GitStatusResult(
    bool IsRepository,
    string Branch,
    string RemoteOrigin,
    string RawStatus,
    IReadOnlyList<string> ChangedFiles,
    string? ErrorMessage);
