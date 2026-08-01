namespace AI.GitHubManager.Core.Update;

/// <summary>
/// Result of an update check, including the platform-specific asset that was
/// (or could not be) selected for the machine AI GitHub Manager is running on.
/// </summary>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleasePageUrl,
    string? DirectDownloadUrl,
    string? ErrorMessage,
    UpdateOperatingSystem OperatingSystem = UpdateOperatingSystem.Unknown,
    UpdateArchitecture Architecture = UpdateArchitecture.Unknown,
    string? AssetName = null,
    bool IsCompatibleAssetAvailable = false)
{
    public static UpdateCheckResult UpToDate(string current) =>
        new(false, current, current, string.Empty, null, null);

    public static UpdateCheckResult Failed(string current, string error) =>
        new(false, current, string.Empty, string.Empty, null, error);

    public static UpdateCheckResult NewVersion(
        string current,
        string latest,
        string releaseUrl,
        string? downloadUrl,
        UpdateOperatingSystem operatingSystem = UpdateOperatingSystem.Unknown,
        UpdateArchitecture architecture = UpdateArchitecture.Unknown,
        string? assetName = null,
        bool isCompatibleAssetAvailable = false) =>
        new(true, current, latest, releaseUrl, downloadUrl, null,
            operatingSystem, architecture, assetName, isCompatibleAssetAvailable);

    /// <summary>
    /// A newer version exists, but no compatible package could be found for
    /// this platform/architecture in the release's assets. The caller must
    /// not offer a download link that would be wrong for this machine —
    /// only the release page is safe to open.
    /// </summary>
    public static UpdateCheckResult NoCompatibleAsset(
        string current,
        string latest,
        string releaseUrl,
        UpdateOperatingSystem operatingSystem,
        UpdateArchitecture architecture) =>
        new(true, current, latest, releaseUrl, null,
            "Für diese Plattform ist in diesem Release derzeit kein passendes Paket verfügbar. Die Release-Seite wurde geöffnet.",
            operatingSystem, architecture, null, false);
}
