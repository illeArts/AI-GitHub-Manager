using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace AI.GitHubManager.Core.Update;

/// <summary>
/// Checks the GitHub Releases API for a newer version of AI GitHub Manager.
/// Requires no authentication — works against public repos.
///
/// Configure the repository owner/name via the constructor or leave the
/// defaults pointing to the canonical repo.
///
/// Asset selection is platform-aware: the service never returns a download
/// link for a different operating system/architecture than the one it is
/// currently running on (see <see cref="ReleaseAssetSelector"/>).
/// </summary>
public sealed class UpdateCheckService
{
    // ── Configuration ────────────────────────────────────────────────────────

    /// <summary>GitHub owner/repo slug, e.g. "illeArts/AI-GitHub-Manager".</summary>
    private readonly string _repoSlug;
    private readonly PlatformDescriptor _platform;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    // ── Lazy singleton HttpClient ────────────────────────────────────────────
    private static readonly Lazy<HttpClient> _http = new(() =>
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        // GitHub API requires a User-Agent header
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("AI-GitHub-Manager", CurrentVersion));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    });

    // ── Current version (read from assembly, falls back to hardcoded) ────────
    public static readonly string CurrentVersion =
        Assembly.GetEntryAssembly()?.GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "1.6.2";

    public UpdateCheckService(string repoSlug = "illeArts/AI-GitHub-Manager")
        : this(repoSlug, PlatformDescriptor.Current)
    {
    }

    /// <summary>Test seam: inject an explicit platform instead of detecting the real one.</summary>
    public UpdateCheckService(string repoSlug, PlatformDescriptor platform)
    {
        _repoSlug = repoSlug;
        _platform = platform;
    }

    /// <summary>
    /// Queries the GitHub Releases API and compares the latest tag with the
    /// running version. Returns immediately with an error result on any failure
    /// (no exceptions propagate).
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_repoSlug}/releases/latest";
            using var response = await _http.Value.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // 404 = no releases yet; don't bother the user
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return UpdateCheckResult.UpToDate(CurrentVersion);

                return UpdateCheckResult.Failed(CurrentVersion,
                    $"GitHub API: HTTP {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName    = root.TryGetProperty("tag_name",  out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var releaseUrl = root.TryGetProperty("html_url",  out var h) ? h.GetString() ?? string.Empty : string.Empty;
            var isDraft    = root.TryGetProperty("draft",     out var d) && d.GetBoolean();
            var isPreRelease = root.TryGetProperty("prerelease", out var p) && p.GetBoolean();

            // Skip drafts and pre-releases
            if (isDraft || isPreRelease)
                return UpdateCheckResult.UpToDate(CurrentVersion);

            // Normalise tag: strip leading 'v'
            var latestVersion = tagName.TrimStart('v');

            if (!IsNewerVersion(CurrentVersion, latestVersion))
                return UpdateCheckResult.UpToDate(CurrentVersion);

            var assets = new List<ReleaseAsset>();
            if (root.TryGetProperty("assets", out var assetsElement))
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var downloadUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(downloadUrl))
                        assets.Add(new ReleaseAsset(name, downloadUrl));
                }
            }

            var selected = ReleaseAssetSelector.SelectFor(_platform, assets);

            if (selected is null)
            {
                return UpdateCheckResult.NoCompatibleAsset(
                    CurrentVersion, latestVersion, releaseUrl,
                    _platform.OperatingSystem, _platform.Architecture);
            }

            return UpdateCheckResult.NewVersion(
                CurrentVersion, latestVersion, releaseUrl, selected.DownloadUrl,
                _platform.OperatingSystem, _platform.Architecture, selected.Name,
                isCompatibleAssetAvailable: true);
        }
        catch (OperationCanceledException)
        {
            return UpdateCheckResult.Failed(CurrentVersion, "Update-Check abgebrochen.");
        }
        catch (HttpRequestException ex)
        {
            return UpdateCheckResult.Failed(CurrentVersion, $"Netzwerkfehler beim Update-Check: {ex.Message}");
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(CurrentVersion, $"Update-Check fehlgeschlagen: {ex.Message}");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="latest"/> is strictly newer than
    /// <paramref name="current"/> using semantic versioning rules.
    /// Falls back to string comparison when parsing fails.
    /// </summary>
    internal static bool IsNewerVersion(string current, string latest)
    {
        if (string.IsNullOrWhiteSpace(latest)) return false;
        if (Version.TryParse(current, out var c) && Version.TryParse(latest, out var l))
            return l > c;
        return string.Compare(latest, current, StringComparison.Ordinal) > 0;
    }
}
