using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

internal readonly record struct GameWindowRectangle(
    int Left,
    int Top,
    int Right,
    int Bottom)
{
    internal int Width => Right - Left;
    internal int Height => Bottom - Top;
}

internal readonly record struct GameWindowSnapshot(
    nint Handle,
    int ProcessId,
    GameWindowRectangle Bounds,
    uint Dpi,
    bool IsForeground);

internal sealed class GameWindowTracker : IDisposable
{
    private static readonly string[] ProcessNames =
        ["gamemd", "gamemd-ares", "gamemd-spawn"];

    private readonly DispatcherTimer timer;
    private bool disposed;

    internal GameWindowTracker()
    {
        timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        timer.Tick += HandleTimerTick;
    }

    internal event Action<GameWindowSnapshot?>? Updated;

    internal GameWindowSnapshot? Current { get; private set; }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (timer.IsEnabled)
            return;
        Refresh();
        timer.Start();
    }

    private void HandleTimerTick(object? sender, EventArgs eventArgs) => Refresh();

    private void Refresh()
    {
        Current = FindGameWindow();
        Updated?.Invoke(Current);
    }

    private static GameWindowSnapshot? FindGameWindow()
    {
        foreach (var processName in ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var handle = process.MainWindowHandle;
                        if (handle == 0 || !IsWindow(handle) || !IsWindowVisible(handle) ||
                            IsIconic(handle) || !TryGetClientBounds(handle, out var bounds))
                            continue;

                        var foregroundWindow = GetForegroundWindow();
                        _ = GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId);
                        var dpi = GetDpiForWindow(handle);
                        return new GameWindowSnapshot(
                            handle,
                            process.Id,
                            bounds,
                            dpi == 0 ? 96u : dpi,
                            foregroundProcessId == (uint)process.Id);
                    }
                    catch (Exception error) when (error is InvalidOperationException or
                                                  System.ComponentModel.Win32Exception)
                    {
                        // The process can exit while its window information is queried.
                    }
                }
            }
        }

        return null;
    }

    private static bool TryGetClientBounds(
        nint window, out GameWindowRectangle bounds)
    {
        bounds = default;
        if (!GetClientRect(window, out var client) ||
            client.Right <= client.Left || client.Bottom <= client.Top)
            return false;

        var upperLeft = new NativePoint(client.Left, client.Top);
        var lowerRight = new NativePoint(client.Right, client.Bottom);
        if (!ClientToScreen(window, ref upperLeft) ||
            !ClientToScreen(window, ref lowerRight))
            return false;

        bounds = new GameWindowRectangle(
            upperLeft.X, upperLeft.Y, lowerRight.X, lowerRight.Y);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        timer.Stop();
        timer.Tick -= HandleTimerTick;
        Updated = null;
        Current = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint window, ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
}
