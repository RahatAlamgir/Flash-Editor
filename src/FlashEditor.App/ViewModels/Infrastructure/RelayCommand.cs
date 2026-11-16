using System.Windows.Input;

namespace FlashEditor.App.ViewModels.Infrastructure;

/// <summary>An <see cref="ICommand"/> that runs a delegate. CanExecute is re-checked automatically by WPF.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute()) { }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter)) _execute(parameter);
    }

    /// <summary>Asks WPF to re-evaluate every command's CanExecute (e.g. after undo).</summary>
    public static void Refresh() => CommandManager.InvalidateRequerySuggested();
}
