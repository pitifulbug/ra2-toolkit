internal sealed class GameSessionHost : IDisposable
{
    internal const string WaitingForGameStatus = "等待启动游戏";

    private readonly object gate = new();
    private readonly Func<IGameSession> createSession;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread worker;
    private IGameSession? activeSession;
    private bool started;
    private bool stopRequested;
    private bool disposed;
    private string? lastConnectionError;

    internal GameSessionHost(Func<IGameSession>? createSession = null)
    {
        this.createSession = createSession ?? (() => new CratePicker());
        worker = new Thread(WorkerMain)
        {
            IsBackground = true,
            Name = "RA2 Toolkit game worker"
        };
    }

    internal event Action<OverlayState>? StateChanged;
    internal event Action<string, bool>? StatusChanged;
    internal event Action? Completed;

    internal void Start()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (started)
                return;
            started = true;
            worker.Start();
        }
    }

    internal void Dispatch(OverlayCommand command)
    {
        IGameSession? session;
        lock (gate)
        {
            if (disposed)
                return;
            if (command == OverlayCommand.ExitProgram)
                stopRequested = true;
            session = activeSession;
        }

        if (session is not null)
        {
            try
            {
                session.EnqueueCommand(command);
            }
            catch (ObjectDisposedException)
            {
                // The game session ended between taking the snapshot and publishing
                // the command. The worker will either reconnect or complete shutdown.
            }
            return;
        }

        if (command == OverlayCommand.ExitProgram)
        {
            cancellation.Cancel();
            return;
        }
    }

    internal void RequestStop() => Dispatch(OverlayCommand.ExitProgram);

    private void WorkerMain()
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                IGameSession? session = null;
                try
                {
                    session = createSession();
                    session.StateChanged += ForwardState;
                    session.OperationStatusChanged += ForwardStatus;
                    lock (gate)
                    {
                        activeSession = session;
                        if (stopRequested)
                            session.EnqueueCommand(OverlayCommand.ExitProgram);
                    }

                    lastConnectionError = null;
                    ForwardStatus("已连接游戏对局。", false);
                    session.Run();
                    if (session.ExitRequested || IsStopRequested())
                        break;
                    ForwardStatus("游戏对局已结束，正在等待下一场对局…", false);
                }
                catch (GameProcessExitedException)
                {
                    ForwardStatus("游戏进程已结束，正在等待重新启动…", false);
                }
                catch (GameProcessNotFoundException)
                {
                    lastConnectionError = null;
                    ForwardStatus(WaitingForGameStatus, false);
                }
                catch (Exception error) when (error is InvalidOperationException or
                                              System.ComponentModel.Win32Exception)
                {
                    ReportConnectionError(error.Message);
                }
                catch (Exception error)
                {
                    ReportConnectionError($"游戏控制器发生错误：{error.Message}");
                }
                finally
                {
                    if (session is not null)
                    {
                        session.StateChanged -= ForwardState;
                        session.OperationStatusChanged -= ForwardStatus;
                        lock (gate)
                        {
                            if (ReferenceEquals(activeSession, session))
                                activeSession = null;
                        }
                        try
                        {
                            session.Dispose();
                        }
                        catch
                        {
                            // Session cleanup is already best-effort; the host must remain reusable.
                        }
                    }
                    ForwardState(OverlayState.Empty);
                }

                if (IsStopRequested() || cancellation.Token.WaitHandle.WaitOne(
                        TimeSpan.FromSeconds(1)))
                    break;
            }
        }
        finally
        {
            InvokeSafely(Completed);
        }
    }

    private bool IsStopRequested()
    {
        lock (gate)
            return stopRequested;
    }

    private void ReportConnectionError(string message)
    {
        if (string.Equals(lastConnectionError, message, StringComparison.Ordinal))
            return;
        lastConnectionError = message;
        ForwardStatus(message, true);
    }

    private void ForwardState(OverlayState state) => InvokeSafely(StateChanged, state);

    private void ForwardStatus(string message, bool isError) =>
        InvokeSafely(StatusChanged, message, isError);

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
        }

        RequestStopAfterDispose();
        if (started && worker.IsAlive && worker != Thread.CurrentThread)
        {
            if (!worker.Join(TimeSpan.FromSeconds(10)))
            {
                cancellation.Cancel();
                _ = worker.Join(TimeSpan.FromSeconds(2));
            }
        }
        if (!worker.IsAlive)
            cancellation.Dispose();
    }

    private void RequestStopAfterDispose()
    {
        IGameSession? session;
        lock (gate)
        {
            stopRequested = true;
            session = activeSession;
        }
        if (session is null)
        {
            cancellation.Cancel();
            return;
        }

        try
        {
            session.EnqueueCommand(OverlayCommand.ExitProgram);
        }
        catch (ObjectDisposedException)
        {
            cancellation.Cancel();
        }
    }

    private static void InvokeSafely(Action? callback)
    {
        try { callback?.Invoke(); }
        catch
        {
            // UI/event consumers must never terminate the game worker thread.
        }
    }

    private static void InvokeSafely<T>(Action<T>? callback, T argument)
    {
        try { callback?.Invoke(argument); }
        catch
        {
        }
    }

    private static void InvokeSafely<T1, T2>(Action<T1, T2>? callback,
        T1 first, T2 second)
    {
        try { callback?.Invoke(first, second); }
        catch
        {
        }
    }
}
