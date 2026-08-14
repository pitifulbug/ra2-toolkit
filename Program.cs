Console.SetOut(TextWriter.Null);

using var singleInstanceMutex = new Mutex(
    true, @"Local\PitifulBug.RA2Toolkit.SingleInstance", out var isFirstInstance);
if (!isFirstInstance)
{
    System.Windows.Forms.MessageBox.Show(
        "RA2 Toolkit 已经在运行，请切换到现有窗口。",
        "RA2 Toolkit",
        System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Information);
    return;
}

try
{
    using var picker = new CratePicker();
    picker.Run();
}
catch (GameProcessExitedException)
{
    // The game closed while the controller was reading its final frame.
}
catch (Exception error)
{
    System.Windows.Forms.MessageBox.Show(
        $"启动失败：{error.Message}",
        "RA2 Toolkit",
        System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Error);
}
