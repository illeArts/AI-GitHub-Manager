using Avalonia.Controls;
using AI.GitHubManager.App.ViewModels;

namespace AI.GitHubManager.App.Views;

/// <summary>
/// Hosts the "Verständlicher Vorgang" (dropdown + risk/description/details panel),
/// moved out of the main window so the main work area stays compact
/// (Auftragserweiterung UI-Polishing #2). Shares the same MainWindowViewModel
/// instance as the main window — nothing here is a separate copy of state.
/// </summary>
public partial class OperationWindow : Window
{
    public OperationWindow() => InitializeComponent();

    public OperationWindow(MainWindowViewModel vm) : this()
    {
        DataContext = vm;
    }
}
