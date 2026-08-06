using AI.GitHubManager.Core.Git;
using AI.GitHubManager.Core.Projects;

namespace AI.GitHubManager.Core.Remote;

/// <summary>
/// Result of trying to detect a repository's real GitHub link from its local
/// git remote. Never fabricates a URL — either it found and parsed a real
/// "origin" remote, or it reports exactly why it could not
/// (no origin configured, or the origin URL didn't parse as GitHub/host+owner+repo).
/// </summary>
public sealed record RemoteDetectionResult(
    bool Success,
    string? Owner,
    string? Repository,
    string? WebUrl,
    string? RawRemoteUrl,
    string? ErrorMessage)
{
    public static RemoteDetectionResult Failed(string message, string? rawRemoteUrl = null) =>
        new(false, null, null, null, rawRemoteUrl, message);
}

/// <summary>
/// Detects a local repository's real GitHub owner/repository/web URL by running
/// <c>git remote get-url origin</c> and parsing the result with
/// <see cref="RemoteUrlNormalizer"/>. This is the only place in the app that is
/// allowed to populate <see cref="ManagedProject.RepositoryWebUrl"/> from git —
/// it never falls back to combining the currently-authenticated GitHub CLI user
/// with the local folder/project name.
/// </summary>
public sealed class RemoteDetectionService
{
    private readonly GitService _git;

    public RemoteDetectionService(GitService git) => _git = git;

    public async Task<RemoteDetectionResult> DetectAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var status = await _git.GetStatusAsync(repositoryPath, cancellationToken);

        if (!status.IsRepository)
            return RemoteDetectionResult.Failed(
                status.ErrorMessage ?? "Der Ordner ist kein Git-Repository.");

        if (string.IsNullOrWhiteSpace(status.RemoteOrigin))
            return RemoteDetectionResult.Failed(
                "Kein 'origin'-Remote gefunden. Bitte den GitHub-Link manuell festlegen.");

        var info = RemoteUrlNormalizer.Parse(status.RemoteOrigin);
        if (info is null)
            return RemoteDetectionResult.Failed(
                "Die Remote-URL konnte nicht als GitHub-Repository erkannt werden. Bitte den GitHub-Link manuell festlegen.",
                status.RemoteOrigin);

        return new RemoteDetectionResult(true, info.Owner, info.Repository, info.WebUrl, status.RemoteOrigin, null);
    }
}
