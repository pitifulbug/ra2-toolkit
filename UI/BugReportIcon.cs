internal static class BugReportIcon
{
    internal static Icon Create()
    {
        using var stream = typeof(BugReportIcon).Assembly
            .GetManifestResourceStream("BugReport.ico")
            ?? throw new InvalidOperationException("缺少报告问题图标资源。");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
