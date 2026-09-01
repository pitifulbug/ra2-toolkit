using Xunit;

public sealed class ParadropDefinitionTests
{
    [Theory]
    [InlineData(0, "Americans", "美国空降兵类型")]
    [InlineData(0, "French", "盟军空降兵类型")]
    [InlineData(1, "Russians", "苏联空降兵类型")]
    [InlineData(2, "YuriCountry", "尤里空降兵类型")]
    public void Current_country_routes_to_its_paradrop_rules(
        int sideIndex, string countryId, string expectedName)
    {
        var definition = CratePicker.GetParadropDefinition(
            OverlayCommand.SetParadropInfantryType, sideIndex, countryId);
        var countDefinition = CratePicker.GetParadropDefinition(
            OverlayCommand.SetParadropCount, sideIndex, countryId);

        Assert.True(definition.IsInfantryType);
        Assert.Equal(expectedName, definition.DisplayName);
        Assert.False(countDefinition.IsInfantryType);
        Assert.Equal(expectedName.Replace("类型", "数量"), countDefinition.DisplayName);
        Assert.NotEqual(definition.Offset, countDefinition.Offset);
    }
}
