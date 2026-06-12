namespace AI.GitHubManager.Core.Update;

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleasePageUrl,
    string? DirectDownloadUrl,
    string? ErrorMessage)
{
    public static UpdateCheckResult UpToDate(string current) =>
        new(false, current, current, string.Empty, null, null);

    public static UpdateCheckResult Failed(string current, string error) =>
        new(false, current, string.Empty, string.Empty, null, error);

    public static UpdateCheckResult NewVersion(string current, string latest, string releaseUrl, string? downloadUrl) =>
        new(true, current, latest, releaseUrl, downloadUrl, null);
}
