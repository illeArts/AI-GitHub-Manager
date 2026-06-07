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
