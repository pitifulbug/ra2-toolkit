internal enum FeatureCategory
{
    Resources,
    Construction,
    AutoConstruction,
    Combat,
    MapAndCrates,
    Game,
    Objects
}

internal sealed record FeatureDefinition(
    FeatureCategory Category,
    string Title,
    string Description,
    OverlayCommand? ToggleCommand,
    OverlayCommand HotkeyBindingCommand,
    OverlayCommand HotkeyPressCommand,
    OverlayCommand? HotkeyDoublePressCommand,
    bool RestrictedInMultiplayer,
    Func<OverlayState, bool>? StateSelector)
{
    internal bool IsToggle => ToggleCommand is not null;
}
