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
    string Message);

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

    public SyncPreflightResult(IEnumerable<PreflightItem> items, string branch = "", string remoteOrigin = "")
    {
        Items        = items.ToList().AsReadOnly();
        Branch       = branch;
        RemoteOrigin = remoteOrigin;
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
        }
        if (!string.IsNullOrWhiteSpace(Branch))
            lines.AppendLine($"\nAktiver Branch: {Branch}");
        if (!string.IsNullOrWhiteSpace(RemoteOrigin))
            lines.AppendLine($"Remote origin:  {RemoteOrigin}");

        lines.AppendLine();
        lines.AppendLine(CanPush
            ? "→ Alle kritischen Checks bestanden. Push möglich."
            : "→ Push blockiert. Bitte Fehler beheben.");

        return lines.ToString().TrimEnd();
    }
}
