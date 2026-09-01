using System.Windows;
using System.Windows.Threading;

internal sealed class ToolkitApplication : System.Windows.Application
{
    private GameSessionHost? gameHost;
    private GlobalHotkeyService? hotkeys;
    private FloatingOverlayWindow? overlayWindow;
    private GameWindowTracker? gameWindowTracker;
    private MainWindowViewModel? viewModel;
    private System.Windows.Forms.NotifyIcon? trayIcon;
    private System.Drawing.Icon? trayImage;
    private bool shuttingDown;

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += HandleDispatcherException;

        try
        {
            gameHost = new GameSessionHost();
            viewModel = new MainWindowViewModel(gameHost.Dispatch);
            gameWindowTracker = new GameWindowTracker();
            overlayWindow = new FloatingOverlayWindow(viewModel, gameWindowTracker);
            MainWindow = overlayWindow;

            gameHost.StateChanged += HandleStateChanged;
            gameHost.StatusChanged += HandleStatusChanged;
            gameHost.Completed += HandleHostCompleted;
            gameWindowTracker.Start();

            var trayMenu = new System.Windows.Forms.ContextMenuStrip();
            _ = trayMenu.Items.Add("退出 RA2 Toolkit", null,
                (_, _) => viewModel.ExitCommand.Execute(null));
            trayImage = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                ContextMenuStrip = trayMenu,
                Icon = trayImage ?? System.Drawing.SystemIcons.Application,
                Text = "RA2 Toolkit",
                Visible = true
            };

            try
            {
                hotkeys = new GlobalHotkeyService();
                hotkeys.Pressed += HandleGlobalHotkey;
            }
            catch (Exception error)
            {
                viewModel.ShowStatus($"全局快捷键不可用：{error.Message}", true);
            }

            viewModel.CheckUpdatesNow();
            gameHost.Start();
        }
        catch (Exception error)
        {
            System.Windows.MessageBox.Show(
                $"启动失败：{error.Message}",
                "RA2 Toolkit",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void HandleStateChanged(OverlayState state) =>
        DispatchToUi(() => viewModel?.ApplyState(state));

    private void HandleStatusChanged(string message, bool isError) =>
        DispatchToUi(() => viewModel?.ShowStatus(message, isError));

    private void HandleGlobalHotkey(HotkeyGesture gesture)
    {
        var target = viewModel;
        if (target is null)
            return;
        // Snapshot capture state before queuing so a newly assigned shortcut is not
        // executed when the same key later reaches the UI thread.
        var isCapturing = target.IsCapturingHotkey;
        DispatchToUi(() =>
        {
            if (isCapturing)
                target.HandleCapturedHotkey(gesture);
            else
                target.HandleGlobalHotkey(gesture);
        });
    }

    private void HandleHostCompleted()
    {
        DispatchToUi(() =>
        {
            if (shuttingDown)
                return;
            shuttingDown = true;
            overlayWindow?.RequestClose();
            Shutdown();
        });
    }

    private void HandleDispatcherException(
        object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        viewModel?.ShowStatus($"界面操作失败：{eventArgs.Exception.Message}", true);
        eventArgs.Handled = true;
    }

    private void DispatchToUi(Action action)
    {
        if (Dispatcher.HasShutdownStarted)
            return;
        _ = Dispatcher.BeginInvoke(action, DispatcherPriority.DataBind);
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        shuttingDown = true;
        DispatcherUnhandledException -= HandleDispatcherException;
        if (trayIcon is not null)
        {
            trayIcon.Visible = false;
            trayIcon.ContextMenuStrip?.Dispose();
            trayIcon.Dispose();
            trayIcon = null;
        }
        trayImage?.Dispose();
        trayImage = null;
        if (overlayWindow is not null)
        {
            overlayWindow.RequestClose();
            overlayWindow = null;
        }
        gameWindowTracker?.Dispose();
        gameWindowTracker = null;
        if (hotkeys is not null)
        {
            hotkeys.Pressed -= HandleGlobalHotkey;
            hotkeys.Dispose();
            hotkeys = null;
        }
        if (gameHost is not null)
        {
            gameHost.StateChanged -= HandleStateChanged;
            gameHost.StatusChanged -= HandleStatusChanged;
            gameHost.Completed -= HandleHostCompleted;
            gameHost.Dispose();
            gameHost = null;
        }
        viewModel?.Dispose();
        viewModel = null;
        base.OnExit(eventArgs);
    }
}
