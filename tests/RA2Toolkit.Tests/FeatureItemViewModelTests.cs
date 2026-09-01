using Xunit;

public sealed class FeatureItemViewModelTests
{
    [Fact]
    public void Feature_is_unavailable_until_game_is_connected()
    {
        var feature = CreateViewModel(OverlayCommand.ToggleRevealMap);

        feature.ApplyState(OverlayState.Empty);

        Assert.False(feature.IsAvailable);
        Assert.False(feature.PrimaryCommand.CanExecute(null));

        feature.ApplyState(new OverlayState { Connected = true });

        Assert.True(feature.IsAvailable);
        Assert.True(feature.PrimaryCommand.CanExecute(null));
    }

    [Fact]
    public void Multiplayer_disables_only_restricted_features()
    {
        var restricted = CreateViewModel(OverlayCommand.ToggleGodMode);
        var unrestricted = CreateViewModel(OverlayCommand.ToggleCrateRouteLines);
        var multiplayerState = new OverlayState
        {
            Connected = true,
            RestrictedFeaturesAvailable = false
        };

        restricted.ApplyState(multiplayerState);
        unrestricted.ApplyState(multiplayerState);

        Assert.False(restricted.IsAvailable);
        Assert.False(restricted.PrimaryCommand.CanExecute(null));
        Assert.True(unrestricted.IsAvailable);
        Assert.True(unrestricted.PrimaryCommand.CanExecute(null));
    }

    [Fact]
    public void Number_control_dispatches_rules_command_with_number_payload()
    {
        OverlayCommandRequest? dispatched = null;
        var feature = CreateViewModel(
            OverlayCommand.SetLightningStormDamage,
            request => dispatched = request);
        feature.ApplyState(new OverlayState { Connected = true });
        feature.NumberText = "345";

        feature.PrimaryCommand.Execute(null);

        Assert.NotNull(dispatched);
        Assert.Equal(OverlayCommand.SetLightningStormDamage, dispatched.Command);
        Assert.Equal(345m, dispatched.Number);
        Assert.Null(dispatched.Option);
    }

    [Fact]
    public void Choice_control_dispatches_rules_command_with_option_payload()
    {
        OverlayCommandRequest? dispatched = null;
        var feature = CreateViewModel(
            OverlayCommand.SetParadropInfantryType,
            request => dispatched = request);
        feature.ApplyState(new OverlayState { Connected = true });
        feature.OptionText = "INIT";

        feature.PrimaryCommand.Execute(null);

        Assert.NotNull(dispatched);
        Assert.Equal(OverlayCommand.SetParadropInfantryType, dispatched.Command);
        Assert.Null(dispatched.Number);
        Assert.Equal("INIT", dispatched.Option);
    }

    [Fact]
    public void Number_control_can_be_changed_without_keyboard_focus()
    {
        var feature = CreateViewModel(OverlayCommand.SetGameSpeed);
        feature.ApplyState(new OverlayState { Connected = true });
        feature.NumberText = "3";

        feature.IncreaseNumberCommand.Execute(null);
        Assert.Equal("4", feature.NumberText);

        feature.DecreaseNumberCommand.Execute(null);
        Assert.Equal("3", feature.NumberText);
    }

    [Fact]
    public void Choice_control_can_be_changed_without_opening_a_popup()
    {
        var feature = CreateViewModel(OverlayCommand.SetParadropInfantryType);
        feature.ApplyState(new OverlayState { Connected = true });
        var original = feature.SelectedChoice;

        feature.NextChoiceCommand.Execute(null);

        Assert.NotNull(original);
        Assert.NotEqual(original, feature.SelectedChoice);
        feature.PreviousChoiceCommand.Execute(null);
        Assert.Equal(original, feature.SelectedChoice);
    }

    private static FeatureItemViewModel CreateViewModel(
        OverlayCommand command,
        Action<OverlayCommandRequest>? dispatch = null)
    {
        var definition = FeatureCatalog.All.Single(feature =>
            feature.PrimaryCommand == command);
        var hotkeyPath = Path.Combine(Path.GetTempPath(),
            $"ra2-toolkit-tests-{Guid.NewGuid():N}.json");
        return new FeatureItemViewModel(
            definition,
            dispatch ?? (_ => { }),
            _ => { },
            new HotkeyStore(hotkeyPath));
    }
}
