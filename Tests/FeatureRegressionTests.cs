public sealed class FeatureRegressionTests
{
    [Fact]
    public void MaximumPower_RequestsGamePowerStateRecalculation()
    {
        Assert.Equal(1, CratePicker.MaximumPowerRecheckFlag);
    }

    [Fact]
    public void InfiniteSpeed_MultipliesTheExistingSpeedByTheGameCrateBonus()
    {
        Assert.Equal(2.0, CratePicker.CalculateInfiniteSpeedMultiplier(1.0, 2.0));
        Assert.Equal(3.0, CratePicker.CalculateInfiniteSpeedMultiplier(1.5, 2.0));
        Assert.Equal(1.0, CratePicker.CalculateInfiniteSpeedMultiplier(1.0, 1.0));
    }

    [Theory]
    [InlineData(0.0, 2.0, 0.0)]
    [InlineData(1.0, 0.0, 1.0)]
    [InlineData(10.0, 2.0, 10.0)]
    [InlineData(double.NaN, 2.0, double.NaN)]
    public void InfiniteSpeed_LeavesInvalidOrOverflowingMultipliersUnchanged(
        double originalMultiplier, double speedBoost, double expected)
    {
        var actual = CratePicker.CalculateInfiniteSpeedMultiplier(originalMultiplier, speedBoost);

        if (double.IsNaN(expected))
            Assert.True(double.IsNaN(actual));
        else
            Assert.Equal(expected, actual);
    }

    [Fact]
    public void RevealMap_DisablesThroughHouseReshroudRoutine()
    {
        Assert.Equal(0x577D90L, CratePicker.RevealMapRoutineAddress);
        Assert.Equal(0x50BD10L, CratePicker.ReshroudMapRoutineAddress);
    }

    [Theory]
    [InlineData(1, int.MaxValue)]
    [InlineData(5, int.MaxValue)]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    public void UnlimitedProduction_PersistsPositiveBuildLimitsAsUnlimited(
        int originalBuildLimit, int expected)
    {
        Assert.Equal(expected, CratePicker.GetUnlimitedBuildLimit(originalBuildLimit));
    }

}
