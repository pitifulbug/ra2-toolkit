using System.Diagnostics;
using System.Reflection;

internal sealed partial class BugReportDialog : Form
{
    private const string NewIssueUrl =
        "https://github.com/pitifulbug/ra2-toolkit/issues/new";

    internal BugReportDialog()
    {
        InitializeComponent();
        Icon = BugReportIcon.Create();
    }

    private void ReportTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (reportTypeComboBox.SelectedItem is not string reportType)
            return;

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "未知";
        var title = Uri.EscapeDataString($"[{reportType}] RA2 Toolkit {version}");
        var body = Uri.EscapeDataString(
            $"请在此记录事件经过、复现方式或改进构想。{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}软件版本：{version}");
        var url = $"{NewIssueUrl}?title={title}&body={body}";

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Close();
        }
        catch (Exception error)
        {
            MessageBox.Show(this, $"未能打开 GitHub 情报页面：{error.Message}",
                "RA2 Toolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
