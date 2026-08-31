using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private bool allowClose;

    internal MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CaptureStateChanged += HandleCaptureStateChanged;
        PreviewKeyDown += HandlePreviewKeyDown;
        Loaded += HandleLoaded;
        Closing += HandleClosing;
        Closed += HandleClosed;
    }

    internal void RequestClose()
    {
        allowClose = true;
        Close();
    }

    private void HandleLoaded(object sender, RoutedEventArgs eventArgs) =>
        _ = viewModel.CheckUpdatesNowAsync();

    private void HandleCaptureStateChanged()
    {
        if (viewModel.IsCapturingHotkey)
            Focus();
    }

    private void HandlePreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (!viewModel.IsCapturingHotkey)
            return;

        var key = eventArgs.Key switch
        {
            Key.System => eventArgs.SystemKey,
            Key.ImeProcessed => eventArgs.ImeProcessedKey,
            _ => eventArgs.Key
        };
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            eventArgs.Handled = true;
            return;
        }
        if (key == Key.Escape)
        {
            viewModel.CancelHotkeyCapture();
            eventArgs.Handled = true;
            return;
        }
        if (key is Key.Delete or Key.Back)
        {
            viewModel.ClearCapturedHotkey();
            eventArgs.Handled = true;
            return;
        }

        var modifiers = 0u;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            modifiers |= HotkeyGesture.Control;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            modifiers |= HotkeyGesture.Shift;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            modifiers |= HotkeyGesture.Alt;
        var gesture = new HotkeyGesture(modifiers, KeyInterop.VirtualKeyFromKey(key));
        if (!viewModel.TryCaptureHotkey(gesture, out var error) && error is not null)
            System.Windows.MessageBox.Show(this, error, "快捷键冲突", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        eventArgs.Handled = true;
    }

    private void HandleClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (allowClose)
            return;
        eventArgs.Cancel = true;
        viewModel.ShowStatus("正在安全恢复游戏状态并退出…");
        viewModel.ExitCommand.Execute(null);
    }

    private void HandleClosed(object? sender, EventArgs eventArgs)
    {
        viewModel.CaptureStateChanged -= HandleCaptureStateChanged;
        PreviewKeyDown -= HandlePreviewKeyDown;
        Loaded -= HandleLoaded;
        Closing -= HandleClosing;
        Closed -= HandleClosed;
        viewModel.Dispose();
    }
}
