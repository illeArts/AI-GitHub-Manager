using Avalonia.Controls;
using Avalonia.Input;
using AI.GitHubManager.App.Services;

namespace AI.GitHubManager.App.Views;

/// <summary>
/// Small generic yes/no confirmation dialog used by the project-list context
/// menu (e.g. "Remote erneut erkennen" overwrite confirmation, "Aus Manager
/// entfernen …"). No button is IsDefault, so Enter never confirms by accident;
/// Escape/No is always the safe path.
/// </summary>
public partial class ConfirmYesNoWindow : Window
{
    public ConfirmYesNoWindow() : this(string.Empty, string.Empty) { }

    public ConfirmYesNoWindow(string title, string message)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        NoButton.Content = L.T("Abbrechen", "Cancel");
        YesButton.Content = L.T("Bestätigen", "Confirm");

        KeyDown += (_, e) => { if (e.Key == Key.Enter) e.Handled = true; };
    }

    private void OnNo(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private void OnYes(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);
}
