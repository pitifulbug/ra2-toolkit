using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private void StartOverlay()
    {
        var ready = new ManualResetEventSlim();
        try
        {
            overlayThread = new Thread(() =>
            {
                try
                {
                    System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
                    System.Windows.Forms.Application.EnableVisualStyles();
                    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                    var panel = new OverlayPanel(overlayCommands.Enqueue);
                    overlay = panel;
                    panel.Shown += (_, _) =>
                    {
                        ready.Set();
                    };
                    System.Windows.Forms.Application.Run(panel);
                }
                catch (Exception error)
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"桌面控制中心启动失败：{error.Message}",
                        "RA2 Toolkit",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                    ready.Set();
                }
                finally
                {
                    overlay = null;
                }
            })
            {
                IsBackground = true,
                Name = "ra2-toolkit overlay"
            };
            overlayThread.SetApartmentState(ApartmentState.STA);
            overlayThread.Start();
            ready.Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception error)
        {
            System.Windows.Forms.MessageBox.Show(
                $"桌面控制中心启动失败：{error.Message}",
                "RA2 Toolkit",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    private static Func<int?> Command(Action action) => () =>
    {
        action();
        return null;
    };

    private IReadOnlyDictionary<OverlayCommand, Func<int?>> CreateCommandHandlers() =>
        new Dictionary<OverlayCommand, Func<int?>>
        {
            [OverlayCommand.ToggleRevealMap] = Command(ToggleRevealMap),
            [OverlayCommand.ToggleInfiniteMoney] = Command(ToggleInfiniteMoney),
            [OverlayCommand.ToggleOneHitKill] = Command(ToggleOneHitKill),
            [OverlayCommand.ToggleHighDefense] = Command(ToggleHighDefense),
            [OverlayCommand.ToggleCratePicker] = Command(ToggleCratePicker),
            [OverlayCommand.EnableSelectedCratePickers] = () => SetSelectedCratePickers(true),
            [OverlayCommand.DisableSelectedCratePickers] = () => SetSelectedCratePickers(false),
            [OverlayCommand.ToggleCrateRouteLines] = Command(ToggleCrateRouteLines),
            [OverlayCommand.ToggleMaximumPower] = Command(ToggleMaximumPower),
            [OverlayCommand.ToggleFullTech] = Command(ToggleFullTech),
            [OverlayCommand.ToggleUnlimitedProduction] = Command(ToggleUnlimitedProduction),
            [OverlayCommand.ToggleChronoLegionnaireNoCooldown] = Command(ToggleChronoLegionnaireNoCooldown),
            [OverlayCommand.ToggleEliteUnits] = Command(ToggleEliteUnits),
            [OverlayCommand.ToggleFormationMode] = Command(ToggleFormationMode),
            [OverlayCommand.ArrangeSelectedFormation] = () => formationModeEnabled ? ArrangeSelectedFormation() : -1,
            [OverlayCommand.ToggleInfiniteRangeMode] = Command(ToggleInfiniteRangeMode),
            [OverlayCommand.ToggleSelectedInfiniteRange] = () => ToggleSelectedInfiniteRange(),
            [OverlayCommand.ToggleInfiniteSpeedMode] = Command(ToggleInfiniteSpeedMode),
            [OverlayCommand.ToggleSelectedInfiniteSpeed] = () => ToggleSelectedInfiniteSpeed(),
            [OverlayCommand.ToggleSpinningMcvMode] = Command(ToggleSpinningMcvMode),
            [OverlayCommand.ToggleSelectedSpinningMcvs] = () => ToggleSelectedSpinningMcvs(),
            [OverlayCommand.ToggleInstantBuild] = Command(ToggleInstantBuild),
            [OverlayCommand.ToggleBuildAnywhere] = Command(ToggleBuildAnywhere),
            [OverlayCommand.ToggleAutoRepair] = Command(ToggleAutoRepair),
            [OverlayCommand.ToggleSuperWeaponNoCooldown] = Command(ToggleSuperWeaponNoCooldown),
            [OverlayCommand.AutoBuildFlakCannon] = Command(() => ToggleAutoBuild("NAFLAK", "防空炮")),
            [OverlayCommand.AutoBuildPatriotMissile] = Command(() => ToggleAutoBuild("NASAM", "爱国者导弹")),
            [OverlayCommand.AutoBuildPrismTower] = Command(() => ToggleAutoBuild("ATESLA", "光棱塔")),
            [OverlayCommand.AutoBuildTeslaCoil] = Command(() => ToggleAutoBuild("TESLA", "磁暴线圈")),
            [OverlayCommand.DeleteSelectedObjects] = () => DeleteSelectedObjects(),
            [OverlayCommand.TakeOwnershipSelectedObjects] = () => TakeOwnershipOfSelectedObjects(),
            [OverlayCommand.ToggleFastTurn] = Command(ToggleFastTurn),
            [OverlayCommand.ToggleDisableGapGenerators] = Command(ToggleDisabledGapGenerators),
            [OverlayCommand.ToggleInvadeMode] = Command(ToggleInvadeMode),
            [OverlayCommand.ToggleGamePause] = Command(ToggleGamePause),
            [OverlayCommand.ExitProgram] = Command(RequestExit)
        };

    private void ToggleCratePicker()
    {
        if (enabled)
            DisableCratePicker();
        else
            EnableCratePicker();
    }

    private void ToggleChronoLegionnaireNoCooldown()
    {
        chronoLegionnaireNoCooldownEnabled = !chronoLegionnaireNoCooldownEnabled;
        nextChronoLegionnaireRefreshAt = DateTime.MinValue;
    }

    private void ToggleFormationMode()
    {
        formationModeEnabled = !formationModeEnabled;
        if (!formationModeEnabled)
            formationFacingStates.Clear();
    }

    private void ProcessOverlayCommands()
    {
        while (overlayCommands.TryDequeue(out var command))
        {
            try
            {
                if (multiplayerSession && IsRestrictedInMultiplayer(command))
                    continue;
                var previousState = GetToggleState(command);
                var affectedCount = commandDispatcher.Execute(command);
                ReportCommandResult(command, previousState, affectedCount);
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException)
            {
                if (!multiplayerSession)
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
        OverlayCommand.ToggleFormationMode => formationModeEnabled,
        OverlayCommand.ToggleInfiniteRangeMode => infiniteRangeModeEnabled,
        OverlayCommand.ToggleInfiniteSpeedMode => infiniteSpeedModeEnabled,
        OverlayCommand.ToggleSpinningMcvMode => spinningMcvModeEnabled,
        OverlayCommand.ToggleCratePicker => enabled,
        OverlayCommand.ToggleCrateRouteLines => crateRouteLinesEnabled,
        OverlayCommand.ToggleMaximumPower => maximumPowerEnabled,
        OverlayCommand.ToggleFullTech => fullTechEnabled,
        OverlayCommand.ToggleUnlimitedProduction => unlimitedProductionEnabled,
        OverlayCommand.ToggleChronoLegionnaireNoCooldown => chronoLegionnaireNoCooldownEnabled,
        OverlayCommand.ToggleInstantBuild => instantBuildEnabled,
        OverlayCommand.ToggleBuildAnywhere => buildAnywhereEnabled,
        OverlayCommand.ToggleAutoRepair => autoRepairEnabled,
        OverlayCommand.ToggleSuperWeaponNoCooldown => superWeaponNoCooldownEnabled,
        OverlayCommand.ToggleFastTurn => fastTurnEnabled,
        OverlayCommand.ToggleDisableGapGenerators => disableGapGeneratorsEnabled,
        OverlayCommand.ToggleInvadeMode => invadeModeEnabled,
        OverlayCommand.ToggleGamePause => gamePaused,
        _ => null
    };

    private void ReportCommandResult(
        OverlayCommand command, bool? previousState, int? affectedCount)
    {
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
            ShowOperationStatus($"{feature}已{(current ? "启用" : "停用")}。");
            return;
        }

        var message = command switch
        {
            OverlayCommand.ArrangeSelectedFormation when affectedCount > 0 =>
                $"已让 {affectedCount} 个单位进行方阵排列。",
            OverlayCommand.ArrangeSelectedFormation when affectedCount < 0 =>
                "请先在控制面板中启用方阵排列。",
            OverlayCommand.ToggleSelectedSpinningMcvs when affectedCount == int.MinValue =>
                "请先在控制面板中启用基地车转圈。",
            OverlayCommand.ToggleSelectedSpinningMcvs when affectedCount > 0 =>
                $"已让 {affectedCount} 辆基地车开始转圈。",
            OverlayCommand.ToggleSelectedSpinningMcvs when affectedCount < 0 =>
                $"已让 {-affectedCount} 辆基地车停止转圈。",
            OverlayCommand.ToggleSelectedSpinningMcvs =>
                "未找到选中的己方基地车，请先选择基地车再按快捷键。",
            OverlayCommand.ToggleSelectedInfiniteRange when affectedCount == int.MinValue =>
                "请先在控制面板中启用无限射程。",
            OverlayCommand.ToggleSelectedInfiniteRange when affectedCount > 0 =>
                $"已为 {affectedCount} 个选中单位解锁无限射程。",
            OverlayCommand.ToggleSelectedInfiniteRange when affectedCount < 0 =>
                $"已恢复 {-affectedCount} 个选中单位的正常射程。",
            OverlayCommand.ToggleSelectedInfiniteRange =>
                "未找到可操作的选中单位，请先在游戏中选择己方单位。",
            OverlayCommand.ToggleSelectedInfiniteSpeed when affectedCount == int.MinValue =>
                "请先在控制面板中启用无限移速。",
            OverlayCommand.ToggleSelectedInfiniteSpeed when affectedCount > 0 =>
                $"已为 {affectedCount} 个选中单位启用无限移速。",
            OverlayCommand.ToggleSelectedInfiniteSpeed when affectedCount < 0 =>
                $"已恢复 {-affectedCount} 个选中单位的正常移速。",
            OverlayCommand.ToggleSelectedInfiniteSpeed =>
                "未找到可操作的选中单位，请先在游戏中选择己方可移动单位。",
            OverlayCommand.EnableSelectedCratePickers when affectedCount > 0 =>
                $"已为 {affectedCount} 个单位启用自动捡箱子。",
            OverlayCommand.DisableSelectedCratePickers when affectedCount > 0 =>
                $"已停止 {affectedCount} 个单位自动捡箱子。",
            OverlayCommand.EnableSelectedCratePickers when affectedCount < 0 =>
                "请先在控制面板中启用自动捡箱子。",
            OverlayCommand.DisableSelectedCratePickers when affectedCount < 0 =>
                "请先在控制面板中启用自动捡箱子。",
            OverlayCommand.DeleteSelectedObjects when affectedCount > 0 =>
                $"已删除 {affectedCount} 个选中对象。",
            OverlayCommand.DeleteSelectedObjects =>
                "未找到选中的对象。",
            OverlayCommand.TakeOwnershipSelectedObjects when affectedCount > 0 =>
                $"已将 {affectedCount} 个选中单位转为我方阵营。",
            OverlayCommand.TakeOwnershipSelectedObjects =>
                "未找到可转换阵营的选中单位。",
            OverlayCommand.ArrangeSelectedFormation or
            OverlayCommand.EnableSelectedCratePickers or
            OverlayCommand.DisableSelectedCratePickers =>
                "未找到可操作的选中单位，请先在游戏中选择己方单位。",
            _ => null
        };
        if (message is not null)
            ShowOperationStatus(message, affectedCount <= 0);
    }

    private static string GetFeatureDisplayName(OverlayCommand command) => command switch
    {
        OverlayCommand.ToggleRevealMap => "地图全开",
        OverlayCommand.ToggleInfiniteMoney => "无限金钱",
        OverlayCommand.ToggleOneHitKill => "秒杀",
        OverlayCommand.ToggleHighDefense => "高防御",
        OverlayCommand.ToggleEliteUnits => "单位升到三级",
        OverlayCommand.ToggleFormationMode => "方阵排列",
        OverlayCommand.ToggleInfiniteRangeMode => "无限射程",
        OverlayCommand.ToggleInfiniteSpeedMode => "无限移速",
        OverlayCommand.ToggleSpinningMcvMode => "基地车转圈",
        OverlayCommand.ToggleCratePicker => "自动捡箱子",
        OverlayCommand.ToggleCrateRouteLines => "显示捡箱路线",
        OverlayCommand.ToggleMaximumPower => "无限电力",
        OverlayCommand.ToggleFullTech => "解锁全部科技",
        OverlayCommand.ToggleUnlimitedProduction => "解除制造数量限制",
        OverlayCommand.ToggleChronoLegionnaireNoCooldown => "超时空单位攻击/传送无冷却",
        OverlayCommand.ToggleInstantBuild => "快速建造",
        OverlayCommand.ToggleBuildAnywhere => "随处建造",
        OverlayCommand.ToggleAutoRepair => "自动修复建筑",
        OverlayCommand.ToggleSuperWeaponNoCooldown => "超级武器无冷却",
        OverlayCommand.ToggleFastTurn => "极速转身",
        OverlayCommand.ToggleDisableGapGenerators => "瘫痪裂缝产生器",
        OverlayCommand.ToggleInvadeMode => "侵略模式",
        OverlayCommand.ToggleGamePause => "暂停游戏",
        _ => "功能"
    };

    private void ShowOperationStatus(string message, bool isError = false) =>
        overlay?.ShowOperationStatus(message, isError);

    private void RefreshOverlay()
    {
        var now = DateTime.UtcNow;
        if (now < nextOverlayRefreshAt)
            return;
        nextOverlayRefreshAt = now + TimeSpan.FromMilliseconds(100);
        var panel = overlay;
        if (panel is null)
            return;
        panel.UpdateState(new OverlayState(
            revealMapEnabled,
            infiniteMoneyEnabled,
            oneHitKillEnabled,
            highDefenseEnabled,
            eliteUnitsEnabled,
            formationModeEnabled,
            infiniteRangeModeEnabled,
            infiniteSpeedModeEnabled,
            spinningMcvModeEnabled,
            enabled,
            crateRouteLinesEnabled,
            maximumPowerEnabled,
            fullTechEnabled,
            unlimitedProductionEnabled,
            chronoLegionnaireNoCooldownEnabled,
            instantBuildEnabled,
            buildAnywhereEnabled,
            autoRepairEnabled,
            superWeaponNoCooldownEnabled,
            fastTurnEnabled,
            disableGapGeneratorsEnabled,
            invadeModeEnabled,
            gamePaused,
            !multiplayerSession));
    }

    private static bool IsRestrictedInMultiplayer(OverlayCommand command) => command is
        OverlayCommand.ToggleRevealMap or
        OverlayCommand.ToggleInfiniteMoney or
        OverlayCommand.ToggleOneHitKill or
        OverlayCommand.ToggleHighDefense or
        OverlayCommand.ToggleMaximumPower or
        OverlayCommand.ToggleFullTech or
        OverlayCommand.ToggleUnlimitedProduction or
        OverlayCommand.ToggleEliteUnits or
        OverlayCommand.ToggleInfiniteRangeMode or
        OverlayCommand.ToggleSelectedInfiniteRange or
        OverlayCommand.ToggleInfiniteSpeedMode or
        OverlayCommand.ToggleSelectedInfiniteSpeed or
        OverlayCommand.ToggleSpinningMcvMode or
        OverlayCommand.ToggleSelectedSpinningMcvs or
        OverlayCommand.ToggleInstantBuild or
        OverlayCommand.ToggleBuildAnywhere or
        OverlayCommand.ToggleSuperWeaponNoCooldown or
        OverlayCommand.AutoBuildFlakCannon or
        OverlayCommand.AutoBuildPatriotMissile or
        OverlayCommand.AutoBuildPrismTower or
        OverlayCommand.AutoBuildTeslaCoil or
        OverlayCommand.DeleteSelectedObjects or
        OverlayCommand.TakeOwnershipSelectedObjects or
        OverlayCommand.ToggleFastTurn or
        OverlayCommand.ToggleDisableGapGenerators or
        OverlayCommand.ToggleInvadeMode or
        OverlayCommand.ToggleGamePause;

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
            instantBuildEnabled ||
            buildAnywhereEnabled ||
            superWeaponNoCooldownEnabled ||
            fastTurnEnabled || disableGapGeneratorsEnabled ||
            invadeModeEnabled || gamePaused ||
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
        DisableInstantBuild();
        DisableBuildAnywhere();
        superWeaponNoCooldownEnabled = false;
        DisableDisabledGapGenerators();
        DisableInvadeMode();
        DisableGamePause();

        if (hadRestrictedFeature)
            ShowOperationStatus("多人对局中已停用受限制功能。", true);
    }

    private void StopOverlay()
    {
        var panel = overlay;
        if (panel is not null)
            panel.RequestClose();
        if (overlayThread is { IsAlive: true } thread && thread != Thread.CurrentThread)
            thread.Join(2000);
        overlayThread = null;
    }

    private void RequestExit()
    {
        if (exitRequested)
            return;
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
        DisableInstantBuild();
        DisableBuildAnywhere();
        DisableDisabledGapGenerators();
        DisableInvadeMode();
        DisableGamePause();
        formationModeEnabled = false;
        formationFacingStates.Clear();
        autoRepairEnabled = false;
        superWeaponNoCooldownEnabled = false;
        DisableCratePicker();
        exitRequested = true;
    }
}
