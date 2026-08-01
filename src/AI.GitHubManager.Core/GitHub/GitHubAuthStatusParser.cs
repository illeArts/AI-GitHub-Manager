using System.Text.RegularExpressions;
using AI.GitHubManager.Core.Process;

namespace AI.GitHubManager.Core.GitHub;

/// <summary>
/// Parses the human-readable output of <c>gh auth status</c> into a structured
/// <see cref="GitHubAuthStatusInfo"/>. `gh` has no `--json` mode for this command,
/// so we parse the text it prints on stdout/stderr, line by line.
///
/// Example (invalid GITHUB_TOKEN hiding a valid keyring login):
/// <code>
/// X Failed to log in to github.com using token (GITHUB_TOKEN)
/// - The token in GITHUB_TOKEN is invalid.
///
/// ✓ Logged in to github.com account illeArts (keyring)
/// - Active account: false
/// - Token scopes: gist, read:org, repo, workflow
/// </code>
/// </summary>
public static class GitHubAuthStatusParser
{
    private static readonly Regex LoginLine =
        new(@"Logged in to \S+ account (?<account>\S+)\s*\((?<source>[^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FailedTokenLine =
        new(@"Failed to log in to \S+ using token \((?<var>[A-Z_]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InvalidTokenLine =
        new(@"token in (?<var>[A-Z_]+) is invalid", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ActiveAccountLine =
        new(@"Active account:\s*(?<value>true|false)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ScopesLine =
        new(@"Token scopes?:\s*(?<scopes>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Defense-in-depth: gh never prints the raw token in `auth status` output,
    // but redact anything that looks like one before it is ever stored/shown.
    private static readonly Regex TokenLikeValue =
        new(@"\b(gh[oprsu]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})\b", RegexOptions.Compiled);

    public static GitHubAuthStatusInfo Parse(CommandResult result)
    {
        var raw = Redact(result.CombinedOutput);
        var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

        bool loggedIn = false;
        string? account = null;
        string? source = null;
        bool? activeAccount = null;
        var scopes = new List<string>();
        bool hasTokenFailure = false;
        string? failedTokenVariable = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var login = LoginLine.Match(line);
            if (login.Success)
            {
                loggedIn = true;
                account = login.Groups["account"].Value;
                source = login.Groups["source"].Value;
                continue;
            }

            var failed = FailedTokenLine.Match(line);
            if (failed.Success)
            {
                hasTokenFailure = true;
                failedTokenVariable = failed.Groups["var"].Value;
                continue;
            }

            var invalid = InvalidTokenLine.Match(line);
            if (invalid.Success)
            {
                hasTokenFailure = true;
                failedTokenVariable ??= invalid.Groups["var"].Value;
                continue;
            }

            var active = ActiveAccountLine.Match(line);
            if (active.Success)
            {
                activeAccount = active.Groups["value"].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            var scopeMatch = ScopesLine.Match(line);
            if (scopeMatch.Success)
            {
                var parsed = scopeMatch.Groups["scopes"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => s.Trim('\'', '"'))
                    .Where(s => s.Length > 0);
                scopes.AddRange(parsed);
            }
        }

        return new GitHubAuthStatusInfo(
            result.Success,
            loggedIn,
            account,
            source,
            source is not null && source.Contains("keyring", StringComparison.OrdinalIgnoreCase),
            activeAccount,
            scopes,
            hasTokenFailure,
            failedTokenVariable,
            raw);
    }

    private static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return TokenLikeValue.Replace(text, m => Mask(m.Value));
    }

    private static string Mask(string value)
        => value.Length <= 8 ? "****" : $"{value[..4]}****{value[^4..]}";
}
