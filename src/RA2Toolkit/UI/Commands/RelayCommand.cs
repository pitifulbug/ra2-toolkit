using System.Windows.Input;

internal sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();

    internal void NotifyCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class AsyncRelayCommand(
    Func<CancellationToken, Task> execute,
    Func<bool>? canExecute = null) : ICommand
{
    private CancellationTokenSource? cancellation;
    private bool isRunning;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !isRunning && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        cancellation = new CancellationTokenSource();
        isRunning = true;
        NotifyCanExecuteChanged();
        try
        {
            await execute(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            isRunning = false;
            NotifyCanExecuteChanged();
        }
    }

    internal void Cancel() => cancellation?.Cancel();

    internal void NotifyCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
