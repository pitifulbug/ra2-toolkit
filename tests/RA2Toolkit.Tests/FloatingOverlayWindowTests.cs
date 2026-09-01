using Xunit;

public sealed class FloatingOverlayWindowTests
{
    [Fact]
    public void Startup_uses_only_the_game_overlay()
    {
        var source = ReadRepositoryFile(
            "src", "RA2Toolkit", "App", "ToolkitApplication.cs");

        Assert.Contains("overlayWindow = new FloatingOverlayWindow", source, StringComparison.Ordinal);
        Assert.Contains("MainWindow = overlayWindow;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new MainWindow(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("window.Show();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Tray_menu_can_exit_while_the_game_overlay_is_hidden()
    {
        var source = ReadRepositoryFile(
            "src", "RA2Toolkit", "App", "ToolkitApplication.cs");
        var project = ReadRepositoryFile(
            "src", "RA2Toolkit", "RA2Toolkit.csproj");

        Assert.Contains("<UseWindowsForms>true</UseWindowsForms>", project,
            StringComparison.Ordinal);
        Assert.Contains("new System.Windows.Forms.NotifyIcon", source,
            StringComparison.Ordinal);
        Assert.Contains("退出 RA2 Toolkit", source, StringComparison.Ordinal);
        Assert.Contains("viewModel.ExitCommand.Execute(null)", source,
            StringComparison.Ordinal);
        Assert.Contains("trayIcon.Visible = false", source, StringComparison.Ordinal);
        Assert.Contains("trayIcon.Dispose()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_uses_native_controls_without_activating_the_game_window()
    {
        var xaml = ReadRepositoryFile(
            "src", "RA2Toolkit", "UI", "Views", "FloatingOverlayWindow.xaml");
        var source = ReadRepositoryFile(
            "src", "RA2Toolkit", "UI", "Views", "FloatingOverlayWindow.xaml.cs");

        Assert.Contains("WindowStyle=\"None\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ResizeMode=\"NoResize\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseLeftButtonDown=\"DragWindow_MouseLeftButtonDown\"", xaml,
            StringComparison.Ordinal);
        Assert.Contains("Click=\"ToggleCollapsed_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AllowsTransparency=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowActivated=\"False\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ControlTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("ToggleFeatures", xaml, StringComparison.Ordinal);
        Assert.Contains("ActionFeatures", xaml, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(xaml, "Content=\"{Binding HotkeyText}\""));
        Assert.Equal(2, CountOccurrences(xaml,
            "Command=\"{Binding CaptureHotkeyCommand}\""));
        Assert.Contains("NumberFeatures", xaml, StringComparison.Ordinal);
        Assert.Contains("ChoiceFeatures", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"SelectGroup_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DecreaseNumberCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("IncreaseNumberCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("<ComboBox", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedChoice, Mode=TwoWay}\"", xaml,
            StringComparison.Ordinal);
        Assert.Contains("DisplayMemberPath=\"Label\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ExitCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBox", xaml, StringComparison.Ordinal);
        Assert.Contains("ExtendedStyleNoActivate", source, StringComparison.Ordinal);
        Assert.Contains("MouseActivateMessage", source, StringComparison.Ordinal);
        Assert.Contains("MouseActivateWithoutActivation", source, StringComparison.Ordinal);
        Assert.Contains("SetWindowPositionFlags.NoActivate", source, StringComparison.Ordinal);
        Assert.Contains("DragMove();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_is_visible_only_while_the_game_owns_the_foreground()
    {
        var source = ReadRepositoryFile(
            "src", "RA2Toolkit", "UI", "Views", "GameWindowTracker.cs");

        Assert.Contains("foregroundProcessId == (uint)process.Id", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.ProcessId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_restores_update_controls_without_the_bottom_status_text()
    {
        var xaml = ReadRepositoryFile(
            "src", "RA2Toolkit", "UI", "Views", "FloatingOverlayWindow.xaml");
        var source = ReadRepositoryFile(
            "src", "RA2Toolkit", "UI", "Views", "FloatingOverlayWindow.xaml.cs");
        var application = ReadRepositoryFile(
            "src", "RA2Toolkit", "App", "ToolkitApplication.cs");

        Assert.Contains("Content=\"{Binding VersionText}\"", xaml,
            StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CheckUpdatesCommand}\"", xaml,
            StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding UpdateText}\"", xaml,
            StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenReleaseCommand}\"", xaml,
            StringComparison.Ordinal);
        Assert.Contains("viewModel.CheckUpdatesNow();", application,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StatusBlock", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding StatusText}\"", xaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StatusBlock.Visibility", source,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root.FullName, .. pathParts]));
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static DirectoryInfo FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[]
                 {
                     new DirectoryInfo(Path.GetDirectoryName(sourceFile)!),
                     new DirectoryInfo(Directory.GetCurrentDirectory()),
                     new DirectoryInfo(AppContext.BaseDirectory)
                 })
        {
            for (var directory = start;
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ra2-toolkit.slnx")))
                    return directory;
            }
        }

        throw new DirectoryNotFoundException("无法从测试输出目录定位仓库根目录。");
    }
}
