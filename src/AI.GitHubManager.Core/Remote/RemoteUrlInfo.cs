namespace AI.GitHubManager.Core.Remote;

/// <summary>
/// A parsed, structured view of a git remote URL (HTTPS or SSH). Never carries
/// the raw credential value — only whether one was present and a masked form.
/// </summary>
public sealed record RemoteUrlInfo(
    string Host,
    string Owner,
    string Repository,
    bool IsSsh,
    bool ContainsCredentials,
    string? MaskedCredential)
{
    /// <summary>The canonical, credential-free HTTPS remote URL for this repository.</summary>
    public string CanonicalHttpsUrl => $"https://{Host}/{Owner}/{Repository}.git";

    /// <summary>The canonical SSH remote URL for this repository.</summary>
    public string CanonicalSshUrl => $"git@{Host}:{Owner}/{Repository}.git";

    /// <summary>The browsable GitHub web page for this repository — no ".git", no trailing slash.</summary>
    public string WebUrl => $"https://{Host}/{Owner}/{Repository}";
}
