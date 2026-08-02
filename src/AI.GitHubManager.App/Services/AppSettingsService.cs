using System.Text.Json;
using AI.GitHubManager.Core.Operations;

namespace AI.GitHubManager.App.Services;

public sealed class AppSettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AI.GitHubManager", "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // ── Settings model ────────────────────────────────────────────────────────

    public string Language { get; set; } = "de"; // "de" | "en"

    /// <summary>
    /// "Sicherer Pull mit automatischer Schutzsicherung" — default enabled.
    /// When true, a Pull with local changes present automatically creates a
    /// git stash backup, pulls with --ff-only, and restores the backup.
    /// When false, a Pull with local changes present is aborted with a warning
    /// instead (no automatic stash/backup is created).
    /// </summary>
    public bool SafePullEnabled { get; set; } = true;

    /// <summary>
    /// Id of the last selected normal Git operation (Teil B1/C). Only ever
    /// set to an operation whose risk level is Safe or Caution — see
    /// <see cref="SetSelectedOperationIfSafe"/>. Dangerous or Advanced
    /// operations are never persisted as the next default (Teil B7/C).
    /// </summary>
    public string SelectedOperationId { get; set; } = GitOperationCatalog.DefaultOperationId;

    /// <summary>
    /// Returns the persisted operation, falling back to the default
    /// ("Aktualisieren") when the stored id is missing, unknown (e.g. from
    /// an older or corrupted settings file), or — defensively — refers to a
    /// dangerous/advanced operation that should never have been stored.
    /// Fault-tolerant loading of old/invalid settings (Teil A1).
    /// </summary>
    public GitOperationDefinition GetSelectedOperationOrDefault()
    {
        var found = GitOperationCatalog.Find(SelectedOperationId);
        if (found is not null && GitOperationCatalog.IsSafeToPersistAsDefault(found))
            return found;

        return GitOperationCatalog.Update;
    }

    /// <summary>
    /// Stores the selection only if it is safe to come back automatically
    /// next time (Teil B7/C: "Gefährliche Vorgänge dürfen niemals als
    /// letzte automatische Standardauswahl wiederhergestellt werden").
    /// Selecting a dangerous or advanced operation simply isn't remembered —
    /// the previous safe selection (or the default) stays persisted.
    /// </summary>
    public bool SetSelectedOperationIfSafe(GitOperationDefinition operation)
    {
        if (!GitOperationCatalog.IsSafeToPersistAsDefault(operation)) return false;
        SelectedOperationId = operation.Id;
        return true;
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public static AppSettingsService Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettingsService>(json, JsonOptions)
                       ?? new AppSettingsService();
            }
        }
        catch { /* corrupt file — fall back to defaults */ }

        return new AppSettingsService();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { /* non-fatal */ }
    }

    /// <summary>Applies the stored language to the localization service.</summary>
    public void ApplyLanguage()
    {
        L.Language = Language.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.English
            : AppLanguage.German;
    }
}
