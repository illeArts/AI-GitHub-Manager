using System.Text.Json;

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
