using System.Windows;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        using var singleInstanceMutex = new Mutex(
            true, @"Local\PitifulBug.RA2Toolkit.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show(
                "RA2 Toolkit 已经在运行，请切换到现有窗口。",
                "RA2 Toolkit",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return 0;
        }

        var application = new ToolkitApplication();
        return application.Run();
    }
}
