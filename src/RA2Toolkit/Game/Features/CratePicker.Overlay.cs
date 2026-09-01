using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    internal event Action<OverlayState>? StateChanged;
    internal event Action<string, bool>? OperationStatusChanged;

    internal bool ExitRequested => exitRequested;

    internal void EnqueueCommand(OverlayCommandRequest request)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposeState) != 0, this);
        overlayCommands.Enqueue(request);
    }

    private static Func<OverlayCommandRequest, int?> Command(Action action) => _ =>
    {
        action();
        return null;
    };

    private IReadOnlyDictionary<OverlayCommand, Func<OverlayCommandRequest, int?>> CreateCommandHandlers() =>
        new Dictionary<OverlayCommand, Func<OverlayCommandRequest, int?>>
        {
            [OverlayCommand.SetMoneyAmount] = SetMoneyAmount,
            [OverlayCommand.SetBuildQueueLimit] = SetBuildQueueLimit,
            [OverlayCommand.ToggleRevealMap] = Command(ToggleRevealMap),
            [OverlayCommand.ToggleInfiniteMoney] = Command(ToggleInfiniteMoney),
            [OverlayCommand.ToggleOneHitKill] = Command(ToggleOneHitKill),
            [OverlayCommand.ToggleHighDefense] = Command(ToggleHighDefense),
            [OverlayCommand.ToggleSelectedCratePickers] = _ => ToggleSelectedCratePickers(),
            [OverlayCommand.ToggleCrateRouteLines] = Command(ToggleCrateRouteLines),
            [OverlayCommand.ToggleMaximumPower] = Command(ToggleMaximumPower),
            [OverlayCommand.ToggleFullTech] = Command(ToggleFullTech),
            [OverlayCommand.ToggleUnlimitedProduction] = Command(ToggleUnlimitedProduction),
            [OverlayCommand.ToggleChronoLegionnaireNoCooldown] = Command(ToggleChronoLegionnaireNoCooldown),
            [OverlayCommand.ToggleEliteUnits] = Command(ToggleEliteUnits),
            [OverlayCommand.ArrangeSelectedFormation] = _ => ArrangeSelectedFormationFromUi(),
            [OverlayCommand.ToggleSelectedInfiniteRange] = _ => ToggleSelectedInfiniteRange(),
            [OverlayCommand.ToggleSelectedInfiniteSpeed] = _ => ToggleSelectedInfiniteSpeed(),
            [OverlayCommand.ToggleSelectedSpinningMcvs] = _ => ToggleSelectedSpinningMcvs(),
            [OverlayCommand.ToggleInstantBuild] = Command(ToggleInstantBuild),
            [OverlayCommand.ToggleBuildAnywhere] = Command(ToggleBuildAnywhere),
            [OverlayCommand.ToggleAutoRepair] = Command(ToggleAutoRepair),
            [OverlayCommand.ToggleSuperWeaponNoCooldown] = Command(ToggleSuperWeaponNoCooldown),
            [OverlayCommand.ToggleParatrooperNoCooldown] = Command(ToggleParatrooperNoCooldown),
            [OverlayCommand.AutoBuildFlakCannon] = Command(() => ToggleAutoBuild("NAFLAK", "防空炮")),
            [OverlayCommand.AutoBuildPatriotMissile] = Command(() => ToggleAutoBuild("NASAM", "爱国者导弹")),
            [OverlayCommand.AutoBuildPrismTower] = Command(() => ToggleAutoBuild("ATESLA", "光棱塔")),
            [OverlayCommand.AutoBuildTeslaCoil] = Command(() => ToggleAutoBuild("TESLA", "磁暴线圈")),
            [OverlayCommand.DeleteSelectedObjects] = _ => DeleteSelectedObjects(),
            [OverlayCommand.TakeOwnershipSelectedObjects] = _ => TakeOwnershipOfSelectedObjects(),
            [OverlayCommand.ToggleFastTurn] = Command(ToggleFastTurn),
            [OverlayCommand.ToggleDisableGapGenerators] = Command(ToggleDisabledGapGenerators),
            [OverlayCommand.ToggleInvadeMode] = Command(ToggleInvadeMode),
            [OverlayCommand.ToggleGamePause] = Command(ToggleGamePause),
            [OverlayCommand.ToggleGodMode] = Command(ToggleGodMode),
            [OverlayCommand.ToggleSellEverything] = Command(ToggleSellEverything),
            [OverlayCommand.ToggleSpySatelliteAndRadar] = Command(ToggleSpySatelliteAndRadar),
            [OverlayCommand.ToggleDetectDisguises] = Command(ToggleDetectDisguises),
            [OverlayCommand.ToggleMindControlImmunity] = Command(ToggleMindControlImmunity),
            [OverlayCommand.ToggleAutoUnitRepair] = Command(ToggleAutoUnitRepair),
            [OverlayCommand.PromoteSelectedUnits] = _ => PromoteSelectedUnits(),
            [OverlayCommand.ToggleRapidAttack] = Command(ToggleRapidAttack),
            [OverlayCommand.ToggleFastReload] = Command(ToggleFastReload),
            [OverlayCommand.ToggleLargeAmmo] = Command(ToggleLargeAmmo),
            [OverlayCommand.ToggleExtendedGuardRange] = Command(ToggleExtendedGuardRange),
            [OverlayCommand.ToggleFastInfantry] = Command(ToggleFastInfantry),
            [OverlayCommand.SetGameSpeed] = SetGameSpeed,
            [OverlayCommand.WinImmediately] = _ => WinImmediately(),
            [OverlayCommand.ToggleSpyVsSpy] = Command(ToggleSpyVsSpy),
            [OverlayCommand.GrantNuclearMissile] = _ => GrantNuclearMissile(),
            [OverlayCommand.ToggleDisableShields] = Command(ToggleDisableShields),
            [OverlayCommand.ToggleFreezeEnemies] = Command(ToggleFreezeEnemies),
            [OverlayCommand.ToggleEnemyRepairsCauseDamage] = Command(ToggleEnemyRepairsCauseDamage),
            [OverlayCommand.ToggleCounterYuriControl] = Command(ToggleCounterYuriControl),
            [OverlayCommand.ToggleCaptureAttackers] = Command(ToggleCaptureAttackers),
            [OverlayCommand.TogglePreventEnemyCaptures] = Command(TogglePreventEnemyCaptures),
            [OverlayCommand.TogglePreventEnemyGarrison] = Command(TogglePreventEnemyGarrison),
            [OverlayCommand.SetParadropInfantryType] = SetParadropConfiguration,
            [OverlayCommand.SetParadropCount] = SetParadropConfiguration,
            [OverlayCommand.SetNuclearMissileDamage] = SetRulesValue,
            [OverlayCommand.SetEliteUnitAttackMultiplier] = SetRulesValue,
            [OverlayCommand.SetEliteUnitSpeedMultiplier] = SetRulesValue,
            [OverlayCommand.SetEliteUnitArmorMultiplier] = SetRulesValue,
            [OverlayCommand.SetEliteUnitRateOfFireMultiplier] = SetRulesValue,
            [OverlayCommand.SetPsychicDominatorDamage] = SetRulesValue,
            [OverlayCommand.SetPrismSupportModifier] = SetRulesValue,
            [OverlayCommand.SetPrismSupportDelay] = SetRulesValue,
            [OverlayCommand.SetPrismSupportDuration] = SetRulesValue,
            [OverlayCommand.SetPrismSupportHeight] = SetRulesValue,
            [OverlayCommand.SetV3RocketDamage] = SetRulesValue,
            [OverlayCommand.SetEliteV3RocketDamage] = SetRulesValue,
            [OverlayCommand.SetDreadnoughtMissileDamage] = SetRulesValue,
            [OverlayCommand.SetEliteDreadnoughtMissileDamage] = SetRulesValue,
            [OverlayCommand.SetBoomerMissileDamage] = SetRulesValue,
            [OverlayCommand.SetEliteBoomerMissileDamage] = SetRulesValue,
            [OverlayCommand.SetOrePurifierBonus] = SetRulesValue,
            [OverlayCommand.SetLightningStormDamage] = SetRulesValue,
            [OverlayCommand.ExitProgram] = Command(RequestExit)
        };

    private void ToggleChronoLegionnaireNoCooldown()
    {
        chronoLegionnaireNoCooldownEnabled = !chronoLegionnaireNoCooldownEnabled;
        nextChronoLegionnaireRefreshAt = DateTime.MinValue;
    }

    private int ArrangeSelectedFormationFromUi()
    {
        formationModeEnabled = true;
        return ArrangeSelectedFormation();
    }

    private void ProcessOverlayCommands()
    {
        while (overlayCommands.TryDequeue(out var request))
        {
            var command = request.Command;
            try
            {
                if (multiplayerSession && IsRestrictedInMultiplayer(command))
                {
                    ShowOperationStatus("该功能会改变本地模拟状态，联机对局中不可用。", true);
                    continue;
                }
                var previousState = GetToggleState(command);
                var affectedCount = commandDispatcher.Execute(request);
                ReportCommandResult(request, previousState, affectedCount);
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException)
            {
                ShowOperationStatus($"操作未能执行：{error.Message}", true);
            }
        }
    }

    private bool? GetToggleState(OverlayCommand command) => command switch
    {
        OverlayCommand.ToggleRevealMap => revealMapEnabled,
        OverlayCommand.ToggleInfiniteMoney => infiniteMoneyEnabled,
        OverlayCommand.ToggleOneHitKill => oneHitKillEnabled,
        OverlayCommand.ToggleHighDefense => highDefenseEnabled,
        OverlayCommand.ToggleEliteUnits => eliteUnitsEnabled,
        OverlayCommand.ToggleCrateRouteLines => crateRouteLinesEnabled,
        OverlayCommand.ToggleMaximumPower => maximumPowerEnabled,
        OverlayCommand.ToggleFullTech => fullTechEnabled,
        OverlayCommand.ToggleUnlimitedProduction => unlimitedProductionEnabled,
        OverlayCommand.ToggleChronoLegionnaireNoCooldown => chronoLegionnaireNoCooldownEnabled,
        OverlayCommand.ToggleInstantBuild => instantBuildEnabled,
        OverlayCommand.ToggleBuildAnywhere => buildAnywhereEnabled,
        OverlayCommand.ToggleAutoRepair => autoRepairEnabled,
        OverlayCommand.ToggleSuperWeaponNoCooldown => superWeaponNoCooldownEnabled,
        OverlayCommand.ToggleParatrooperNoCooldown => paratrooperNoCooldownEnabled,
        OverlayCommand.ToggleFastTurn => fastTurnEnabled,
        OverlayCommand.ToggleDisableGapGenerators => disableGapGeneratorsEnabled,
        OverlayCommand.ToggleInvadeMode => invadeModeEnabled,
        OverlayCommand.ToggleGamePause => gamePaused,
        OverlayCommand.ToggleGodMode => godModeEnabled,
        OverlayCommand.ToggleSellEverything => sellEverythingEnabled,
        OverlayCommand.ToggleSpySatelliteAndRadar => spySatelliteAndRadarEnabled,
        OverlayCommand.ToggleDetectDisguises => detectDisguisesEnabled,
        OverlayCommand.ToggleMindControlImmunity => mindControlImmunityEnabled,
        OverlayCommand.ToggleAutoUnitRepair => autoUnitRepairEnabled,
        OverlayCommand.ToggleRapidAttack => rapidAttackEnabled,
        OverlayCommand.ToggleFastReload => fastReloadEnabled,
        OverlayCommand.ToggleLargeAmmo => largeAmmoEnabled,
        OverlayCommand.ToggleExtendedGuardRange => extendedGuardRangeEnabled,
        OverlayCommand.ToggleFastInfantry => fastInfantryEnabled,
        OverlayCommand.ToggleSpyVsSpy => spyVsSpyEnabled,
        OverlayCommand.ToggleDisableShields => disableShieldsEnabled,
        OverlayCommand.ToggleFreezeEnemies => freezeEnemiesEnabled,
        OverlayCommand.ToggleEnemyRepairsCauseDamage => enemyRepairsCauseDamageEnabled,
        OverlayCommand.ToggleCounterYuriControl => counterYuriControlEnabled,
        OverlayCommand.ToggleCaptureAttackers => captureAttackersEnabled,
        OverlayCommand.TogglePreventEnemyCaptures => preventEnemyCapturesEnabled,
        OverlayCommand.TogglePreventEnemyGarrison => preventEnemyGarrisonEnabled,
        _ => null
    };

    private void ReportCommandResult(
        OverlayCommandRequest request, bool? previousState, int? affectedCount)
    {
        var command = request.Command;
        if (previousState is { } previous && GetToggleState(command) is { } current)
        {
            var feature = GetFeatureDisplayName(command);
            if (previous == current)
            {
                if (multiplayerSession)
                    return;
                ShowOperationStatus(
                    $"{feature}未能{(previous ? "停用" : "启用")}，请确认当前对局状态。", true);
                return;
            }
            if (!current && command == OverlayCommand.ToggleSuperWeaponNoCooldown &&
                paratrooperNoCooldownEnabled)
            {
                ShowOperationStatus("超级武器无冷却已停用；伞兵无冷却仍保持生效。");
                return;
            }
            if (!current && command == OverlayCommand.ToggleParatrooperNoCooldown &&
                superWeaponNoCooldownEnabled)
            {
                ShowOperationStatus("伞兵独立开关已停用；超级武器无冷却仍会覆盖伞兵。");
                return;
            }
            ShowOperationStatus($"{feature}已{(current ? "启用" : "停用")}。");
            return;
        }

        var message = command switch
        {
            OverlayCommand.ArrangeSelectedFormation when affectedCount > 0 =>
                $"已让 {affectedCount} 个单位进行方阵排列。",
            OverlayCommand.ToggleSelectedSpinningMcvs when affectedCount > 0 =>
                $"已让 {affectedCount} 辆基地车开始转圈。",
            OverlayCommand.ToggleSelectedSpinningMcvs when affectedCount < 0 =>
                $"已让 {-affectedCount} 辆基地车停止转圈。",
            OverlayCommand.ToggleSelectedSpinningMcvs =>
                "未找到选中的己方基地车，请先选择基地车再按快捷键。",
            OverlayCommand.ToggleSelectedInfiniteRange when affectedCount > 0 =>
                $"已为 {affectedCount} 个选中单位解锁无限射程。",
            OverlayCommand.ToggleSelectedInfiniteRange when affectedCount < 0 =>
                $"已恢复 {-affectedCount} 个选中单位的正常射程。",
            OverlayCommand.ToggleSelectedInfiniteRange =>
                "未找到可操作的选中单位，请先在游戏中选择己方单位。",
            OverlayCommand.ToggleSelectedInfiniteSpeed when affectedCount > 0 =>
                $"已为 {affectedCount} 个选中单位启用无限移速。",
            OverlayCommand.ToggleSelectedInfiniteSpeed when affectedCount < 0 =>
                $"已恢复 {-affectedCount} 个选中单位的正常移速。",
            OverlayCommand.ToggleSelectedInfiniteSpeed =>
                "未找到可操作的选中单位，请先在游戏中选择己方可移动单位。",
            OverlayCommand.ToggleSelectedCratePickers when affectedCount > 0 =>
                $"已为 {affectedCount} 个单位启用自动捡箱子。",
            OverlayCommand.ToggleSelectedCratePickers when affectedCount < 0 =>
                $"已停止 {-affectedCount} 个单位自动捡箱子。",
            OverlayCommand.DeleteSelectedObjects when affectedCount > 0 =>
                $"已删除 {affectedCount} 个选中对象。",
            OverlayCommand.DeleteSelectedObjects =>
                "未找到选中的对象。",
            OverlayCommand.TakeOwnershipSelectedObjects when affectedCount > 0 =>
                $"已将 {affectedCount} 个选中单位转为我方阵营。",
            OverlayCommand.TakeOwnershipSelectedObjects =>
                "未找到可转换阵营的选中单位。",
            OverlayCommand.PromoteSelectedUnits when affectedCount > 0 =>
                $"已将 {affectedCount} 个选中单位提升到三级。",
            OverlayCommand.PromoteSelectedUnits =>
                "未找到选中的己方单位。",
            OverlayCommand.WinImmediately => "已提交立即胜利。",
            OverlayCommand.GrantNuclearMissile when affectedCount > 0 =>
                "已获得一枚可立即发射的核弹。",
            OverlayCommand.SetMoneyAmount => $"金钱已设置为 {request.Number:0}。",
            OverlayCommand.SetGameSpeed => $"游戏速度已设置为 {request.Number:0}。",
            OverlayCommand.SetBuildQueueLimit => $"建造队列上限已设置为 {request.Number:0}。",
            OverlayCommand.SetParadropInfantryType =>
                $"空降兵类型已设置为 {request.Option}。",
            OverlayCommand.SetParadropCount =>
                $"空降兵数量已设置为 {request.Number:0}。",
            OverlayCommand.SetNuclearMissileDamage or
            OverlayCommand.SetEliteUnitAttackMultiplier or
            OverlayCommand.SetEliteUnitSpeedMultiplier or
            OverlayCommand.SetEliteUnitArmorMultiplier or
            OverlayCommand.SetEliteUnitRateOfFireMultiplier or
            OverlayCommand.SetPsychicDominatorDamage or
            OverlayCommand.SetPrismSupportModifier or
            OverlayCommand.SetPrismSupportDelay or
            OverlayCommand.SetPrismSupportDuration or
            OverlayCommand.SetPrismSupportHeight or
            OverlayCommand.SetV3RocketDamage or
            OverlayCommand.SetEliteV3RocketDamage or
            OverlayCommand.SetDreadnoughtMissileDamage or
            OverlayCommand.SetEliteDreadnoughtMissileDamage or
            OverlayCommand.SetBoomerMissileDamage or
            OverlayCommand.SetEliteBoomerMissileDamage or
            OverlayCommand.SetOrePurifierBonus or
            OverlayCommand.SetLightningStormDamage =>
                $"数值已设置为 {request.Number}。",
            OverlayCommand.ToggleSelectedCratePickers =>
                $"未添加或移除单位：请选择己方可移动单位；若已登记 {MaximumCratePickerUnits} 个，请先停用部分单位。",
            OverlayCommand.ArrangeSelectedFormation =>
                "未找到可操作的选中单位，请先在游戏中选择己方单位。",
            _ => null
        };
        if (message is not null)
            ShowOperationStatus(message, affectedCount == 0);
    }

    private static string GetFeatureDisplayName(OverlayCommand command) => command switch
    {
        OverlayCommand.ToggleRevealMap => "地图全开",
        OverlayCommand.ToggleInfiniteMoney => "无限金钱",
        OverlayCommand.ToggleOneHitKill => "秒杀",
        OverlayCommand.ToggleHighDefense => "高防御",
        OverlayCommand.ToggleEliteUnits => "单位升到三级",
        OverlayCommand.ToggleCrateRouteLines => "显示捡箱路线",
        OverlayCommand.ToggleMaximumPower => "无限电力",
        OverlayCommand.ToggleFullTech => "解锁全部科技",
        OverlayCommand.ToggleUnlimitedProduction => "解除制造数量限制",
        OverlayCommand.ToggleChronoLegionnaireNoCooldown => "超时空单位攻击/传送无冷却",
        OverlayCommand.ToggleInstantBuild => "快速建造",
        OverlayCommand.ToggleBuildAnywhere => "随处建造",
        OverlayCommand.ToggleAutoRepair => "自动修复建筑",
        OverlayCommand.ToggleSuperWeaponNoCooldown => "超级武器无冷却",
        OverlayCommand.ToggleParatrooperNoCooldown => "伞兵无冷却",
        OverlayCommand.ToggleFastTurn => "极速转身",
        OverlayCommand.ToggleDisableGapGenerators => "瘫痪裂缝产生器",
        OverlayCommand.ToggleInvadeMode => "侵略模式",
        OverlayCommand.ToggleGamePause => "暂停游戏",
        OverlayCommand.ToggleGodMode => "无敌模式",
        OverlayCommand.ToggleSellEverything => "随意出售",
        OverlayCommand.ToggleSpySatelliteAndRadar => "间谍卫星与雷达",
        OverlayCommand.ToggleDetectDisguises => "识破伪装",
        OverlayCommand.ToggleMindControlImmunity => "免疫心灵控制",
        OverlayCommand.ToggleAutoUnitRepair => "自动治疗与坦克维修",
        OverlayCommand.ToggleRapidAttack => "极速攻击",
        OverlayCommand.ToggleFastReload => "极速重装",
        OverlayCommand.ToggleLargeAmmo => "大量弹药",
        OverlayCommand.ToggleExtendedGuardRange => "远程警戒",
        OverlayCommand.ToggleFastInfantry => "步兵高速移动",
        OverlayCommand.ToggleSpyVsSpy => "无间道",
        OverlayCommand.ToggleDisableShields => "护盾无效",
        OverlayCommand.ToggleFreezeEnemies => "冻结敌人",
        OverlayCommand.ToggleEnemyRepairsCauseDamage => "敌方血越修越少",
        OverlayCommand.ToggleCounterYuriControl => "反制尤里控制",
        OverlayCommand.ToggleCaptureAttackers => "攻击者归我方",
        OverlayCommand.TogglePreventEnemyCaptures => "阻止敌方占领",
        OverlayCommand.TogglePreventEnemyGarrison => "阻止敌方驻军",
        _ => "功能"
    };

    private void ShowOperationStatus(string message, bool isError = false) =>
        OperationStatusChanged?.Invoke(message, isError);

    private void RefreshOverlay()
    {
        var now = DateTime.UtcNow;
        if (now < nextOverlayRefreshAt)
            return;
        nextOverlayRefreshAt = now + TimeSpan.FromMilliseconds(100);
        StateChanged?.Invoke(new OverlayState
        {
            Connected = true,
            RevealMap = revealMapEnabled,
            InfiniteMoney = infiniteMoneyEnabled,
            OneHitKill = oneHitKillEnabled,
            HighDefense = highDefenseEnabled,
            EliteUnits = eliteUnitsEnabled,
            FormationMode = formationModeEnabled,
            InfiniteRangeMode = infiniteRangeModeEnabled,
            InfiniteSpeedMode = infiniteSpeedModeEnabled,
            SpinningMcvMode = spinningMcvModeEnabled,
            CratePicker = enabled,
            CrateRouteLines = crateRouteLinesEnabled,
            MaximumPower = maximumPowerEnabled,
            FullTech = fullTechEnabled,
            UnlimitedProduction = unlimitedProductionEnabled,
            ChronoLegionnaireNoCooldown = chronoLegionnaireNoCooldownEnabled,
            InstantBuild = instantBuildEnabled,
            BuildAnywhere = buildAnywhereEnabled,
            AutoRepair = autoRepairEnabled,
            SuperWeaponNoCooldown = superWeaponNoCooldownEnabled,
            ParatrooperNoCooldown = paratrooperNoCooldownEnabled,
            FastTurn = fastTurnEnabled,
            DisableGapGenerators = disableGapGeneratorsEnabled,
            InvadeMode = invadeModeEnabled,
            GamePaused = gamePaused,
            GodMode = godModeEnabled,
            SellEverything = sellEverythingEnabled,
            SpySatelliteAndRadar = spySatelliteAndRadarEnabled,
            DetectDisguises = detectDisguisesEnabled,
            MindControlImmunity = mindControlImmunityEnabled,
            AutoUnitRepair = autoUnitRepairEnabled,
            RapidAttack = rapidAttackEnabled,
            FastReload = fastReloadEnabled,
            LargeAmmo = largeAmmoEnabled,
            ExtendedGuardRange = extendedGuardRangeEnabled,
            FastInfantry = fastInfantryEnabled,
            SpyVsSpy = spyVsSpyEnabled,
            DisableShields = disableShieldsEnabled,
            FreezeEnemies = freezeEnemiesEnabled,
            EnemyRepairsCauseDamage = enemyRepairsCauseDamageEnabled,
            CounterYuriControl = counterYuriControlEnabled,
            CaptureAttackers = captureAttackersEnabled,
            PreventEnemyCaptures = preventEnemyCapturesEnabled,
            PreventEnemyGarrison = preventEnemyGarrisonEnabled,
            RestrictedFeaturesAvailable = !multiplayerSession
        });
    }

    private static bool IsRestrictedInMultiplayer(OverlayCommand command) => command is
        OverlayCommand.ToggleRevealMap or
        OverlayCommand.ToggleInfiniteMoney or
        OverlayCommand.ToggleOneHitKill or
        OverlayCommand.ToggleHighDefense or
        OverlayCommand.ToggleMaximumPower or
        OverlayCommand.ToggleFullTech or
        OverlayCommand.ToggleUnlimitedProduction or
        OverlayCommand.ToggleChronoLegionnaireNoCooldown or
        OverlayCommand.ToggleEliteUnits or
        OverlayCommand.ArrangeSelectedFormation or
        OverlayCommand.ToggleSelectedInfiniteRange or
        OverlayCommand.ToggleSelectedInfiniteSpeed or
        OverlayCommand.ToggleSelectedSpinningMcvs or
        OverlayCommand.ToggleInstantBuild or
        OverlayCommand.ToggleBuildAnywhere or
        OverlayCommand.ToggleAutoRepair or
        OverlayCommand.ToggleSuperWeaponNoCooldown or
        OverlayCommand.ToggleParatrooperNoCooldown or
        OverlayCommand.AutoBuildFlakCannon or
        OverlayCommand.AutoBuildPatriotMissile or
        OverlayCommand.AutoBuildPrismTower or
        OverlayCommand.AutoBuildTeslaCoil or
        OverlayCommand.DeleteSelectedObjects or
        OverlayCommand.TakeOwnershipSelectedObjects or
        OverlayCommand.ToggleFastTurn or
        OverlayCommand.ToggleDisableGapGenerators or
        OverlayCommand.ToggleInvadeMode or
        OverlayCommand.ToggleGamePause or
        OverlayCommand.SetMoneyAmount or
        OverlayCommand.SetBuildQueueLimit or
        OverlayCommand.ToggleGodMode or
        OverlayCommand.ToggleSellEverything or
        OverlayCommand.ToggleSpySatelliteAndRadar or
        OverlayCommand.ToggleDetectDisguises or
        OverlayCommand.ToggleMindControlImmunity or
        OverlayCommand.ToggleAutoUnitRepair or
        OverlayCommand.PromoteSelectedUnits or
        OverlayCommand.ToggleRapidAttack or
        OverlayCommand.ToggleFastReload or
        OverlayCommand.ToggleLargeAmmo or
        OverlayCommand.ToggleExtendedGuardRange or
        OverlayCommand.ToggleFastInfantry or
        OverlayCommand.SetGameSpeed or
        OverlayCommand.WinImmediately or
        OverlayCommand.ToggleSpyVsSpy or
        OverlayCommand.GrantNuclearMissile or
        OverlayCommand.ToggleDisableShields or
        OverlayCommand.ToggleFreezeEnemies or
        OverlayCommand.ToggleEnemyRepairsCauseDamage or
        OverlayCommand.ToggleCounterYuriControl or
        OverlayCommand.ToggleCaptureAttackers or
        OverlayCommand.TogglePreventEnemyCaptures or
        OverlayCommand.TogglePreventEnemyGarrison or
        OverlayCommand.SetParadropInfantryType or
        OverlayCommand.SetParadropCount or
        OverlayCommand.SetNuclearMissileDamage or
        OverlayCommand.SetEliteUnitAttackMultiplier or
        OverlayCommand.SetEliteUnitSpeedMultiplier or
        OverlayCommand.SetEliteUnitArmorMultiplier or
        OverlayCommand.SetEliteUnitRateOfFireMultiplier or
        OverlayCommand.SetPsychicDominatorDamage or
        OverlayCommand.SetPrismSupportModifier or
        OverlayCommand.SetPrismSupportDelay or
        OverlayCommand.SetPrismSupportDuration or
        OverlayCommand.SetPrismSupportHeight or
        OverlayCommand.SetV3RocketDamage or
        OverlayCommand.SetEliteV3RocketDamage or
        OverlayCommand.SetDreadnoughtMissileDamage or
        OverlayCommand.SetEliteDreadnoughtMissileDamage or
        OverlayCommand.SetBoomerMissileDamage or
        OverlayCommand.SetEliteBoomerMissileDamage or
        OverlayCommand.SetOrePurifierBonus or
        OverlayCommand.SetLightningStormDamage;

    private void EnforceMultiplayerSafety()
    {
        var hadRestrictedFeature = revealMapEnabled ||
            infiniteMoneyEnabled ||
            oneHitKillEnabled ||
            highDefenseEnabled ||
            eliteUnitsEnabled ||
            infiniteRangeModeEnabled ||
            infiniteSpeedModeEnabled ||
            spinningMcvModeEnabled ||
            maximumPowerEnabled ||
            fullTechEnabled ||
            unlimitedProductionEnabled ||
            chronoLegionnaireNoCooldownEnabled ||
            formationModeEnabled ||
            formationMissions.Count != 0 ||
            formationFacingStates.Count != 0 ||
            instantBuildEnabled ||
            buildAnywhereEnabled ||
            autoRepairEnabled ||
            superWeaponNoCooldownEnabled ||
            paratrooperNoCooldownEnabled ||
            fastTurnEnabled || disableGapGeneratorsEnabled ||
            invadeModeEnabled || gamePaused ||
            godModeEnabled || sellEverythingEnabled ||
            spySatelliteAndRadarEnabled || detectDisguisesEnabled ||
            mindControlImmunityEnabled || autoUnitRepairEnabled ||
            rapidAttackEnabled || fastReloadEnabled || largeAmmoEnabled ||
            extendedGuardRangeEnabled || fastInfantryEnabled ||
            gameSpeedOverridden || spyVsSpyEnabled || disableShieldsEnabled ||
            freezeEnemiesEnabled || enemyRepairsCauseDamageEnabled ||
            counterYuriControlEnabled || captureAttackersEnabled ||
            preventEnemyCapturesEnabled || preventEnemyGarrisonEnabled ||
            rulesOverrides.Count != 0 ||
            autoBuildState is not null;

        StopAutoBuild(null);
        DisableRevealMapBestEffort();
        DisableInfiniteMoney();
        DisableOneHitKill();
        DisableHighDefense();
        DisableEliteUnits();
        DisableInfiniteRangeMode();
        DisableInfiniteSpeedMode();
        DisableSpinningMcvMode();
        DisableMaximumPower();
        DisableFullTech();
        DisableUnlimitedProduction();
        chronoLegionnaireNoCooldownEnabled = false;
        nextChronoLegionnaireRefreshAt = DateTime.MinValue;
        formationModeEnabled = false;
        formationMissions.Clear();
        formationFacingStates.Clear();
        DisableInstantBuild();
        DisableBuildAnywhere();
        autoRepairEnabled = false;
        autoRepairScanCursor = 0;
        autoRepairScanHouse = 0;
        superWeaponNoCooldownEnabled = false;
        paratrooperNoCooldownEnabled = false;
        fastTurnEnabled = false;
        nextFastTurnAt = DateTime.MinValue;
        DisableDisabledGapGenerators();
        DisableInvadeMode();
        DisableGamePause();
        DisablePlayerEnhancements();
        DisableUnitEnhancements();
        RestoreMiscOverrides();
        DisableEnemyAndFunFeatures();
        RestoreRulesOverrides();

        if (hadRestrictedFeature)
            ShowOperationStatus("多人对局中已停用受限制功能。", true);
    }

    private void RequestExit()
    {
        exitRequested = true;
    }
}
