using Avalonia.Controls;
using AI.GitHubManager.App.Services;

namespace AI.GitHubManager.App.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettingsService _settings;
    private readonly AppLanguage _originalLanguage;   // restore on Cancel

    public SettingsWindow() : this(AppSettingsService.Load()) { }

    public SettingsWindow(AppSettingsService settings)
    {
        _settings         = settings;
        _originalLanguage = L.Language;   // snapshot before any live changes

        InitializeComponent();
        ApplyStrings();
        L.Changed += ApplyStrings;
        Closed += (_, _) => L.Changed -= ApplyStrings;

        LangEn.IsChecked = L.Language == AppLanguage.English;
        LangDe.IsChecked = L.Language == AppLanguage.German;

        SafePullOn.IsChecked       = _settings.SafePullEnabled;
        SafePullWarnOnly.IsChecked = !_settings.SafePullEnabled;
    }

    private void ApplyStrings()
    {
        Title              = L.T("Einstellungen", "Settings");
        TitleText.Text     = L.T("Einstellungen", "Settings");
        LangLabel.Text     = L.T("Sprache / Language", "Language / Sprache");
        LangNote.Text      = L.T("Sprachänderung wird sofort übernommen.",
                                 "Language change is applied immediately.");
        CancelButton.Content = L.T("Abbrechen", "Cancel");
        SaveButton.Content   = L.T("Speichern",  "Save");

        SafePullLabel.Text       = L.T("Pull-Verhalten bei lokalen Änderungen", "Pull behavior with local changes");
        SafePullOn.Content       = L.T("Sicherer Pull mit automatischer Schutzsicherung", "Safe pull with automatic protective backup");
        SafePullWarnOnly.Content = L.T("Bei lokalen Änderungen nur warnen und Pull abbrechen", "Only warn and abort the pull when local changes exist");
        SafePullNote.Text        = L.T(
            "Empfohlen: erstellt vor jedem Pull automatisch eine Git-Stash-Sicherung lokaler Änderungen " +
            "(inkl. neuer Dateien) und stellt sie nach dem Pull wieder her. Es werden nie lokale Änderungen " +
            "verworfen oder bestehende Stashes verändert.",
            "Recommended: automatically creates a git stash backup of local changes (including new files) " +
            "before every pull and restores it afterwards. Local changes are never discarded and existing " +
            "stashes are never touched.");
    }

    /// Live-preview: switch language immediately so the whole UI updates as the user
    /// moves between radio buttons.
    /// NOTE: uses sender identity, NOT IsChecked — Avalonia fires Checked before the
    /// sibling radio button's IsChecked is updated, so reading LangEn.IsChecked here
    /// would still return the old value.
    private void OnLanguageChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => L.Language = ReferenceEquals(sender, LangEn) ? AppLanguage.English : AppLanguage.German;

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _settings.Language = L.IsEnglish ? "en" : "de";
        _settings.SafePullEnabled = SafePullOn.IsChecked == true;
        _settings.Save();
        Close();
    }

    /// Cancel: revert to the language that was active when the dialog opened.
    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        L.Language = _originalLanguage;
        Close();
    }
}
