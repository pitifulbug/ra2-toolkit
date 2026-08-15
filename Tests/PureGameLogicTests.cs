public sealed class PureGameLogicTests
{
    [Fact]
    public void DistanceSquared_UsesLongArithmetic()
    {
        Assert.Equal(25L, CratePicker.DistanceSquared((1, 2), (4, 6)));
        Assert.Equal(20_000_000_000L,
            CratePicker.DistanceSquared((50_000, 50_000), (-50_000, -50_000)));
    }

    [Fact]
    public void FormatUnitIds_SummarizesOnlyAfterTenIds()
    {
        Assert.Equal("", CratePicker.FormatUnitIds(Array.Empty<int>()));
        Assert.Equal("1, 2, 3", CratePicker.FormatUnitIds(new[] { 1, 2, 3 }));
        Assert.Equal("1, 2, 3, 4, 5, 6, 7, 8, 9, 10…",
            CratePicker.FormatUnitIds(Enumerable.Range(1, 11)));
    }

    [Theory]
    [InlineData(0.05, true)]
    [InlineData(10.0, true)]
    [InlineData(0.049, false)]
    [InlineData(10.001, false)]
    public void IsReasonableSpeedMultiplier_EnforcesFiniteSupportedRange(
        double value, bool expected)
    {
        Assert.Equal(expected, CratePicker.IsReasonableSpeedMultiplier(value));
    }

    [Fact]
    public void IsReasonableSpeedMultiplier_RejectsNonFiniteValues()
    {
        Assert.False(CratePicker.IsReasonableSpeedMultiplier(double.NaN));
        Assert.False(CratePicker.IsReasonableSpeedMultiplier(double.PositiveInfinity));
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(1000.0, true)]
    [InlineData(-0.001, false)]
    [InlineData(1000.001, false)]
    public void IsReasonableFirepowerMultiplier_EnforcesFiniteSupportedRange(
        double value, bool expected)
    {
        Assert.Equal(expected, CratePicker.IsReasonableFirepowerMultiplier(value));
    }

    [Fact]
    public void IsReasonableFirepowerMultiplier_RejectsNonFiniteValues()
    {
        Assert.False(CratePicker.IsReasonableFirepowerMultiplier(double.NaN));
        Assert.False(CratePicker.IsReasonableFirepowerMultiplier(double.NegativeInfinity));
    }

    [Theory]
    [InlineData(1, 0, 0x6000)]
    [InlineData(0, -1, 0x2000)]
    [InlineData(0, 1, 0xA000)]
    [InlineData(-1, 0, 0xE000)]
    public void GetFormationFacing_MapsDirectionToGameFacing(
        int x, int y, ushort expected)
    {
        Assert.Equal(expected, CratePicker.GetFormationFacing((x, y)));
    }
}
