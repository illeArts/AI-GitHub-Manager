using System.Text.RegularExpressions;

namespace AI.GitHubManager.Core.Remote;

/// <summary>
/// Parses, compares, and sanitizes git remote URLs. A token must never be part
/// of a remote URL — the canonical form is always
/// <c>https://github.com/&lt;owner&gt;/&lt;repository&gt;.git</c> (or the SSH equivalent).
///
/// Accepts and treats as equivalent:
///   https://github.com/owner/repo.git
///   https://github.com/owner/repo
///   git@github.com:owner/repo.git
///   ssh://git@github.com/owner/repo.git
/// </summary>
public static class RemoteUrlNormalizer
{
    private static readonly Regex HttpsPattern = new(
        @"^https://(?:(?<cred>[^@/]+)@)?(?<host>[^/]+)/(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SshUriPattern = new(
        @"^ssh://(?:[^@/]+@)?(?<host>[^/]+)/(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SshShorthandPattern = new(
        @"^[^@\s]+@(?<host>[^:\s]+):(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PlaceholderTokenPattern = new(
        @"DEIN_VORHANDENER_TOKEN|<\s*[^>]*TOKEN[^>]*>|YOUR[_-]?TOKEN|\bDEIN[_-]\w*TOKEN\w*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "owner/repo" shorthand, only accepted for manual entry (never for parsing
    // a real git remote) — GitHub login/org name rules: alphanumeric and single
    // hyphens, not starting/ending with one; repo names allow dots/underscores.
    private static readonly Regex OwnerRepoShorthandPattern = new(
        @"^(?<owner>[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})?)/(?<repo>[A-Za-z0-9._-]+)$",
        RegexOptions.Compiled);

    /// <summary>Parses a remote URL. Returns null when the format is not recognised.</summary>
    public static RemoteUrlInfo? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();

        var https = HttpsPattern.Match(url);
        if (https.Success)
        {
            var cred = https.Groups["cred"].Success ? https.Groups["cred"].Value : null;
            return new RemoteUrlInfo(
                https.Groups["host"].Value,
                https.Groups["owner"].Value,
                https.Groups["repo"].Value,
                IsSsh: false,
                ContainsCredentials: !string.IsNullOrEmpty(cred),
                MaskedCredential: string.IsNullOrEmpty(cred) ? null : Mask(cred));
        }

        var sshUri = SshUriPattern.Match(url);
        if (sshUri.Success)
        {
            return new RemoteUrlInfo(
                sshUri.Groups["host"].Value, sshUri.Groups["owner"].Value, sshUri.Groups["repo"].Value,
                IsSsh: true, ContainsCredentials: false, MaskedCredential: null);
        }

        var sshShort = SshShorthandPattern.Match(url);
        if (sshShort.Success)
        {
            return new RemoteUrlInfo(
                sshShort.Groups["host"].Value, sshShort.Groups["owner"].Value, sshShort.Groups["repo"].Value,
                IsSsh: true, ContainsCredentials: false, MaskedCredential: null);
        }

        return null;
    }

    /// <summary>
    /// Compares two remote URLs semantically (host, owner, repository, case-insensitively,
    /// ignoring an optional trailing .git and HTTPS vs. SSH form).
    /// </summary>
    public static bool AreEquivalent(string? a, string? b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        if (pa is null || pb is null) return false;

        return string.Equals(pa.Host, pb.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(pa.Owner, pb.Owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(pa.Repository, pb.Repository, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses user-typed manual GitHub link entry: accepts everything <see cref="Parse"/>
    /// does, plus the bare "owner/repo" shorthand (assumed to be on github.com). Used only
    /// for the manual-link dialog — never used to interpret a real git remote URL, and
    /// never falls back to fabricating an owner from the logged-in GitHub CLI account.
    /// Returns null when the input cannot be confidently parsed either way.
    /// </summary>
    public static RemoteUrlInfo? ParseManualEntry(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();

        var direct = Parse(trimmed);
        if (direct is not null) return direct;

        var shorthand = OwnerRepoShorthandPattern.Match(trimmed);
        if (shorthand.Success)
        {
            return new RemoteUrlInfo(
                "github.com",
                shorthand.Groups["owner"].Value,
                shorthand.Groups["repo"].Value,
                IsSsh: false,
                ContainsCredentials: false,
                MaskedCredential: null);
        }

        return null;
    }

    /// <summary>True when the URL embeds credentials (e.g. https://TOKEN@github.com/...).</summary>
    public static bool ContainsCredentials(string? url) => Parse(url)?.ContainsCredentials ?? false;

    /// <summary>
    /// True when the URL contains a known unresolved placeholder such as
    /// <c>DEIN_VORHANDENER_TOKEN</c> — these must never appear in a real, working remote URL.
    /// </summary>
    public static bool ContainsPlaceholderToken(string? url)
        => !string.IsNullOrWhiteSpace(url) && PlaceholderTokenPattern.IsMatch(url);

    /// <summary>
    /// Returns the canonical, credential-free form of a remote URL. SSH URLs are
    /// normalized to the canonical SSH shorthand; everything else becomes HTTPS.
    /// Falls back to the original string when the URL cannot be parsed at all
    /// (e.g. a placeholder that was never a real URL).
    /// </summary>
    public static string Sanitize(string? url)
    {
        var info = Parse(url);
        if (info is null) return url ?? string.Empty;
        return info.IsSsh ? info.CanonicalSshUrl : info.CanonicalHttpsUrl;
    }

    private static string Mask(string credential)
    {
        // credential may be "user:token" or just "token" — mask only the secret part.
        var separator = credential.IndexOf(':');
        var token = separator >= 0 ? credential[(separator + 1)..] : credential;
        if (token.Length == 0) return "****";
        return token.Length <= 8 ? "****" : $"{token[..4]}****{token[^4..]}";
    }
}
