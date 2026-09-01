internal enum OverlayCommand
{
    SetMoneyAmount,
    SetBuildQueueLimit,
    ToggleRevealMap,
    ToggleInfiniteMoney,
    ToggleOneHitKill,
    ToggleHighDefense,
    ToggleSelectedCratePickers,
    ToggleCrateRouteLines,
    ToggleMaximumPower,
    ToggleFullTech,
    ToggleUnlimitedProduction,
    ToggleChronoLegionnaireNoCooldown,
    ToggleEliteUnits,
    ArrangeSelectedFormation,
    ToggleSelectedInfiniteRange,
    ToggleSelectedInfiniteSpeed,
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
    ToggleGodMode,
    ToggleSellEverything,
    ToggleSpySatelliteAndRadar,
    ToggleDetectDisguises,
    ToggleMindControlImmunity,
    ToggleAutoUnitRepair,
    PromoteSelectedUnits,
    ToggleRapidAttack,
    ToggleFastReload,
    ToggleLargeAmmo,
    ToggleExtendedGuardRange,
    ToggleFastInfantry,
    SetGameSpeed,
    WinImmediately,
    ToggleSpyVsSpy,
    GrantNuclearMissile,
    ToggleDisableShields,
    ToggleFreezeEnemies,
    ToggleEnemyRepairsCauseDamage,
    ToggleCounterYuriControl,
    ToggleCaptureAttackers,
    TogglePreventEnemyCaptures,
    TogglePreventEnemyGarrison,
    SetParadropInfantryType,
    SetParadropCount,
    SetNuclearMissileDamage,
    SetEliteUnitAttackMultiplier,
    SetEliteUnitSpeedMultiplier,
    SetEliteUnitArmorMultiplier,
    SetEliteUnitRateOfFireMultiplier,
    SetPsychicDominatorDamage,
    SetPrismSupportModifier,
    SetPrismSupportDelay,
    SetPrismSupportDuration,
    SetPrismSupportHeight,
    SetV3RocketDamage,
    SetEliteV3RocketDamage,
    SetDreadnoughtMissileDamage,
    SetEliteDreadnoughtMissileDamage,
    SetBoomerMissileDamage,
    SetEliteBoomerMissileDamage,
    SetOrePurifierBonus,
    SetLightningStormDamage,
    ExitProgram
}

internal sealed record OverlayCommandRequest(
    OverlayCommand Command,
    decimal? Number = null,
    string? Option = null)
{
    internal static OverlayCommandRequest For(OverlayCommand command) => new(command);
}

internal sealed record OverlayState
{
    internal static OverlayState Empty { get; } = new();

    public bool Connected { get; init; }

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
    public bool GodMode { get; init; }
    public bool SellEverything { get; init; }
    public bool SpySatelliteAndRadar { get; init; }
    public bool DetectDisguises { get; init; }
    public bool MindControlImmunity { get; init; }
    public bool AutoUnitRepair { get; init; }
    public bool RapidAttack { get; init; }
    public bool FastReload { get; init; }
    public bool LargeAmmo { get; init; }
    public bool ExtendedGuardRange { get; init; }
    public bool FastInfantry { get; init; }
    public bool SpyVsSpy { get; init; }
    public bool DisableShields { get; init; }
    public bool FreezeEnemies { get; init; }
    public bool EnemyRepairsCauseDamage { get; init; }
    public bool CounterYuriControl { get; init; }
    public bool CaptureAttackers { get; init; }
    public bool PreventEnemyCaptures { get; init; }
    public bool PreventEnemyGarrison { get; init; }
    public bool RestrictedFeaturesAvailable { get; init; } = true;
}
