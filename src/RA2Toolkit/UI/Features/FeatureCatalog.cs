internal static class FeatureCatalog
{
    private static readonly IReadOnlyList<FeatureChoice> InfantryChoices =
    [
        new("E1", "美国大兵"),
        new("E2", "动员兵"),
        new("INIT", "尤里新兵"),
        new("GGI", "重装大兵"),
        new("FLAKT", "防空步兵"),
        new("SHK", "磁爆步兵"),
        new("SNIPE", "狙击手"),
        new("DESO", "辐射工兵"),
        new("TERROR", "恐怖分子"),
        new("SPY", "间谍"),
        new("ENGINEER", "工程师"),
        new("GHOST", "海豹部队"),
        new("TANY", "谭雅"),
        new("BORIS", "鲍里斯"),
        new("YURI", "尤里"),
        new("YURIPR", "尤里X"),
        new("VIRUS", "病毒狙击手"),
        new("BRUTE", "狂兽人"),
        new("CLEG", "超时空军团兵")
    ];

    internal static IReadOnlyList<FeatureDefinition> All { get; } =
    [
        Number(FeatureCategory.Player, "金钱数量", "设置当前玩家的可用资金。",
            OverlayCommand.SetMoneyAmount, 100_000, 0, 2_000_000_000, 10_000),
        Number(FeatureCategory.Player, "建造队列上限", "设置单个生产队列可排入的最大项目数。",
            OverlayCommand.SetBuildQueueLimit, 99, 1, 1_000, 1),
        Toggle(FeatureCategory.Player, "瞬间完成建造", "快速完成当前及后续单位、建筑生产。",
            OverlayCommand.ToggleInstantBuild, state => state.InstantBuild),
        Toggle(FeatureCategory.Player, "无限电力", "锁定当前玩家的电力供应。",
            OverlayCommand.ToggleMaximumPower, state => state.MaximumPower),
        Toggle(FeatureCategory.Player, "无敌模式", "阻止己方单位与建筑受到常规伤害和超时空抹除。",
            OverlayCommand.ToggleGodMode, state => state.GodMode),
        Toggle(FeatureCategory.Player, "自动修理建筑", "公平轮询并维修受损、已放置的己方建筑。",
            OverlayCommand.ToggleAutoRepair, state => state.AutoRepair),
        Toggle(FeatureCategory.Player, "超级武器无冷却", "持续推进已有超级武器的充能。",
            OverlayCommand.ToggleSuperWeaponNoCooldown, state => state.SuperWeaponNoCooldown),
        Toggle(FeatureCategory.Player, "空降兵无冷却", "持续推进普通空降兵和美国空降兵的充能。",
            OverlayCommand.ToggleParatrooperNoCooldown, state => state.ParatrooperNoCooldown),
        Toggle(FeatureCategory.Player, "出售一切", "允许出售通常不可出售或不满足出售条件的对象。",
            OverlayCommand.ToggleSellEverything, state => state.SellEverything),
        Toggle(FeatureCategory.Player, "随意建筑", "取消基地距离与水面放置限制。",
            OverlayCommand.ToggleBuildAnywhere, state => state.BuildAnywhere),
        Toggle(FeatureCategory.Player, "全科技", "解除当前玩家的可建造科技条件。",
            OverlayCommand.ToggleFullTech, state => state.FullTech),
        Toggle(FeatureCategory.Player, "间谍卫星与雷达", "持续提供雷达和间谍卫星视野能力。",
            OverlayCommand.ToggleSpySatelliteAndRadar, state => state.SpySatelliteAndRadar),
        Toggle(FeatureCategory.Player, "识破伪装", "修改当前己方所用的共享单位类型；敌方同类型单位也会识破伪装。",
            OverlayCommand.ToggleDetectDisguises, state => state.DetectDisguises),
        Toggle(FeatureCategory.Player, "免疫心灵控制", "修改当前己方所用的共享单位类型；敌方同类型单位也会免疫心灵控制。",
            OverlayCommand.ToggleMindControlImmunity, state => state.MindControlImmunity),
        Toggle(FeatureCategory.Player, "地图全开", "调用游戏原生视野效果揭开地图。",
            OverlayCommand.ToggleRevealMap, state => state.RevealMap),

        Toggle(FeatureCategory.Units, "自动治疗与自动坦克维修", "持续恢复己方步兵与载具的生命值。",
            OverlayCommand.ToggleAutoUnitRepair, state => state.AutoUnitRepair),
        Action(FeatureCategory.Units, "选中单位三星", "将当前选中的己方单位提升为精英。",
            OverlayCommand.PromoteSelectedUnits),
        Action(FeatureCategory.Units, "选中单位加速", "切换当前选中单位的高速移动状态。",
            OverlayCommand.ToggleSelectedInfiniteSpeed),
        Action(FeatureCategory.Units, "控制选中单位", "将可转换的选中对象交给当前玩家。",
            OverlayCommand.TakeOwnershipSelectedObjects),
        Action(FeatureCategory.Units, "删除选中单位", "调用游戏原生删除操作；该操作不可撤销。",
            OverlayCommand.DeleteSelectedObjects),
        Toggle(FeatureCategory.Units, "极速攻击", "缩短己方单位的攻击准备时间。",
            OverlayCommand.ToggleRapidAttack, state => state.RapidAttack),
        Toggle(FeatureCategory.Units, "极速转身", "快速同步己方可移动单位的朝向。",
            OverlayCommand.ToggleFastTurn, state => state.FastTurn),
        Toggle(FeatureCategory.Units, "极速重装", "缩短己方单位的装填时间。",
            OverlayCommand.ToggleFastReload, state => state.FastReload),
        Toggle(FeatureCategory.Units, "大量弹药", "持续补充使用弹药计数的己方单位。",
            OverlayCommand.ToggleLargeAmmo, state => state.LargeAmmo),
        Action(FeatureCategory.Units, "无限远程", "切换当前选中单位的无限射程状态。",
            OverlayCommand.ToggleSelectedInfiniteRange),
        Toggle(FeatureCategory.Units, "远程警戒", "扩大当前己方所用单位类型的共享警戒范围；敌方同类型单位也会受影响。",
            OverlayCommand.ToggleExtendedGuardRange, state => state.ExtendedGuardRange),
        Toggle(FeatureCategory.Units, "步兵单位高速移动", "提升全部己方步兵的移动速度。",
            OverlayCommand.ToggleFastInfantry, state => state.FastInfantry),
        Toggle(FeatureCategory.Units, "部队全部三级", "将当前及新建己方单位提升为精英。",
            OverlayCommand.ToggleEliteUnits, state => state.EliteUnits),

        Number(FeatureCategory.Miscellaneous, "游戏速度", "设置游戏速度等级（0 最慢，6 最快）。",
            OverlayCommand.SetGameSpeed, 3, 0, 6, 1),
        Action(FeatureCategory.Miscellaneous, "立即胜利", "立即将当前玩家标记为本局胜者。",
            OverlayCommand.WinImmediately),
        Toggle(FeatureCategory.Miscellaneous, "瘫痪裂缝产生器", "停用敌方裂缝产生器。",
            OverlayCommand.ToggleDisableGapGenerators, state => state.DisableGapGenerators),
        Toggle(FeatureCategory.Miscellaneous, "瞬间超时空", "清除超时空单位的传送后计时器。",
            OverlayCommand.ToggleChronoLegionnaireNoCooldown,
            state => state.ChronoLegionnaireNoCooldown),
        Toggle(FeatureCategory.Miscellaneous, "无间道", "敌方间谍渗透我方建筑时，间谍效果归当前玩家。",
            OverlayCommand.ToggleSpyVsSpy, state => state.SpyVsSpy),
        Action(FeatureCategory.Miscellaneous, "获得一枚核弹", "为当前玩家准备一枚核弹超级武器。",
            OverlayCommand.GrantNuclearMissile),
        Toggle(FeatureCategory.Miscellaneous, "护盾无效", "让护盾不再阻挡伤害。",
            OverlayCommand.ToggleDisableShields, state => state.DisableShields),

        Toggle(FeatureCategory.Enemy, "冻结敌人", "阻止敌方可移动单位继续移动。",
            OverlayCommand.ToggleFreezeEnemies, state => state.FreezeEnemies),
        Toggle(FeatureCategory.Enemy, "敌方血越修越少", "敌方建筑发生维修时反向扣除生命值。",
            OverlayCommand.ToggleEnemyRepairsCauseDamage, state => state.EnemyRepairsCauseDamage),
        Toggle(FeatureCategory.Enemy, "反制尤里控制", "夺回被尤里阵营心灵控制的己方单位。",
            OverlayCommand.ToggleCounterYuriControl, state => state.CounterYuriControl),

        Toggle(FeatureCategory.Fun, "攻击玩家的单位归属给玩家", "把正在攻击己方的敌军转交给当前玩家。",
            OverlayCommand.ToggleCaptureAttackers, state => state.CaptureAttackers),
        Toggle(FeatureCategory.Fun, "敌人无法进行占领事件", "阻止敌方工程师等单位占领建筑。",
            OverlayCommand.TogglePreventEnemyCaptures, state => state.PreventEnemyCaptures),
        Toggle(FeatureCategory.Fun, "敌人无法在房屋驻军", "阻止敌方步兵进入可驻军建筑。",
            OverlayCommand.TogglePreventEnemyGarrison, state => state.PreventEnemyGarrison),
        Choice(FeatureCategory.Fun, "空降兵类型", "自动识别当前阵营并设置空降兵单位。",
            OverlayCommand.SetParadropInfantryType, "E1"),
        Number(FeatureCategory.Fun, "空降兵数量", "自动识别当前阵营并设置每次空降的步兵数量。",
            OverlayCommand.SetParadropCount, 6, 0, 100, 1),

        Number(FeatureCategory.Values, "核弹伤害", "设置核弹武器的基础伤害。",
            OverlayCommand.SetNuclearMissileDamage, 1_000, 0, 2_000_000_000, 100),
        Number(FeatureCategory.Values, "三星单位攻击力", "设置精英单位的攻击力倍率。",
            OverlayCommand.SetEliteUnitAttackMultiplier, 1.1m, 0, 100, 0.05m, 4),
        Number(FeatureCategory.Values, "三星单位移动速度", "设置精英单位的移动速度倍率。",
            OverlayCommand.SetEliteUnitSpeedMultiplier, 1.2m, 0, 100, 0.05m, 4),
        Number(FeatureCategory.Values, "三星单位装甲", "设置精英单位的装甲倍率。",
            OverlayCommand.SetEliteUnitArmorMultiplier, 1.5m, 0, 100, 0.05m, 4),
        Number(FeatureCategory.Values, "三星单位射速", "设置精英单位的开火间隔倍率，越小越快。",
            OverlayCommand.SetEliteUnitRateOfFireMultiplier, 0.6m, 0, 100, 0.05m, 4),
        Number(FeatureCategory.Values, "心灵控制器伤害", "设置心灵控制器爆发伤害。",
            OverlayCommand.SetPsychicDominatorDamage, 1_000, 0, 2_000_000_000, 100),
        Number(FeatureCategory.Values, "光棱塔连携系数", "设置光棱塔支援光束的伤害百分比。",
            OverlayCommand.SetPrismSupportModifier, 150, 0, 10_000, 10),
        Number(FeatureCategory.Values, "光棱塔连携延迟", "设置光棱塔连携延迟帧数。",
            OverlayCommand.SetPrismSupportDelay, 45, 0, 100_000, 1),
        Number(FeatureCategory.Values, "光棱塔连携持续时间", "设置光棱塔连携持续帧数。",
            OverlayCommand.SetPrismSupportDuration, 15, 0, 100_000, 1),
        Number(FeatureCategory.Values, "光棱塔连携高度", "设置光棱塔连携光束高度（256 为一格）。",
            OverlayCommand.SetPrismSupportHeight, 420, 0, 100_000, 10),
        Number(FeatureCategory.Values, "V3火箭伤害", "设置 V3 火箭的普通伤害。",
            OverlayCommand.SetV3RocketDamage, 200, 0, 2_000_000_000, 100),
        Number(FeatureCategory.Values, "V3火箭三星伤害", "设置精英 V3 火箭的伤害。",
            OverlayCommand.SetEliteV3RocketDamage, 400, 0, 2_000_000_000, 100),
        Number(FeatureCategory.Values, "无畏舰导弹伤害", "设置无畏舰导弹的普通伤害。",
            OverlayCommand.SetDreadnoughtMissileDamage, 300, 0, 2_000_000_000, 100),
        Number(FeatureCategory.Values, "无畏舰导弹三星伤害", "设置精英无畏舰导弹的伤害。",
            OverlayCommand.SetEliteDreadnoughtMissileDamage, 600, 0, 2_000_000_000, 100),
        Number(FeatureCategory.Values, "雷鸣潜艇导弹伤害", "设置雷鸣潜艇导弹的普通伤害。",
            OverlayCommand.SetBoomerMissileDamage, 200, 0, 2_000_000_000, 100),
        Number(FeatureCategory.Values, "雷鸣潜艇三星伤害", "设置精英雷鸣潜艇导弹的伤害。",
            OverlayCommand.SetEliteBoomerMissileDamage, 250, 0, 2_000_000_000, 50),
        Number(FeatureCategory.Values, "矿石精炼器加成", "设置收入加成值，例如 0.25 表示加成 25%。",
            OverlayCommand.SetOrePurifierBonus, 0.25m, 0, 100, 0.05m, 4),
        Number(FeatureCategory.Values, "闪电风暴伤害", "设置闪电风暴单次雷击伤害。",
            OverlayCommand.SetLightningStormDamage, 250, 0, 2_000_000_000, 100),

        Toggle(FeatureCategory.Player, "资金下限锁定", "资金低于 100,000 时自动补足；与一次性的“金钱数量”不同。",
            OverlayCommand.ToggleInfiniteMoney, state => state.InfiniteMoney),
        Toggle(FeatureCategory.Player, "解除生产数量限制", "解除当前玩家的单位与建筑拥有数量限制，不改变建造速度或队列长度。",
            OverlayCommand.ToggleUnlimitedProduction, state => state.UnlimitedProduction),
        Action(FeatureCategory.Player, "自动建造防空炮", "围绕选中的己方建筑寻找位置。",
            OverlayCommand.AutoBuildFlakCannon),
        Action(FeatureCategory.Player, "自动建造爱国者导弹", "围绕选中的己方建筑寻找位置。",
            OverlayCommand.AutoBuildPatriotMissile),
        Action(FeatureCategory.Player, "自动建造光棱塔", "围绕选中的己方建筑寻找位置。",
            OverlayCommand.AutoBuildPrismTower),
        Action(FeatureCategory.Player, "自动建造磁暴线圈", "围绕选中的己方建筑寻找位置。",
            OverlayCommand.AutoBuildTeslaCoil),
        Toggle(FeatureCategory.Units, "全军一击必杀", "增强全部当前及新建己方单位的火力；与仅调整三星规则倍率不同。",
            OverlayCommand.ToggleOneHitKill, state => state.OneHitKill),
        Toggle(FeatureCategory.Units, "全军高防御", "增强全部当前及新建己方单位的防御；与完全阻止伤害的无敌模式不同。",
            OverlayCommand.ToggleHighDefense, state => state.HighDefense),
        Action(FeatureCategory.Units, "方阵排列", "让当前选中的己方单位按方阵移动。",
            OverlayCommand.ArrangeSelectedFormation),
        CratePicker(),
        Toggle(FeatureCategory.Miscellaneous, "显示捡箱路线", "只显示正在前往箱子的登记单位。",
            OverlayCommand.ToggleCrateRouteLines, state => state.CrateRouteLines,
            restricted: false),
        Action(FeatureCategory.Fun, "基地车旋转", "切换当前选中基地车的持续旋转状态。",
            OverlayCommand.ToggleSelectedSpinningMcvs),
        Toggle(FeatureCategory.Units, "侵略模式", "放宽当前玩家单位的攻击目标限制。",
            OverlayCommand.ToggleInvadeMode, state => state.InvadeMode),
        Toggle(FeatureCategory.Miscellaneous, "暂停游戏", "暂停或恢复游戏主逻辑更新。",
            OverlayCommand.ToggleGamePause, state => state.GamePaused)
    ];

    internal static string GetCategoryTitle(FeatureCategory category) => category switch
    {
        FeatureCategory.Player => "玩家",
        FeatureCategory.Units => "单位",
        FeatureCategory.Miscellaneous => "杂项",
        FeatureCategory.Enemy => "敌人",
        FeatureCategory.Fun => "趣味",
        FeatureCategory.Values => "数值",
        _ => category.ToString()
    };

    internal static string GetCommandTitle(OverlayCommand command) =>
        All.FirstOrDefault(feature => feature.HotkeyBindingCommand == command)?.Title ??
        All.FirstOrDefault(feature => feature.PrimaryCommand == command)?.Title ??
        command.ToString();

    internal static IReadOnlyCollection<OverlayCommand> CoveredCommands => All
        .SelectMany(feature => new OverlayCommand?[]
        {
            feature.PrimaryCommand,
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
        bool restricted = true) =>
        new(category, title, description, FeatureControlKind.Toggle, command,
            command, command, command, null, restricted, selector);

    private static FeatureDefinition Action(
        FeatureCategory category,
        string title,
        string description,
        OverlayCommand command,
        bool restricted = true) =>
        new(category, title, description, FeatureControlKind.Action, command,
            null, command, command, null, restricted, null);

    private static FeatureDefinition Number(
        FeatureCategory category,
        string title,
        string description,
        OverlayCommand command,
        decimal defaultValue,
        decimal minimum,
        decimal maximum,
        decimal step,
        int decimalPlaces = 0,
        bool restricted = true) =>
        new(category, title, description, FeatureControlKind.Number, command,
            null, null, null, null, restricted, null,
            defaultValue, minimum, maximum, step, decimalPlaces);

    private static FeatureDefinition Choice(
        FeatureCategory category,
        string title,
        string description,
        OverlayCommand command,
        string defaultOption,
        bool restricted = true) =>
        new(category, title, description, FeatureControlKind.Choice, command,
            null, null, null, null, restricted, null,
            Choices: InfantryChoices, DefaultOption: defaultOption);

    private static FeatureDefinition CratePicker() =>
        Action(FeatureCategory.Miscellaneous, "自动捡箱",
            "切换当前选中单位的自动捡箱状态；首次使用会自动启动捡箱服务。",
            OverlayCommand.ToggleSelectedCratePickers, restricted: false);
}
