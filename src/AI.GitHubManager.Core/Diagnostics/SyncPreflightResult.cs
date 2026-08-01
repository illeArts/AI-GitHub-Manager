using AI.GitHubManager.Core.GitHub;
using AI.GitHubManager.Core.Remote;

namespace AI.GitHubManager.Core.Diagnostics;

/// <summary>
/// Severity of a single preflight check item.
/// </summary>
public enum PreflightSeverity
{
    Ok,
    Warning,
    Error
}

/// <summary>
/// Result of a single pre-push check step.
/// </summary>
public sealed record PreflightItem(
    string Label,
    PreflightSeverity Severity,
    string Message,
    bool CanAutoRepair = false,
    string? RepairActionId = null);

/// <summary>
/// Aggregated result of all pre-push checks for one project.
/// </summary>
public sealed class SyncPreflightResult
{
    public IReadOnlyList<PreflightItem> Items { get; }

    /// <summary>True when no item has severity Error.</summary>
    public bool CanPush => Items.All(i => i.Severity != PreflightSeverity.Error);

    /// <summary>True when at least one item has severity Warning.</summary>
    public bool HasWarnings => Items.Any(i => i.Severity == PreflightSeverity.Warning);

    /// <summary>Active branch detected during preflight (empty if unknown).</summary>
    public string Branch { get; }

    /// <summary>Remote origin URL detected during preflight (empty if unknown).</summary>
    public string RemoteOrigin { get; }

    /// <summary>Full authentication diagnosis for this preflight run, if it got that far.</summary>
    public AuthenticationDiagnosis? Authentication { get; }

    /// <summary>Parsed remote origin info (host/owner/repo), if the URL could be parsed.</summary>
    public RemoteUrlInfo? RemoteInfo { get; }

    public SyncPreflightResult(
        IEnumerable<PreflightItem> items,
        string branch = "",
        string remoteOrigin = "",
        AuthenticationDiagnosis? authentication = null,
        RemoteUrlInfo? remoteInfo = null)
    {
        Items          = items.ToList().AsReadOnly();
        Branch         = branch;
        RemoteOrigin   = remoteOrigin;
        Authentication = authentication;
        RemoteInfo     = remoteInfo;
    }

    /// <summary>Human-readable summary suitable for the Log panel.</summary>
    public string ToLogText()
    {
        var lines = new System.Text.StringBuilder();
        foreach (var item in Items)
        {
            var icon = item.Severity switch
            {
                PreflightSeverity.Ok      => "✅",
                PreflightSeverity.Warning => "⚠️",
                PreflightSeverity.Error   => "❌",
                _                         => "  "
            };
            lines.AppendLine($"{icon} {item.Label}: {item.Message}");
            if (item.CanAutoRepair)
                lines.AppendLine("   → Automatische Reparatur verfügbar.");
        }
        if (!string.IsNullOrWhiteSpace(Branch))
            lines.AppendLine($"\nAktiver Branch: {Branch}");
        if (!string.IsNullOrWhiteSpace(RemoteOrigin))
        {
            var display = RemoteUrlNormalizer.ContainsCredentials(RemoteOrigin)
                ? RemoteUrlNormalizer.Sanitize(RemoteOrigin) + "  (Zugangsdaten in der Anzeige entfernt)"
                : RemoteOrigin;
            lines.AppendLine($"Remote origin:  {display}");
        }

        lines.AppendLine();
        lines.AppendLine(CanPush
            ? "→ Alle kritischen Checks bestanden. Push möglich."
            : BuildBlockedSummary());

        return lines.ToString().TrimEnd();
    }

    private string BuildBlockedSummary()
    {
        var blocking = Items.Where(i => i.Severity == PreflightSeverity.Error).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("→ Push blockiert wegen:");
        foreach (var item in blocking)
            sb.AppendLine($"  - {item.Label}: {item.Message}");
        if (blocking.Any(i => i.CanAutoRepair))
            sb.Append("Automatische Reparatur verfügbar.");
        return sb.ToString().TrimEnd();
    }
}
