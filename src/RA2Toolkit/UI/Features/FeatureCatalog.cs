internal static class FeatureCatalog
{
    internal static IReadOnlyList<FeatureDefinition> All { get; } =
    [
        Toggle(FeatureCategory.Resources, "无限金钱", "资金低于 100,000 时自动补足。",
            OverlayCommand.ToggleInfiniteMoney, s => s.InfiniteMoney, restricted: true),
        Toggle(FeatureCategory.Resources, "无限电力", "仅锁定当前玩家的电力供应。",
            OverlayCommand.ToggleMaximumPower, s => s.MaximumPower, restricted: true),

        Toggle(FeatureCategory.Construction, "快速建造", "快速推进当前玩家正在生产的项目。",
            OverlayCommand.ToggleInstantBuild, s => s.InstantBuild, restricted: true),
        Toggle(FeatureCategory.Construction, "全科技解锁", "解除当前玩家可建造科技条件。",
            OverlayCommand.ToggleFullTech, s => s.FullTech, restricted: true),
        Toggle(FeatureCategory.Construction, "无限生产", "解除当前玩家的制造数量限制。",
            OverlayCommand.ToggleUnlimitedProduction, s => s.UnlimitedProduction, restricted: true),
        Toggle(FeatureCategory.Construction, "随处建造", "取消当前玩家的基地距离与水面放置限制。",
            OverlayCommand.ToggleBuildAnywhere, s => s.BuildAnywhere, restricted: true),
        Toggle(FeatureCategory.Construction, "自动维修", "公平轮询并维修受损、已放置的己方建筑。",
            OverlayCommand.ToggleAutoRepair, s => s.AutoRepair, restricted: true),

        Action(FeatureCategory.AutoConstruction, "自动建造防空炮", "围绕选中的己方建筑寻找位置。",
            OverlayCommand.AutoBuildFlakCannon, restricted: true),
        Action(FeatureCategory.AutoConstruction, "自动建造爱国者导弹", "围绕选中的己方建筑寻找位置。",
            OverlayCommand.AutoBuildPatriotMissile, restricted: true),
        Action(FeatureCategory.AutoConstruction, "自动建造光棱塔", "围绕选中的己方建筑寻找位置。",
            OverlayCommand.AutoBuildPrismTower, restricted: true),
        Action(FeatureCategory.AutoConstruction, "自动建造磁暴线圈", "围绕选中的己方建筑寻找位置。",
            OverlayCommand.AutoBuildTeslaCoil, restricted: true),

        Toggle(FeatureCategory.Combat, "一击必杀", "增强当前及新建己方单位的火力。",
            OverlayCommand.ToggleOneHitKill, s => s.OneHitKill, restricted: true),
        Toggle(FeatureCategory.Combat, "极高防御", "增强当前及新建己方单位的防御。",
            OverlayCommand.ToggleHighDefense, s => s.HighDefense, restricted: true),
        Toggle(FeatureCategory.Combat, "超级武器无冷却", "持续推进已有超级武器的充能。",
            OverlayCommand.ToggleSuperWeaponNoCooldown, s => s.SuperWeaponNoCooldown, restricted: true),
        Toggle(FeatureCategory.Combat, "伞兵无冷却", "仅持续推进普通伞兵和美国伞兵的充能。",
            OverlayCommand.ToggleParatrooperNoCooldown, s => s.ParatrooperNoCooldown, restricted: true),
        Toggle(FeatureCategory.Combat, "全员三级", "将当前及新建己方单位提升为精英。",
            OverlayCommand.ToggleEliteUnits, s => s.EliteUnits, restricted: true),
        ModeAction(FeatureCategory.Combat, "方阵排列", "先启用模式，再用快捷键排列选中单位。",
            OverlayCommand.ToggleFormationMode, OverlayCommand.ArrangeSelectedFormation,
            s => s.FormationMode, restricted: true),
        Toggle(FeatureCategory.Combat, "超时空无冷却", "清除传送单位的攻击与移动计时器。",
            OverlayCommand.ToggleChronoLegionnaireNoCooldown,
            s => s.ChronoLegionnaireNoCooldown, restricted: true),
        ModeAction(FeatureCategory.Combat, "无限射程", "先启用模式，再用快捷键切换选中单位。",
            OverlayCommand.ToggleInfiniteRangeMode, OverlayCommand.ToggleSelectedInfiniteRange,
            s => s.InfiniteRangeMode, restricted: true),
        Toggle(FeatureCategory.Combat, "极速转身", "快速同步己方可移动单位的朝向。",
            OverlayCommand.ToggleFastTurn, s => s.FastTurn, restricted: true),
        Toggle(FeatureCategory.Combat, "侵略模式", "放宽当前玩家单位的攻击目标限制。",
            OverlayCommand.ToggleInvadeMode, s => s.InvadeMode, restricted: true),
        ModeAction(FeatureCategory.Combat, "无限移速", "先启用模式，再用快捷键切换选中单位。",
            OverlayCommand.ToggleInfiniteSpeedMode, OverlayCommand.ToggleSelectedInfiniteSpeed,
            s => s.InfiniteSpeedMode, restricted: true),

        Toggle(FeatureCategory.MapAndCrates, "地图全开", "调用游戏原生视野效果揭开地图。",
            OverlayCommand.ToggleRevealMap, s => s.RevealMap, restricted: true),
        new FeatureDefinition(FeatureCategory.MapAndCrates, "自动捡箱", 
            "启用后，快捷键单按加入选中单位，双按移除选中单位。",
            OverlayCommand.ToggleCratePicker, OverlayCommand.ToggleCratePicker,
            OverlayCommand.EnableSelectedCratePickers, OverlayCommand.DisableSelectedCratePickers,
            false, s => s.CratePicker),
        Toggle(FeatureCategory.MapAndCrates, "显示捡箱路线", "只显示正在前往箱子的登记单位。",
            OverlayCommand.ToggleCrateRouteLines, s => s.CrateRouteLines),
        Toggle(FeatureCategory.MapAndCrates, "瘫痪裂缝产生器", "停用敌方裂缝产生器。",
            OverlayCommand.ToggleDisableGapGenerators, s => s.DisableGapGenerators,
            restricted: true),

        ModeAction(FeatureCategory.Game, "基地车旋转", "先启用模式，再用快捷键切换选中的基地车。",
            OverlayCommand.ToggleSpinningMcvMode, OverlayCommand.ToggleSelectedSpinningMcvs,
            s => s.SpinningMcvMode, restricted: true),
        Toggle(FeatureCategory.Game, "暂停游戏", "暂停或恢复游戏主逻辑更新。",
            OverlayCommand.ToggleGamePause, s => s.GamePaused, restricted: true),

        Action(FeatureCategory.Objects, "删除选中对象", "调用游戏原生删除操作；该操作不可撤销。",
            OverlayCommand.DeleteSelectedObjects, restricted: true),
        Action(FeatureCategory.Objects, "选中对象归我方", "将可转换的选中对象交给当前玩家。",
            OverlayCommand.TakeOwnershipSelectedObjects, restricted: true)
    ];

    internal static string GetCategoryTitle(FeatureCategory category) => category switch
    {
        FeatureCategory.Resources => "资源",
        FeatureCategory.Construction => "建造",
        FeatureCategory.AutoConstruction => "自动建造",
        FeatureCategory.Combat => "战斗",
        FeatureCategory.MapAndCrates => "地图与采集",
        FeatureCategory.Game => "游戏",
        FeatureCategory.Objects => "对象",
        _ => category.ToString()
    };

    internal static string GetCommandTitle(OverlayCommand command) =>
        All.FirstOrDefault(feature => feature.HotkeyBindingCommand == command)?.Title ??
        All.FirstOrDefault(feature => feature.ToggleCommand == command)?.Title ??
        command.ToString();

    internal static IReadOnlyCollection<OverlayCommand> CoveredCommands => All
        .SelectMany(feature => new OverlayCommand?[]
        {
            feature.ToggleCommand,
            feature.HotkeyBindingCommand,
            feature.HotkeyPressCommand,
            feature.HotkeyDoublePressCommand
        })
        .Where(command => command is not null)
        .Select(command => command!.Value)
        .Append(OverlayCommand.ExitProgram)
        .ToHashSet();

    private static FeatureDefinition Toggle(
        FeatureCategory category,
        string title,
        string description,
        OverlayCommand command,
        Func<OverlayState, bool> selector,
        bool restricted = false) =>
        new(category, title, description, command, command, command, null,
            restricted, selector);

    private static FeatureDefinition Action(
        FeatureCategory category,
        string title,
        string description,
        OverlayCommand command,
        bool restricted = false) =>
        new(category, title, description, null, command, command, null,
            restricted, null);

    private static FeatureDefinition ModeAction(
        FeatureCategory category,
        string title,
        string description,
        OverlayCommand toggleCommand,
        OverlayCommand actionCommand,
        Func<OverlayState, bool> selector,
        bool restricted = false) =>
        new(category, title, description, toggleCommand, actionCommand, actionCommand, null,
            restricted, selector);
}
