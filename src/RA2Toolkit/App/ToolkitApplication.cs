using System.Windows;
using System.Windows.Threading;

internal sealed class ToolkitApplication : System.Windows.Application
{
    private GameSessionHost? gameHost;
    private GlobalHotkeyService? hotkeys;
    private MainWindow? window;
    private MainWindowViewModel? viewModel;
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
            window = new MainWindow(viewModel);
            MainWindow = window;

            gameHost.StateChanged += HandleStateChanged;
            gameHost.StatusChanged += HandleStatusChanged;
            gameHost.Completed += HandleHostCompleted;
            window.Show();

            try
            {
                hotkeys = new GlobalHotkeyService();
                hotkeys.Pressed += HandleGlobalHotkey;
            }
            catch (Exception error)
            {
                viewModel.ShowStatus($"全局快捷键不可用：{error.Message}", true);
            }

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
        // The hook fires before WPF handles the same key. Drop it before queuing,
        // otherwise completing capture can make the newly assigned shortcut run.
        if (target is null || target.IsCapturingHotkey)
            return;
        DispatchToUi(() => target.HandleGlobalHotkey(gesture));
    }

    private void HandleHostCompleted()
    {
        DispatchToUi(() =>
        {
            if (shuttingDown)
                return;
            shuttingDown = true;
            window?.RequestClose();
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
        base.OnExit(eventArgs);
    }
}
