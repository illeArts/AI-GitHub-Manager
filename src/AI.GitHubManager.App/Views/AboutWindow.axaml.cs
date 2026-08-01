using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AI.GitHubManager.App.Services;

namespace AI.GitHubManager.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build ?? 0}";

        ApplyStrings();
        L.Changed += ApplyStrings;
        Closed += (_, _) => L.Changed -= ApplyStrings;
    }

    private void ApplyStrings()
    {
        Title = L.T("Über AI GitHub Manager", "About AI GitHub Manager");
        TitleBarText.Text = L.T("Über", "About");
        DescriptionText.Text = L.T(
            "AI GitHub Manager — sicheres, einfaches GitHub-Management für alle deine Projekte. Push, Pull, Diagnose und GitHub CLI-Setup auf einen Klick. Läuft auf Windows und macOS.",
            "AI GitHub Manager — secure, simple GitHub management for all your projects. Push, Pull, diagnostics, and GitHub CLI setup at the click of a button. Runs on Windows and macOS.");
    }

    private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();

    // Allow dragging the custom title bar
    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
