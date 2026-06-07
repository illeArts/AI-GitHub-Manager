using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace AI.GitHubManager.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build ?? 0}";
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
