using System.Windows.Input;

namespace AI.GitHubManager.App.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public async void Execute(object? parameter) => await _execute();

    /// <summary>Awaitable form of <see cref="Execute"/>, used by tests that need to
    /// observe side effects (e.g. ViewModel.Log) only after the command has fully
    /// completed, rather than racing the async-void UI entry point.</summary>
    public Task ExecuteAsync(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
