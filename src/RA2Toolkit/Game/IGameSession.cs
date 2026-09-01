internal interface IGameSession : IDisposable
{
    event Action<OverlayState>? StateChanged;
    event Action<string, bool>? OperationStatusChanged;

    bool ExitRequested { get; }
    void EnqueueCommand(OverlayCommandRequest request);
    void Run();
}

internal sealed partial class CratePicker : IGameSession
{
    event Action<OverlayState>? IGameSession.StateChanged
    {
        add => StateChanged += value;
        remove => StateChanged -= value;
    }

    event Action<string, bool>? IGameSession.OperationStatusChanged
    {
        add => OperationStatusChanged += value;
        remove => OperationStatusChanged -= value;
    }

    bool IGameSession.ExitRequested => ExitRequested;

    void IGameSession.EnqueueCommand(OverlayCommandRequest request) => EnqueueCommand(request);
}
