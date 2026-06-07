namespace AI.GitHubManager.App.Services;

public enum AppLanguage { German, English }

/// <summary>
/// Minimal bilingual helper. Usage: L.T("Deutsch", "English")
/// Subscribe to L.Changed to refresh UI on language switch.
/// </summary>
public static class L
{
    private static AppLanguage _language = AppLanguage.German;

    public static AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Fired when the language is switched. Dialogs should re-read their strings.</summary>
    public static event Action? Changed;

    /// <summary>Returns the German or English string depending on the current language.</summary>
    public static string T(string de, string en) => _language == AppLanguage.English ? en : de;

    public static bool IsEnglish => _language == AppLanguage.English;
}
