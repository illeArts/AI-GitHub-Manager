using System.Text;
using System.Text.RegularExpressions;
using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Remote;

namespace AI.GitHubManager.Core.Diagnostics;

/// <summary>
/// Builds a human- and AI-readable diagnostic report a non-technical user can
/// hand to a developer (or paste into an AI chat) for help — with every secret
/// automatically redacted. Never includes: tokens, passwords, Authorization
/// headers, credential manager contents, private keys, or raw environment
/// variable values.
/// </summary>
public static class DiagnosticReportService
{
    public static string Generate(
        string appVersion,
        string osDescription,
        string gitVersion,
        string ghVersion,
        string repositoryPath,
        string? remoteUrl,
        string branch,
        AuthenticationDiagnosis? authentication,
        IReadOnlyList<PreflightItem>? preflightItems = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AI GitHub Manager — Diagnosebericht");
        sb.AppendLine($"Erstellt: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine($"Programmversion: {appVersion}");
        sb.AppendLine($"Betriebssystem:  {osDescription}");
        sb.AppendLine($"Git-Version:     {gitVersion}");
        sb.AppendLine($"GitHub-CLI:      {ghVersion}");
        sb.AppendLine();
        sb.AppendLine($"Repositorypfad:  {repositoryPath}");
        sb.AppendLine($"Remote (sicher): {(string.IsNullOrWhiteSpace(remoteUrl) ? "-" : RemoteUrlNormalizer.Sanitize(remoteUrl))}");
        sb.AppendLine($"Branch:          {(string.IsNullOrWhiteSpace(branch) ? "-" : branch)}");
        sb.AppendLine();

        if (authentication is not null)
        {
            sb.AppendLine("Authentifizierung:");
            sb.AppendLine($"  Status:            {authentication.State}");
            sb.AppendLine($"  Aktives Konto:     {authentication.ActiveAccount ?? "-"}");
            sb.AppendLine($"  Scopes:            {(authentication.Scopes.Count > 0 ? string.Join(", ", authentication.Scopes) : "-")}");
            if (authentication.MissingScopes.Count > 0)
                sb.AppendLine($"  Fehlende Scopes:   {string.Join(", ", authentication.MissingScopes)}");
            if (!string.IsNullOrWhiteSpace(authentication.RelevantEnvironmentVariable))
                sb.AppendLine($"  Betroffene Variable: {authentication.RelevantEnvironmentVariable} (Wert nicht enthalten)");
            sb.AppendLine();
        }

        if (preflightItems is { Count: > 0 })
        {
            sb.AppendLine("Prüfungen / Reparaturschritte:");
            foreach (var item in preflightItems)
                sb.AppendLine($"  [{item.Severity}] {item.Label}: {item.Message}");
            sb.AppendLine();
        }

        sb.AppendLine("Hinweis: Dieser Bericht enthält keine Tokens, Passwörter oder sonstigen Zugangsdaten.");

        return Redact(sb.ToString());
    }

    /// <summary>Strips anything resembling a secret, as a last line of defense before export.</summary>
    private static string Redact(string text)
    {
        text = Regex.Replace(text,
            @"\b(gh[oprsu]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})\b",
            "****redacted****", RegexOptions.None);
        text = Regex.Replace(text,
            @"(?<name>GITHUB_TOKEN|GH_TOKEN)\s*=\s*\S+",
            "${name}=****redacted****", RegexOptions.IgnoreCase);
        text = Regex.Replace(text,
            @"(?i)Authorization:\s*\S+",
            "Authorization: ****redacted****");
        return text;
    }
}
