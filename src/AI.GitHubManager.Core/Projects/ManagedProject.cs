using System.Text.Json.Serialization;

namespace AI.GitHubManager.Core.Projects;

public sealed class ManagedProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string RemoteUrl { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public string WindowsPath { get; set; } = string.Empty;
    public string MacPath { get; set; } = string.Empty;
    public string LinuxPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── GitHub link (Teil: "Auf GitHub öffnen") ─────────────────────────────
    // These are the real source of truth for the project's GitHub page going
    // forward — Owner/RemoteUrl above stay for back-compat with existing code
    // and existing projects.json files. Never fabricated from the logged-in
    // gh account: always derived from the real git remote (RemoteSource.GitOrigin),
    // a validated manual entry (RemoteSource.Manual), or gh's own repo list
    // (RemoteSource.Imported).

    /// <summary>e.g. "https://github.com/illeArts-Finance/bullbear" — no ".git", no trailing slash.</summary>
    public string RepositoryWebUrl { get; set; } = string.Empty;

    /// <summary>Owner or organisation login, e.g. "illeArts-Finance".</summary>
    public string RepositoryOwner { get; set; } = string.Empty;

    /// <summary>Bare repository name, e.g. "bullbear".</summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>How <see cref="RepositoryWebUrl"/> was determined.</summary>
    public RemoteSource RemoteSource { get; set; } = RemoteSource.Unknown;

    /// <summary>Computed, not persisted — true once a real GitHub link is known.</summary>
    [JsonIgnore]
    public bool HasGitHubLink => !string.IsNullOrWhiteSpace(RepositoryWebUrl);

    public string GetPathForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return WindowsPath;
        if (OperatingSystem.IsMacOS()) return MacPath;
        return LinuxPath;
    }
}
