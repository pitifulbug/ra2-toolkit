using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

public partial class FloatingOverlayWindow : Window
{
    private const double OverlayWidth = 340;
    private const double OverlayHeight = 600;
    private const double CollapsedOverlayHeight = 112;
    private const double EdgeMargin = 12;
    private const int ExtendedWindowStyleIndex = -20;
    private const int ExtendedStyleNoActivate = 0x08000000;
    private const int ExtendedStyleToolWindow = 0x00000080;
    private const int MouseActivateMessage = 0x0021;
    private static readonly nint MouseActivateWithoutActivation = new(3);

    private static readonly nint HwndTopmost = new(-1);

    private readonly GameWindowTracker tracker;
    private readonly MainWindowViewModel viewModel;
    private GameWindowSnapshot? currentGame;
    private HwndSource? windowSource;
    private nint windowHandle;
    private int relativeLeft;
    private int relativeTop;
    private bool hasCustomPlacement;
    private bool hasCompletedInitialPlacement;
    private bool isPositioning;
    private bool isClosed;
    private bool allowClose;
    private bool isCollapsed;

    internal FloatingOverlayWindow(
        MainWindowViewModel viewModel,
        GameWindowTracker tracker)
    {
        InitializeComponent();
        DataContext = viewModel;
        this.viewModel = viewModel;
        SelectGroup(viewModel.FeatureGroups[0]);
        this.tracker = tracker;
        tracker.Updated += HandleGameWindowUpdated;
    }

    internal void RequestClose()
    {
        if (!isClosed)
        {
            allowClose = true;
            Close();
        }
    }

    protected override void OnSourceInitialized(EventArgs eventArgs)
    {
        base.OnSourceInitialized(eventArgs);
        windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(windowHandle, ExtendedWindowStyleIndex);
        _ = SetWindowLong(windowHandle, ExtendedWindowStyleIndex,
            extendedStyle | ExtendedStyleNoActivate | ExtendedStyleToolWindow);
        windowSource = HwndSource.FromHwnd(windowHandle);
        windowSource?.AddHook(HandleWindowMessage);
    }

    protected override void OnLocationChanged(EventArgs eventArgs)
    {
        base.OnLocationChanged(eventArgs);
        if (!hasCompletedInitialPlacement || isPositioning || windowHandle == 0 ||
            currentGame is not { } game ||
            !GetWindowRect(windowHandle, out var rectangle))
            return;

        relativeLeft = rectangle.Left - game.Bounds.Left;
        relativeTop = rectangle.Top - game.Bounds.Top;
        hasCustomPlacement = true;
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        isClosed = true;
        tracker.Updated -= HandleGameWindowUpdated;
        windowSource?.RemoveHook(HandleWindowMessage);
        windowSource = null;
        windowHandle = 0;
        base.OnClosed(eventArgs);
    }

    protected override void OnClosing(CancelEventArgs eventArgs)
    {
        if (!allowClose)
        {
            eventArgs.Cancel = true;
            viewModel.ShowStatus("正在安全恢复游戏状态并退出…");
            viewModel.ExitCommand.Execute(null);
        }
        base.OnClosing(eventArgs);
    }

    private void HandleGameWindowUpdated(GameWindowSnapshot? snapshot)
    {
        currentGame = snapshot;
        if (snapshot is not { } game || !viewModel.IsConnected || !game.IsForeground)
        {
            if (IsVisible)
                Hide();
            return;
        }

        if (!IsVisible)
            Show();
        PositionWithin(game);
    }

    private static nint HandleWindowMessage(
        nint window, int message, nint wordParameter, nint longParameter, ref bool handled)
    {
        if (message != MouseActivateMessage)
            return 0;
        handled = true;
        return MouseActivateWithoutActivation;
    }

    private void SelectGroup_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is System.Windows.Controls.Button
            {
                DataContext: FeatureGroupViewModel group
            })
        {
            SelectGroup(group);
        }
    }

    private void DragWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void SelectGroup(FeatureGroupViewModel group)
    {
        CategoryTitle.Text = group.Title;
        FeatureContent.Content = group;
    }

    private void ToggleCollapsed_Click(object sender, RoutedEventArgs eventArgs)
    {
        isCollapsed = !isCollapsed;
        FeatureContent.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseButton.Content = isCollapsed ? "展开" : "收起";
        if (currentGame is { } game)
            PositionWithin(game);
    }

    private void PositionWithin(GameWindowSnapshot game)
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
            return;

        windowHandle = windowHandle != 0
            ? windowHandle
            : new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (windowHandle == 0)
            return;

        var scale = game.Dpi / 96d;
        var desiredMargin = Math.Max(1, (int)Math.Round(EdgeMargin * scale));
        var horizontalMargin = Math.Min(
            desiredMargin, Math.Max(0, (game.Bounds.Width - 1) / 2));
        var verticalMargin = Math.Min(
            desiredMargin, Math.Max(0, (game.Bounds.Height - 1) / 2));
        var availableWidth = Math.Max(1,
            game.Bounds.Width - (2 * horizontalMargin));
        var availableHeight = Math.Max(1,
            game.Bounds.Height - (2 * verticalMargin));
        var width = Math.Min(
            Math.Max(1, (int)Math.Round(OverlayWidth * scale)),
            availableWidth);
        var desiredHeight = isCollapsed ? CollapsedOverlayHeight : OverlayHeight;
        var height = Math.Min(
            Math.Max(1, (int)Math.Round(desiredHeight * scale)),
            availableHeight);

        var left = hasCustomPlacement
            ? game.Bounds.Left + relativeLeft
            : game.Bounds.Right - horizontalMargin - width;
        var top = hasCustomPlacement
            ? game.Bounds.Top + relativeTop
            : game.Bounds.Top + verticalMargin;
        left = Math.Clamp(left, game.Bounds.Left + horizontalMargin,
            game.Bounds.Right - horizontalMargin - width);
        top = Math.Clamp(top, game.Bounds.Top + verticalMargin,
            game.Bounds.Bottom - verticalMargin - height);

        isPositioning = true;
        try
        {
            Width = width / scale;
            Height = height / scale;
            _ = SetWindowPos(
                windowHandle,
                HwndTopmost,
                left,
                top,
                width,
                height,
                SetWindowPositionFlags.NoActivate | SetWindowPositionFlags.ShowWindow);
        }
        finally
        {
            isPositioning = false;
            hasCompletedInitialPlacement = true;
        }
    }

    [Flags]
    private enum SetWindowPositionFlags : uint
    {
        NoSize = 0x0001,
        NoMove = 0x0002,
        NoZOrder = 0x0004,
        NoActivate = 0x0010,
        ShowWindow = 0x0040
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        SetWindowPositionFlags flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(nint window, int index, int value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRectangle rectangle);

}
