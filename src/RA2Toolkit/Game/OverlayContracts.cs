internal enum OverlayCommand
{
    ToggleRevealMap,
    ToggleInfiniteMoney,
    ToggleOneHitKill,
    ToggleHighDefense,
    ToggleCratePicker,
    EnableSelectedCratePickers,
    DisableSelectedCratePickers,
    ToggleCrateRouteLines,
    ToggleMaximumPower,
    ToggleFullTech,
    ToggleUnlimitedProduction,
    ToggleChronoLegionnaireNoCooldown,
    ToggleEliteUnits,
    ToggleFormationMode,
    ArrangeSelectedFormation,
    ToggleInfiniteRangeMode,
    ToggleSelectedInfiniteRange,
    ToggleInfiniteSpeedMode,
    ToggleSelectedInfiniteSpeed,
    ToggleSpinningMcvMode,
    ToggleSelectedSpinningMcvs,
    ToggleInstantBuild,
    ToggleBuildAnywhere,
    ToggleAutoRepair,
    ToggleSuperWeaponNoCooldown,
    ToggleParatrooperNoCooldown,
    AutoBuildFlakCannon,
    AutoBuildPatriotMissile,
    AutoBuildPrismTower,
    AutoBuildTeslaCoil,
    DeleteSelectedObjects,
    TakeOwnershipSelectedObjects,
    ToggleFastTurn,
    ToggleDisableGapGenerators,
    ToggleInvadeMode,
    ToggleGamePause,
    ExitProgram
}

internal sealed record OverlayState
{
    internal static OverlayState Empty { get; } = new();

    internal OverlayState()
    {
    }

    public bool RevealMap { get; init; }
    public bool InfiniteMoney { get; init; }
    public bool OneHitKill { get; init; }
    public bool HighDefense { get; init; }
    public bool EliteUnits { get; init; }
    public bool FormationMode { get; init; }
    public bool InfiniteRangeMode { get; init; }
    public bool InfiniteSpeedMode { get; init; }
    public bool SpinningMcvMode { get; init; }
    public bool CratePicker { get; init; }
    public bool CrateRouteLines { get; init; }
    public bool MaximumPower { get; init; }
    public bool FullTech { get; init; }
    public bool UnlimitedProduction { get; init; }
    public bool ChronoLegionnaireNoCooldown { get; init; }
    public bool InstantBuild { get; init; }
    public bool BuildAnywhere { get; init; }
    public bool AutoRepair { get; init; }
    public bool SuperWeaponNoCooldown { get; init; }
    public bool ParatrooperNoCooldown { get; init; }
    public bool FastTurn { get; init; }
    public bool DisableGapGenerators { get; init; }
    public bool InvadeMode { get; init; }
    public bool GamePaused { get; init; }
    public bool RestrictedFeaturesAvailable { get; init; } = true;
}
