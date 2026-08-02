using Avalonia.Controls;
using AI.GitHubManager.App.ViewModels;

namespace AI.GitHubManager.App.Views;

/// <summary>
/// Hosts "Erweiterte Befehle" (Rebase/Cherry-Pick/Force Push/Reset/Hard Reset/Clean),
/// moved out of the main window so dangerous operations are never permanently
/// visible in the main work area (Auftragserweiterung UI-Polishing #3). The
/// confirmation-dialog safety mechanism (ConfirmDangerousOperationWindow) is
/// unchanged — this window only relocates the entry points.
/// </summary>
public partial class AdvancedOperationsWindow : Window
{
    public AdvancedOperationsWindow() => InitializeComponent();

    public AdvancedOperationsWindow(MainWindowViewModel vm) : this()
    {
        DataContext = vm;
    }
}
