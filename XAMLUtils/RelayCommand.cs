namespace SylverInk.XAMLUtils;

/// <summary>
/// Encodes the MVVM design flow of requests made from the View to the ViewModel.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<object?, Task>? _asyncExecute;
    private readonly Action<object?>? _syncExecute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _syncExecute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Func<object?, Task> asyncExecute, Predicate<object?>? canExecute = null)
    {
        _asyncExecute = asyncExecute ?? throw new ArgumentNullException(nameof(asyncExecute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public async void Execute(object? parameter)
    {
        if (_asyncExecute != null)
            await _asyncExecute(parameter);
        else
            _syncExecute?.Invoke(parameter);
    }
}
