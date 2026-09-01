internal enum FeatureCategory
{
    Player,
    Units,
    Miscellaneous,
    Enemy,
    Fun,
    Values
}

internal enum FeatureControlKind
{
    Toggle,
    Action,
    Number,
    Choice
}

internal sealed record FeatureChoice(string Value, string Label)
{
    public string DisplayText => $"{Label}（{Value}）";
}

internal sealed record FeatureDefinition(
    FeatureCategory Category,
    string Title,
    string Description,
    FeatureControlKind ControlKind,
    OverlayCommand PrimaryCommand,
    OverlayCommand? ToggleCommand,
    OverlayCommand? HotkeyBindingCommand,
    OverlayCommand? HotkeyPressCommand,
    OverlayCommand? HotkeyDoublePressCommand,
    bool RestrictedInMultiplayer,
    Func<OverlayState, bool>? StateSelector,
    decimal? DefaultNumber = null,
    decimal? MinimumNumber = null,
    decimal? MaximumNumber = null,
    decimal? NumberStep = null,
    int NumberDecimalPlaces = 0,
    IReadOnlyList<FeatureChoice>? Choices = null,
    string? DefaultOption = null)
{
    internal bool IsToggle => ControlKind == FeatureControlKind.Toggle;
    internal bool IsAction => ControlKind == FeatureControlKind.Action;
    internal bool IsNumber => ControlKind == FeatureControlKind.Number;
    internal bool IsChoice => ControlKind == FeatureControlKind.Choice;
}
