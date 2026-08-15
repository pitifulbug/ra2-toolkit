using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string[]> SupportedHashes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["gamemd.exe"] =
            [
                "3E81A61775D2745D1DABE397325EF663CD994FFC194DA4E998E3BF5D2D308600"
            ],
            ["gamemd-ares.exe"] =
            [
                "1CB7E4E421E265208A9F43DFD818F3E14388E32D99009886C9ED3B1B10B8894C",
                "1F5E520C08DC2451A3C6294EDB2FB94096B19FC6593BA8D2DBBA2AB76BAAD34C"
            ],
            ["gamemd-spawn.exe"] =
            [
                "8BE5C5043FF3E7D92BAC505BA7CC955B6F7C2C20B2DD6761924B14FB09F4517E",
                "247F72881E1A68C8FC305E3702DC0100A72D17601305999026393C340D5DAEB0"
            ]
        };

    private static readonly IReadOnlyDictionary<string, (long Size, string Version)> ExecutableProfiles =
        new Dictionary<string, (long, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["gamemd.exe"] = (5_286_208, "1.11"),
            ["gamemd-ares.exe"] = (4_813_072, "1.11"),
            ["gamemd-spawn.exe"] = (5_021_696, "1.11-CnCNet-patch-213aa6b")
        };

    private static readonly (long Offset, byte[] Bytes)[] ExecutableFingerprints =
    [
        (0x133066, Convert.FromHexString("C7009CB87E008BF0")),
        (0x15B4E1, Convert.FromHexString("B9D8F58700E805D7EDFF")),
        (0x12DAEF, Convert.FromHexString("B9E8F78700E887FF0800"))
    ];

    private const int EventSize = 111;
    private const int QueueCapacity = 128;
    private const int MissionEventsPerBatch = 16;
    private const long CurrentFrame = 0xA8ED84;
    private const long MaxAhead = 0xA8B550;
    private const long Session = 0xA8B238;
    private const long MultiplayerPlayerCount = 0xA8B54C;
    private const long CurrentObjects = 0xA8ECB8;
    private const long TechnoArray = 0xA8EC78;
    private const long FootArray = 0x8B3DC0;
    private const long UnitArray = 0x8B4108;
    private const long BuildingArray = 0xA8EB40;
    private const long InfantryArray = 0xA83DE8;
    private const long BuildingTypeArray = 0xA83C68;
    private const long FactoryArray = 0xA83E30;
    private const long Map = 0x87F7E8;
    private const int MapBoundsOffset = 0x124;
    private const long HouseArray = 0xA80228;
    private const long CurrentPlayer = 0xA83D4C;
    private const long OutList = 0xA802C8;
    private const long ActionLineTimerStart = 0xB0EA80;
    private const long ActionLineTimerTimeLeft = 0xB0EA88;
    private const long ActionLinesEnabled = 0x843108;
    private const long ActionLineSelectionCheck = 0x6D4735;
    private const long LogicUpdate = 0x55AFB0;
    private const long RevealMapLikeCrate = 0x577D90;
    private const long HouseReshroudMap = 0x50BD10;
    private const long HouseCanBuild = 0x4F7870;
    private const long BuildingTypeCanPlaceHere = 0x464AC0;
    private const long TechnoIsCloseEnough = 0x6F77B0;
    private const long UnitGetFireError = 0x740FD0;
    private const long InfantryGetFireError = 0x51C8B0;
    private const long AircraftGetFireError = 0x41A9E0;
    private const long TechnoRangeValue = 0x6F7248;
    private static readonly byte[] TechnoRangeValueOriginalBytes =
        Convert.FromHexString("8BBBB4000000");
    private const long GapGeneratorLock = 0x6FAF0D;
    private const long BuildAnywhereGround = 0x4A8EB0;
    private const long BuildAnywhereWater = 0x47C9CD;
    private const long InvadeMode = 0x6F85DD;
    private const long LogicUpdateCall = 0x55DC9E;
    private static readonly byte[] BuildAnywhereGroundOriginalBytes = Convert.FromHexString("A14C3DA800");
    private static readonly byte[] BuildAnywhereWaterOriginalBytes = Convert.FromHexString("8B4C241C83F9FF");
    private static readonly byte[] LogicUpdateCallOriginalBytes = Convert.FromHexString("E80DD3FFFF");
    private static readonly byte[] InvadeModeOriginalBytes = Convert.FromHexString("833800740E");
    private const int MaximumCrateActionLineUnits = 100;
    private const int CrateActionLineCodeCaveSize = 512;
    private static readonly byte[] ActionLineSelectionOriginalBytes =
        Convert.FromHexString("8A868300000084C0");
    private static readonly byte[] LogicUpdateOriginalBytes =
        Convert.FromHexString("83EC288B1540CDAB00");
    private static readonly byte[] RevealMapLikeCrateFingerprint =
        Convert.FromHexString("8B44240453555733FF8BE93BC7BB0100");
    private static readonly byte[] HouseCanBuildOriginalBytes =
        Convert.FromHexString("83EC3C8A442444");
    // The range value hook is the only entry point validated against the
    // reference trainer. The other fire-error entry points have different
    // register/stack contracts across game builds and caused internal errors.
    private const int MaximumInfiniteRangeUnits = 100;
    private const int InfiniteRangeCodeCaveSize = 2048;
    private const int InfiniteRangeCountOffset = 0x200;
    private const int InfiniteRangeTableOffset = 0x204;
    private const int CratesOffset = 0x158;
    private const int ObjectIsOnMapOffset = 0x74;
    private const int ObjectInLimboOffset = 0x81;
    private const int ObjectIsAliveOffset = 0x90;
    private const int ObjectHealthOffset = 0x6C;
    private const int ObjectTypeStrengthOffset = 0xA0;
    private const int TechnoArmorMultiplierOffset = 0x158;
    private const int TechnoFirepowerMultiplierOffset = 0x160;
    private const int FootSpeedMultiplierOffset = 0x580;
    private const long PowerupArguments = 0x89EC28;
    private const int PowerupSpeedArgumentIndex = 10;
    private const int TechnoOwnerOffset = 0x21C;
    private const int TechnoReloadTimerTimeLeftOffset = 0x204;
    private const int TechnoChronoLockRemainingOffset = 0x284;
    private const int TechnoRearmTimerTimeLeftOffset = 0x2F4;
    private const int TechnoVeterancyOffset = 0x150;
    private const int MissionCurrentOffset = 0xAC;
    private const int TechnoPrimaryFacingOffset = 0x388;
    private const int TechnoSecondaryFacingOffset = 0x3A0;
    private const int UnitTypeOffset = 0x6C4;
    private const int FootLocomotorOffset = 0x674;
    private const int TeleportLocomotorTimerTimeLeftOffset = 0x40;
    private const uint TeleportLocomotorVTable = 0x7F5000;
    private const int AbstractTypeIdOffset = 0x24;
    private const int McvSpinFacingStep = 0x800;
    private static readonly HashSet<string> McvTypeIds =
        new(StringComparer.OrdinalIgnoreCase) { "AMCV", "MCV", "SMCV", "PCV" };
    private static readonly HashSet<string> ConstructionYardTypeIds =
        new(StringComparer.OrdinalIgnoreCase) { "GACNST", "NACNST", "YACNST" };
    private static readonly (short X, short Y)[] BaseReturnOffsets =
    [
        (4, 0), (4, 2), (4, 4), (2, 4),
        (0, 4), (-2, 4), (-4, 4), (-4, 2),
        (-4, 0), (-4, -2), (-4, -4), (-2, -4),
        (0, -4), (2, -4), (4, -4), (4, -2),
        (6, 0), (6, 3), (6, 6), (3, 6),
        (0, 6), (-3, 6), (-6, 6), (-6, 3),
        (-6, 0), (-6, -3), (-6, -6), (-3, -6),
        (0, -6), (3, -6), (6, -6), (6, -3)
    ];
    private const int HouseBalanceOffset = 0x30C;
    private const int HouseBaseSpawnCellOffset = 0x5490;
    private const int HouseBaseCenterOffset = 0x5494;
    private const int HouseBuildSpeedOffset = 0x5378;
    private const int HousePowerOutputOffset = 0x53A4;
    private const int HousePowerDrainOffset = 0x53A8;
    private const int HouseBuildingsOffset = 0x68;
    private const int HouseSupersOffset = 0x254;
    private const int HousePowerBlackoutTimerOffset = 0x2A4;
    private const int HouseRecheckPowerOffset = 0x5778;
    private const int HouseRecheckTechTreeOffset = 0x1FC;
    private const int BuildingTypeOffset = 0x520;
    private const int TechnoTypeBuildLimitOffset = 0x3B8;
    private const int BuildingIsBeingRepairedOffset = 0x6E8;
    private const int FactoryProductionValueOffset = 0x24;
    private const int FactoryProductionChangedOffset = 0x28;
    private const int FactoryProductionTimerStartOffset = 0x2C;
    private const int FactoryProductionTimerTimeLeftOffset = 0x34;
    private const int FactoryProductionRateOffset = 0x38;
    private const int FactoryProductionStepOffset = 0x3C;
    private const int FactoryObjectOffset = 0x58;
    private const int FactoryOnHoldOffset = 0x5C;
    private const int FactoryOwnerOffset = 0x6C;
    private const int FactoryIsSuspendedOffset = 0x70;
    private const int SuperRechargeStartOffset = 0x30;
    private const int SuperRechargeTimeLeftOffset = 0x38;
    private const int SuperIsPresentOffset = 0x6D;
    private const int SuperIsReadyOffset = 0x6F;
    private const int SuperIsSuspendedOffset = 0x70;
    private const long PassesProximityCheck = 0x4A8EB0;
    private const double LegacyOverflowingFirepowerMultiplier = 99999.0;
    private const double OneHitKillFirepowerMultiplier = 1000.0;
    private const double ExtremeDefenseArmorMultiplier = 1000.0;
    private const int InfiniteMoneyFloor = 100_000;
    private const int LockedPowerOutput = 1_000_000;
    internal const byte MaximumPowerRecheckFlag = 1;
    internal const long RevealMapRoutineAddress = RevealMapLikeCrate;
    internal const long ReshroudMapRoutineAddress = HouseReshroudMap;
    private const long UpdatePowerFinalComparison = 0x508D8D;
    private static readonly byte[] UpdatePowerOriginalBytes = Convert.FromHexString("8B8EA4530000");

    private readonly Process process;
    private readonly SafeProcessHandle handle;
    private readonly List<UnitState> units = [];
    private readonly Dictionary<int, QueuedMission> pendingMissions = [];
    private readonly Queue<QueuedMission> formationMissions = new();
    private readonly List<FormationFacingState> formationFacingStates = [];
    private DateTime nextFormationFacingAt = DateTime.MinValue;
    private bool enabled;
    private DateTime nextCrateTickAt = DateTime.MinValue;
    private DateTime nextMissionFlushAt = DateTime.MinValue;
    private bool crateRouteLinesEnabled;
    private bool crateActionLinesActive;
    private byte originalActionLinesEnabled;
    private nint crateActionLineCodeCave;
    private long crateActionLineCountAddress;
    private long crateActionLineTableAddress;
    private readonly Dictionary<CrateKey, DateTime> recentlyCollected = [];
    private bool oneHitKillEnabled;
    private bool highDefenseEnabled;
    private bool eliteUnitsEnabled;
    private uint eliteUnitsHouse;
    private readonly Dictionary<uint, EliteUnitState> eliteUnitStates = [];
    private DateTime nextEliteUnitsRefreshAt = DateTime.MinValue;
    private bool formationModeEnabled;
    private bool infiniteRangeModeEnabled;
    private bool infiniteRangePatchInstalled;
    private readonly HashSet<CapturedUnit> infiniteRangeUnits = [];
    private nint infiniteRangeCodeCave;
    private long infiniteRangeCountAddress;
    private long infiniteRangeTableAddress;
    private DateTime nextInfiniteRangeValidationAt = DateTime.MinValue;
    private bool infiniteSpeedModeEnabled;
    private readonly Dictionary<CapturedUnit, double> infiniteSpeedUnits = [];
    private bool fastTurnEnabled;
    private bool disableGapGeneratorsEnabled;
    private readonly Dictionary<uint, (int Id, int OriginalLock)> gapGeneratorStates = [];
    private DateTime nextGapGeneratorRefreshAt = DateTime.MinValue;
    private nint buildAnywhereGroundCodeCave;
    private nint buildAnywhereWaterCodeCave;
    private bool invadeModeEnabled;
    private bool gamePaused;
    private bool spinningMcvModeEnabled;
    private readonly Dictionary<CapturedUnit, SpinningMcvState> spinningMcvs = [];
    private DateTime nextMcvSpinAt = DateTime.MinValue;
    private uint oneHitKillHouse;
    private readonly Dictionary<uint, OneHitKillObjectState> oneHitKillObjects = [];
    private DateTime nextOneHitKillRefreshAt = DateTime.MinValue;
    private bool infiniteMoneyEnabled;
    private DateTime nextInfiniteMoneyRefreshAt = DateTime.MinValue;
    private bool revealMapEnabled;
    private bool maximumPowerEnabled;
    private nint maximumPowerCodeCave;
    private DateTime nextPowerRefreshAt = DateTime.MinValue;
    private bool fullTechEnabled;
    private nint fullTechCodeCave;
    private bool canBuildPatchInstalled;
    private bool unlimitedProductionEnabled;
    private readonly Dictionary<uint, int> unlimitedProductionBuildLimits = [];
    private bool chronoLegionnaireNoCooldownEnabled;
    private DateTime nextChronoLegionnaireRefreshAt = DateTime.MinValue;
    private bool instantBuildEnabled;
    private uint instantBuildHouse;
    private int[]? originalBuildSpeeds;
    private DateTime nextInstantBuildRefreshAt = DateTime.MinValue;
    private bool buildAnywhereEnabled;
    private byte[]? originalProximityCheck;
    private bool autoRepairEnabled;
    private DateTime nextAutoRepairAt = DateTime.MinValue;
    private bool superWeaponNoCooldownEnabled;
    private DateTime nextSuperWeaponRefreshAt = DateTime.MinValue;
    private AutoBuildState? autoBuildState;
    private bool multiplayerSession;
    private readonly ConcurrentQueue<OverlayCommand> overlayCommands = new();
    private Thread? overlayThread;
    private volatile OverlayPanel? overlay;
    private DateTime nextOverlayRefreshAt = DateTime.MinValue;
    private volatile bool exitRequested;
    private readonly OverlayCommandDispatcher commandDispatcher;

    public CratePicker()
    {
        commandDispatcher = new(CreateCommandHandlers());
        var candidates = new[] { "gamemd", "gamemd-ares", "gamemd-spawn" }
            .SelectMany(Process.GetProcessesByName)
            .Where(candidate => !candidate.HasExited)
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException("没有找到 gamemd.exe、gamemd-ares.exe 或 gamemd-spawn.exe。请先启动游戏。");
        if (candidates.Length > 1)
            throw new InvalidOperationException("检测到多个红警游戏进程。请只保留一个游戏进程后重试。");
        process = candidates[0];

        var path = process.MainModule?.FileName
            ?? throw new InvalidOperationException("无法取得游戏路径。请确认本程序已用管理员身份运行。");
        var fileName = Path.GetFileName(path);
        if (!SupportedHashes.TryGetValue(fileName, out var supportedHashes))
            throw new InvalidOperationException($"不受支持的游戏程序：{fileName}");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var exactHashMatch = supportedHashes.Contains(hash, StringComparer.OrdinalIgnoreCase);
        if (!exactHashMatch && !IsCompatibleExecutable(path, fileName, out var compatibilityError))
            throw new InvalidOperationException(
                $"游戏版本不受支持。\n检测到：{hash}\n兼容校验失败：{compatibilityError}");

        const uint access = Native.ProcessQueryInformation | Native.ProcessVmRead |
                            Native.ProcessVmWrite | Native.ProcessVmOperation | Native.ProcessSuspendResume;
        handle = Native.OpenProcess(access, false, process.Id);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开游戏进程");

        ValidateLayout();
        Console.WriteLine($"已连接：{Path.GetFileName(path)}，PID {process.Id}");
        Console.WriteLine(exactHashMatch ? "版本校验：已知哈希。" : "版本校验：兼容补丁指纹通过。");
        Console.WriteLine("游戏内文字注入：已关闭。\n");
    }

    private static bool IsCompatibleExecutable(string path, string fileName, out string error)
    {
        error = string.Empty;
        try
        {
            if (!ExecutableProfiles.TryGetValue(fileName, out var profile))
            {
                error = $"没有 {fileName} 的兼容配置。";
                return false;
            }

            var file = new FileInfo(path);
            if (file.Length != profile.Size)
            {
                error = $"文件大小不匹配（检测到 {file.Length}，需要 {profile.Size}）。";
                return false;
            }

            var version = FileVersionInfo.GetVersionInfo(path).FileVersion ?? string.Empty;
            if (!version.Equals(profile.Version, StringComparison.OrdinalIgnoreCase))
            {
                error = $"文件版本不匹配（检测到 {version}，需要 {profile.Version}）。";
                return false;
            }

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);
            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset > stream.Length - 0x100)
            {
                error = "PE 头位置无效。";
                return false;
            }

            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550 || reader.ReadUInt16() != 0x014C)
            {
                error = "不是受支持的 32 位 x86 PE。";
                return false;
            }

            stream.Position = peOffset + 24;
            if (reader.ReadUInt16() != 0x010B)
            {
                error = "PE 可选头不是 32 位格式。";
                return false;
            }
            stream.Position = peOffset + 24 + 28;
            if (reader.ReadUInt32() != 0x00400000)
            {
                error = "映像基址不是 0x00400000。";
                return false;
            }

            foreach (var fingerprint in ExecutableFingerprints)
            {
                stream.Position = fingerprint.Offset;
                var actual = reader.ReadBytes(fingerprint.Bytes.Length);
                if (!actual.AsSpan().SequenceEqual(fingerprint.Bytes))
                {
                    error = $"关键指令指纹不匹配（文件偏移 0x{fingerprint.Offset:X}）。";
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            error = $"读取兼容信息失败：{exception.Message}";
            return false;
        }
    }

    public void Run()
    {
        StartOverlay();
        Console.WriteLine("软件版本：1.0.3");
        Console.WriteLine("使用方法：");
        Console.WriteLine("1. 在游戏中框选一个或多个己方可移动单位。");
        Console.WriteLine("桌面控制中心已启动；可从任务栏切换，关闭窗口会安全退出工具。\n");
        Console.WriteLine("当前程序状态：已关闭。");

        while (!exitRequested)
        {
            if (IsGameProcessUnavailable())
                break;
            try
            {
                multiplayerSession = IsMultiplayerSession();
                if (multiplayerSession)
                    EnforceMultiplayerSafety();
                ProcessOverlayCommands();
                if (infiniteMoneyEnabled)
                    MaintainInfiniteMoney();
                if (oneHitKillEnabled || highDefenseEnabled)
                    MaintainCombatBoost();
                if (eliteUnitsEnabled)
                    MaintainEliteUnits();
                if (chronoLegionnaireNoCooldownEnabled)
                    MaintainChronoLegionnaireNoCooldown();
                if (spinningMcvModeEnabled)
                    MaintainSpinningMcvs();
                if (infiniteRangeModeEnabled)
                    MaintainInfiniteRangeUnits();
                if (fastTurnEnabled)
                    MaintainFastTurn();
                if (disableGapGeneratorsEnabled)
                    MaintainDisabledGapGenerators();
                if (formationFacingStates.Count != 0)
                    MaintainFormationFacing();
                if (maximumPowerEnabled)
                    MaintainMaximumPower();
                if (instantBuildEnabled)
                    MaintainInstantBuild();
                if (autoBuildState is not null)
                    MaintainAutoBuild();
                if (autoRepairEnabled)
                    MaintainAutoRepair();
                if (superWeaponNoCooldownEnabled)
                    MaintainSuperWeaponNoCooldown();
                var now = DateTime.UtcNow;
                if (enabled && now >= nextCrateTickAt)
                {
                    nextCrateTickAt = now + TimeSpan.FromMilliseconds(100);
                    Tick();
                }
                FlushQueuedMissions(now);
                RefreshOverlay();
                Thread.Sleep(15);
            }
            catch (GameProcessExitedException)
            {
                break;
            }
            catch (Win32Exception)
            {
                // The game keeps its process alive briefly while tearing down a finished match.
                // During that transition, previously valid match objects can no longer be read.
                break;
            }
            catch (Exception error) when (
                error is InvalidOperationException && IsGameProcessUnavailable())
            {
                break;
            }
        }
        if (IsGameProcessUnavailable())
            Console.WriteLine("游戏进程已经退出。");
        StopOverlay();
    }

    private bool IsGameProcessUnavailable()
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (Win32Exception)
        {
            return true;
        }
    }

    private void EnableCratePicker()
    {
        if (enabled)
            return;
        enabled = true;
        recentlyCollected.Clear();
        Console.WriteLine("[自动捡箱已开启] 请在游戏中选中单位并按下快捷键；再次选中这些单位并按快捷键即可停止。");
    }

    private void DisableCratePicker()
    {
        var wasEnabled = enabled;
        enabled = false;
        DisableCrateActionLines();
        pendingMissions.Clear();
        foreach (var state in units.Where(state => IsCapturedUnitValid(state.Unit)))
            QueueGuard(state.Unit);
        units.Clear();
        recentlyCollected.Clear();
        if (wasEnabled)
            Console.WriteLine("[自动捡箱已关闭] 所有已登记单位均已停止捡箱，路线已清除。");
    }

    private int SetSelectedCratePickers(bool shouldEnable)
    {
        if (!enabled)
        {
            Console.WriteLine("[未操作] 请先在控制面板中勾选“启用自动捡箱”。");
            return -1;
        }

        var selectedUnits = CaptureSelectedUnits();
        if (selectedUnits.Count == 0)
        {
            Console.WriteLine("[未操作] 请先在游戏中选中一个或多个己方可移动单位，再按自动捡箱快捷键。");
            return 0;
        }

        if (!shouldEnable)
        {
            var selectedSet = selectedUnits
                .Select(selected => (selected.Pointer, selected.Id))
                .ToHashSet();
            var removedUnits = units
                .Where(state => selectedSet.Contains((state.Unit.Pointer, state.Unit.Id)))
                .ToArray();
            foreach (var state in removedUnits)
                pendingMissions.Remove(state.Unit.Id);
            foreach (var state in removedUnits.Where(state => IsCapturedUnitValid(state.Unit)))
                QueueGuard(state.Unit);
            units.RemoveAll(state => selectedSet.Contains((state.Unit.Pointer, state.Unit.Id)));
            RefreshCrateActionLines(units.Where(state => state.InvalidSince is null));
            if (units.Count == 0)
                DisableCrateActionLines();
            Console.WriteLine($"[停止捡箱] 已停止 {removedUnits.Length} 个选中单位：{FormatUnitIds(removedUnits.Select(state => state.Unit.Id))}");
            return removedUnits.Length;
        }

        var addedUnits = selectedUnits
            .Where(selected => units.All(state => state.Unit != selected))
            .ToArray();
        units.AddRange(addedUnits.Select(selected => new UnitState(selected)));
        if (crateRouteLinesEnabled && units.Count != 0)
            EnableCrateActionLines();
        Console.WriteLine($"[开始捡箱] 已添加 {addedUnits.Length} 个选中单位：{FormatUnitIds(addedUnits.Select(unit => unit.Id))}");
        return addedUnits.Length;
    }

    internal static string FormatUnitIds(IEnumerable<int> ids)
    {
        var values = ids.ToArray();
        var summary = string.Join(", ", values.Take(10));
        return values.Length > 10 ? $"{summary}…" : summary;
    }

    private void ToggleCrateRouteLines()
    {
        crateRouteLinesEnabled = !crateRouteLinesEnabled;
        if (!crateRouteLinesEnabled)
        {
            DisableCrateActionLines();
            return;
        }
        if (enabled && units.Count != 0)
        {
            EnableCrateActionLines();
            RefreshCrateActionLines(units.Where(state => state.InvalidSince is null));
        }
    }

}
