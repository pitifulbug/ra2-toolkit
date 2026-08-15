public sealed class BugReportIconTests
{
    [Fact]
    public void Create_ReturnsExpectedIconSize()
    {
        using var icon = BugReportIcon.Create();

        Assert.Equal(16, icon.Width);
        Assert.Equal(16, icon.Height);
    }
}
