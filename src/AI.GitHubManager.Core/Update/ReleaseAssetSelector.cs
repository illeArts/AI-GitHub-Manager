namespace AI.GitHubManager.Core.Update;

/// <summary>Minimal projection of a GitHub release asset needed for selection.</summary>
public sealed record ReleaseAsset(string Name, string DownloadUrl);

/// <summary>
/// Picks the correct release asset for the platform AI GitHub Manager is
/// currently running on. Never falls back to an asset for a different OS —
/// if nothing matches, the caller gets <c>null</c> and must not offer a
/// download that would be wrong for the user's machine.
/// </summary>
public static class ReleaseAssetSelector
{
    /// <summary>
    /// Naming convention emitted by the release pipeline:
    ///   Windows x64        → AI_GitHub_Manager_Setup_&lt;version&gt;_win-x64.exe
    ///   macOS Intel x64     → AI-GitHub-Manager-v&lt;version&gt;-macos-x64.zip
    ///   macOS Apple Silicon → AI-GitHub-Manager-v&lt;version&gt;-macos-arm64.zip
    ///   Linux x64           → AI-GitHub-Manager-v&lt;version&gt;-linux-x64.tar.gz
    /// </summary>
    public static ReleaseAsset? SelectFor(PlatformDescriptor platform, IReadOnlyList<ReleaseAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        // Checksum files (and other non-installable metadata) must never be
        // offered as the program asset, no matter what the pattern match below does.
        var candidates = assets.Where(a => !IsChecksumOrMetadataFile(a.Name)).ToArray();

        return (platform.OperatingSystem, platform.Architecture) switch
        {
            (UpdateOperatingSystem.Windows, UpdateArchitecture.X64) =>
                FindExact(candidates, suffix: "win-x64.exe", requiredPrefix: null, requiredExtension: ".exe"),

            (UpdateOperatingSystem.MacOS, UpdateArchitecture.X64) =>
                FindExact(candidates, suffix: "macos-x64.zip", requiredPrefix: null, requiredExtension: ".zip"),

            (UpdateOperatingSystem.MacOS, UpdateArchitecture.Arm64) =>
                FindExact(candidates, suffix: "macos-arm64.zip", requiredPrefix: null, requiredExtension: ".zip"),

            (UpdateOperatingSystem.Linux, UpdateArchitecture.X64) =>
                FindExact(candidates, suffix: "linux-x64.tar.gz", requiredPrefix: null, requiredExtension: ".tar.gz"),

            // Unknown OS/architecture combination (e.g. Linux arm64, Windows arm64):
            // no compatible package is currently published — do not guess.
            _ => null,
        };
    }

    private static bool IsChecksumOrMetadataFile(string name)
    {
        return name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".sha256.txt", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".sha512", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".sig", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".asc", StringComparison.OrdinalIgnoreCase)
            || name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".sha256sum", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Matches assets whose name ends with the exact platform suffix (case
    /// insensitive) and additionally verifies the file extension, so that a
    /// name like "...win-x64.exe.sha256" (already filtered above, but kept as
    /// defense in depth) can never slip through.
    /// </summary>
    private static ReleaseAsset? FindExact(
        IReadOnlyList<ReleaseAsset> assets,
        string suffix,
        string? requiredPrefix,
        string requiredExtension)
    {
        var matches = assets
            .Where(a => a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Where(a => a.Name.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
            .Where(a => requiredPrefix is null || a.Name.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // If several near-duplicate assets match (shouldn't happen with a sane
        // release pipeline), prefer the shortest name — it's the least likely
        // to carry an extra qualifier we don't understand — but never guess
        // across platforms.
        return matches.OrderBy(a => a.Name.Length).FirstOrDefault();
    }
}
