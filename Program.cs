using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

Console.SetOut(TextWriter.Null);

using var singleInstanceMutex = new Mutex(
    true, @"Local\PitifulBug.RA2Toolkit.SingleInstance", out var isFirstInstance);
if (!isFirstInstance)
{
    System.Windows.Forms.MessageBox.Show(
        "RA2 Toolkit 已经在运行，请切换到现有窗口。",
        "RA2 Toolkit",
        System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Information);
    return;
}

try
{
    using var picker = new CratePicker();
    picker.Run();
}
catch (GameProcessExitedException)
{
    // The game closed while the controller was reading its final frame.
}
catch (Exception error)
{
    System.Windows.Forms.MessageBox.Show(
        $"启动失败：{error.Message}",
        "RA2 Toolkit",
        System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Error);
}

internal sealed class CratePicker : IDisposable
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
    private const long FactoryArray = 0xA83E30;
    private const long Map = 0x87F7E8;
    private const long HouseArray = 0xA80228;
    private const long CurrentPlayer = 0xA83D4C;
    private const long OutList = 0xA802C8;
    private const long ActionLineTimerStart = 0xB0EA80;
    private const long ActionLineTimerTimeLeft = 0xB0EA88;
    private const long ActionLinesEnabled = 0x843108;
    private const long ActionLineSelectionCheck = 0x6D4735;
    private const long CellFogUpdate = 0x486A70;
    private const int MaximumCrateActionLineUnits = 100;
    private const int CrateActionLineCodeCaveSize = 512;
    private static readonly byte[] ActionLineSelectionOriginalBytes =
        Convert.FromHexString("8A868300000084C0");
    private static readonly byte[] CellFogUpdateOriginalBytes =
        Convert.FromHexString("A130B2A800");
    private static readonly byte[] CellFogUpdateDisabledBytes =
        Convert.FromHexString("C390909090");
    private const int MapBoundsOffset = 0x124;
    private const int MapValidCellCountOffset = 0x6C;
    private const int MapCellsOffset = 0x138;
    private const int MapRedrawsOffset = 0x1158;
    private const int CratesOffset = 0x158;
    private const int CellVisibilityOffset = 0x120;
    private const int CellAltFlagsOffset = 0x12C;
    private const int CellRevealFlagsLength = 0x18;
    private const int ObjectIsOnMapOffset = 0x74;
    private const int ObjectInLimboOffset = 0x81;
    private const int ObjectIsAliveOffset = 0x90;
    private const int ObjectHealthOffset = 0x6C;
    private const int ObjectTypeStrengthOffset = 0xA0;
    private const int TechnoArmorMultiplierOffset = 0x158;
    private const int TechnoFirepowerMultiplierOffset = 0x160;
    private const int TechnoOwnerOffset = 0x21C;
    private const int TechnoVeterancyOffset = 0x150;
    private const int HouseBalanceOffset = 0x30C;
    private const int HouseVisionaryOffset = 0x240;
    private const int HouseMapIsClearOffset = 0x241;
    private const int HouseBaseSpawnCellOffset = 0x5490;
    private const int HouseBaseCenterOffset = 0x5494;
    private const int HouseBuildSpeedOffset = 0x5378;
    private const int HousePowerOutputOffset = 0x53A4;
    private const int HousePowerDrainOffset = 0x53A8;
    private const int HouseBuildingsOffset = 0x68;
    private const int HouseSupersOffset = 0x254;
    private const int HousePowerBlackoutTimerOffset = 0x2A4;
    private const int HouseRecheckPowerOffset = 0x5778;
    private const int HouseSpySatActiveOffset = 0x577A;
    private const int BuildingTypeOffset = 0x520;
    private const int BuildingIsBeingRepairedOffset = 0x6E8;
    private const int FactoryProductionValueOffset = 0x24;
    private const int FactoryProductionChangedOffset = 0x28;
    private const int FactoryProductionTimerStartOffset = 0x2C;
    private const int FactoryProductionTimerTimeLeftOffset = 0x34;
    private const int FactoryProductionRateOffset = 0x38;
    private const int FactoryProductionStepOffset = 0x3C;
    private const int FactoryObjectOffset = 0x58;
    private const int FactoryOwnerOffset = 0x6C;
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
    private const long UpdatePowerFinalComparison = 0x508D8D;
    private static readonly byte[] UpdatePowerOriginalBytes = Convert.FromHexString("8B8EA4530000");

    private readonly Process process;
    private readonly SafeProcessHandle handle;
    private readonly List<UnitState> units = [];
    private readonly Dictionary<int, QueuedMission> pendingMissions = [];
    private readonly Queue<QueuedMission> formationMissions = new();
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
    private uint oneHitKillHouse;
    private readonly Dictionary<uint, OneHitKillObjectState> oneHitKillObjects = [];
    private DateTime nextOneHitKillRefreshAt = DateTime.MinValue;
    private bool infiniteMoneyEnabled;
    private DateTime nextInfiniteMoneyRefreshAt = DateTime.MinValue;
    private bool revealMapEnabled;
    private uint revealMapHouse;
    private byte originalVisionary;
    private byte originalMapIsClear;
    private byte originalSpySatActive;
    private bool revealMapFogUpdateDisabled;
    private readonly Dictionary<uint, RevealMapCellState> originalRevealMapCells = [];
    private DateTime nextRevealMapRefreshAt = DateTime.MinValue;
    private bool maximumPowerEnabled;
    private nint maximumPowerCodeCave;
    private DateTime nextPowerRefreshAt = DateTime.MinValue;
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
    private bool multiplayerSession;
    private readonly ConcurrentQueue<OverlayCommand> overlayCommands = new();
    private Thread? overlayThread;
    private volatile OverlayPanel? overlay;
    private DateTime nextOverlayRefreshAt = DateTime.MinValue;
    private volatile bool exitRequested;

    public CratePicker()
    {
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
        Console.WriteLine("软件版本：1.0.1");
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
                if (revealMapEnabled)
                    MaintainRevealMap();
                if (infiniteMoneyEnabled)
                    MaintainInfiniteMoney();
                if (oneHitKillEnabled)
                    MaintainOneHitKill();
                if (maximumPowerEnabled)
                    MaintainMaximumPower();
                if (instantBuildEnabled)
                    MaintainInstantBuild();
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
            catch (Exception error) when (
                error is Win32Exception or InvalidOperationException && IsGameProcessUnavailable())
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

    private static string FormatUnitIds(IEnumerable<int> ids)
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

    private void ProcessOverlayCommands()
    {
        while (overlayCommands.TryDequeue(out var command))
        {
            try
            {
                if (multiplayerSession && IsUnsafeInMultiplayer(command))
                {
                    ShowOperationStatus("联机对局中已停用该功能，以避免同步异常。", true);
                    continue;
                }
                var previousState = GetToggleState(command);
                int? affectedCount = null;
                switch (command)
                {
                    case OverlayCommand.ToggleRevealMap:
                        ToggleRevealMap();
                        break;
                    case OverlayCommand.ToggleInfiniteMoney:
                        ToggleInfiniteMoney();
                        break;
                    case OverlayCommand.ToggleCombatBoost:
                        ToggleOneHitKill();
                        break;
                    case OverlayCommand.ToggleCratePicker:
                        if (enabled)
                            DisableCratePicker();
                        else
                            EnableCratePicker();
                        break;
                    case OverlayCommand.EnableSelectedCratePickers:
                        affectedCount = SetSelectedCratePickers(true);
                        break;
                    case OverlayCommand.DisableSelectedCratePickers:
                        affectedCount = SetSelectedCratePickers(false);
                        break;
                    case OverlayCommand.ToggleCrateRouteLines:
                        ToggleCrateRouteLines();
                        break;
                    case OverlayCommand.ToggleMaximumPower:
                        ToggleMaximumPower();
                        break;
                    case OverlayCommand.PromoteSelectedUnits:
                        affectedCount = PromoteSelectedUnits();
                        break;
                    case OverlayCommand.ArrangeSelectedFormation:
                        affectedCount = ArrangeSelectedFormation();
                        break;
                    case OverlayCommand.ToggleInstantBuild:
                        ToggleInstantBuild();
                        break;
                    case OverlayCommand.ToggleBuildAnywhere:
                        ToggleBuildAnywhere();
                        break;
                    case OverlayCommand.ToggleAutoRepair:
                        ToggleAutoRepair();
                        break;
                    case OverlayCommand.ToggleSuperWeaponNoCooldown:
                        ToggleSuperWeaponNoCooldown();
                        break;
                    case OverlayCommand.ExitProgram:
                        RequestExit();
                        break;
                }
                ReportCommandResult(command, previousState, affectedCount);
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
        OverlayCommand.ToggleCombatBoost => oneHitKillEnabled,
        OverlayCommand.ToggleCratePicker => enabled,
        OverlayCommand.ToggleCrateRouteLines => crateRouteLinesEnabled,
        OverlayCommand.ToggleMaximumPower => maximumPowerEnabled,
        OverlayCommand.ToggleInstantBuild => instantBuildEnabled,
        OverlayCommand.ToggleBuildAnywhere => buildAnywhereEnabled,
        OverlayCommand.ToggleAutoRepair => autoRepairEnabled,
        OverlayCommand.ToggleSuperWeaponNoCooldown => superWeaponNoCooldownEnabled,
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
                ShowOperationStatus(
                    $"{feature}未能{(previous ? "停用" : "启用")}，请确认当前对局状态。", true);
                return;
            }
            ShowOperationStatus($"{feature}已{(current ? "启用" : "停用")}。");
            return;
        }

        var message = command switch
        {
            OverlayCommand.PromoteSelectedUnits when affectedCount > 0 =>
                $"已将 {affectedCount} 个单位晋升为三级精英。",
            OverlayCommand.ArrangeSelectedFormation when affectedCount > 0 =>
                $"已向 {affectedCount} 个单位下达方阵集结指令。",
            OverlayCommand.EnableSelectedCratePickers when affectedCount > 0 =>
                $"已为 {affectedCount} 个单位启用战利品搜寻。",
            OverlayCommand.DisableSelectedCratePickers when affectedCount > 0 =>
                $"已停止 {affectedCount} 个单位的战利品搜寻。",
            OverlayCommand.EnableSelectedCratePickers when affectedCount < 0 =>
                "请先在控制面板中启用战利品搜寻。",
            OverlayCommand.DisableSelectedCratePickers when affectedCount < 0 =>
                "请先在控制面板中启用战利品搜寻。",
            OverlayCommand.PromoteSelectedUnits or
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
        OverlayCommand.ToggleRevealMap => "全境洞察",
        OverlayCommand.ToggleInfiniteMoney => "战略资金保障",
        OverlayCommand.ToggleCombatBoost => "绝对火力",
        OverlayCommand.ToggleCratePicker => "战利品搜寻",
        OverlayCommand.ToggleCrateRouteLines => "搜寻路线",
        OverlayCommand.ToggleMaximumPower => "电力保障",
        OverlayCommand.ToggleInstantBuild => "生产线全速运转",
        OverlayCommand.ToggleBuildAnywhere => "前线部署",
        OverlayCommand.ToggleAutoRepair => "战地维护",
        OverlayCommand.ToggleSuperWeaponNoCooldown => "终极武器待命",
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
            enabled,
            crateRouteLinesEnabled,
            maximumPowerEnabled,
            instantBuildEnabled,
            buildAnywhereEnabled,
            autoRepairEnabled,
            superWeaponNoCooldownEnabled,
            multiplayerSession));
    }

    private static bool IsUnsafeInMultiplayer(OverlayCommand command) => command is
        OverlayCommand.ToggleRevealMap or
        OverlayCommand.ToggleInfiniteMoney or
        OverlayCommand.ToggleCombatBoost or
        OverlayCommand.ToggleMaximumPower or
        OverlayCommand.PromoteSelectedUnits or
        OverlayCommand.ToggleInstantBuild or
        OverlayCommand.ToggleBuildAnywhere or
        OverlayCommand.ToggleSuperWeaponNoCooldown;

    private void EnforceMultiplayerSafety()
    {
        DisableRevealMap();
        DisableInfiniteMoney();
        DisableOneHitKill();
        DisableMaximumPower();
        DisableInstantBuild();
        DisableBuildAnywhere();
        superWeaponNoCooldownEnabled = false;
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
        DisableRevealMap();
        DisableInfiniteMoney();
        DisableOneHitKill();
        DisableMaximumPower();
        DisableInstantBuild();
        DisableBuildAnywhere();
        autoRepairEnabled = false;
        superWeaponNoCooldownEnabled = false;
        DisableCratePicker();
        exitRequested = true;
    }

    private void ToggleRevealMap()
    {
        if (revealMapEnabled)
        {
            DisableRevealMap();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine("[解除战争迷雾未开启] 当前玩家阵营指针无效。");
            return;
        }

        try
        {
            revealMapHouse = house;
            CaptureRevealMapState(house);
            DisableCellFogUpdates();
            RevealMap(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            RestoreCellFogUpdates();
            revealMapHouse = 0;
            Console.WriteLine($"[解除战争迷雾未开启] {error.Message}");
            return;
        }

        revealMapEnabled = true;
        nextRevealMapRefreshAt = DateTime.MinValue;
        Console.WriteLine("[解除战争迷雾已开启] 已显示全部地图区域及其中单位；取消勾选可停止保持全图可见。");
    }

    private void MaintainRevealMap()
    {
        var now = DateTime.UtcNow;
        if (now < nextRevealMapRefreshAt)
            return;
        nextRevealMapRefreshAt = now + TimeSpan.FromMilliseconds(250);

        try
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            if (house != revealMapHouse)
            {
                RestoreRevealMapState();
                revealMapHouse = house;
                CaptureRevealMapState(house);
                RevealMap(house);
            }
            else
            {
                WriteBytes(house + HouseSpySatActiveOffset, [1]);
                WriteBytes(house + HouseVisionaryOffset, [1]);
                WriteBytes(house + HouseMapIsClearOffset, [1]);
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            nextRevealMapRefreshAt = now + TimeSpan.FromSeconds(1);
        }
    }

    private void RevealMap(uint house)
    {
        if (ReadUInt32(CurrentPlayer) != house)
            throw new InvalidOperationException("当前玩家阵营已经变化，已停止解除战争迷雾。");
        try
        {
            WriteBytes(house + HouseSpySatActiveOffset, [1]);
            WriteBytes(house + HouseVisionaryOffset, [1]);
            WriteBytes(house + HouseMapIsClearOffset, [1]);
            RevealMapCells();
        }
        catch
        {
            WriteBytes(house + HouseVisionaryOffset, [originalVisionary]);
            WriteBytes(house + HouseMapIsClearOffset, [originalMapIsClear]);
            WriteBytes(house + HouseSpySatActiveOffset, [originalSpySatActive]);
            throw;
        }
    }

    private void DisableRevealMap()
    {
        if (!revealMapEnabled)
            return;
        try
        {
            if (!IsGameProcessUnavailable())
                RestoreRevealMapState();
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or GameProcessExitedException)
        {
            Console.WriteLine($"[解除战争迷雾恢复失败] {error.Message}");
        }
        finally
        {
            try
            {
                if (!IsGameProcessUnavailable())
                    RestoreCellFogUpdates();
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException)
            {
                Console.WriteLine($"[解除战争迷雾代码恢复失败] {error.Message}");
            }
            revealMapEnabled = false;
            revealMapHouse = 0;
            originalVisionary = 0;
            originalMapIsClear = 0;
            originalSpySatActive = 0;
            originalRevealMapCells.Clear();
            Console.WriteLine("[解除战争迷雾已关闭] 已停止保持全图可见；已揭开的区域仍由游戏正常管理。");
        }
    }

    private void CaptureRevealMapState(uint house)
    {
        originalVisionary = ReadByte(house + HouseVisionaryOffset);
        originalMapIsClear = ReadByte(house + HouseMapIsClearOffset);
        originalSpySatActive = ReadByte(house + HouseSpySatActiveOffset);
    }

    private void DisableCellFogUpdates()
    {
        var actual = ReadBytes(CellFogUpdate, CellFogUpdateOriginalBytes.Length);
        if (!actual.AsSpan().SequenceEqual(CellFogUpdateOriginalBytes))
            throw new InvalidOperationException("地图迷雾函数指纹不匹配，未修改游戏代码。");
        WriteCode(CellFogUpdate, CellFogUpdateDisabledBytes);
        revealMapFogUpdateDisabled = true;
    }

    private void RestoreCellFogUpdates()
    {
        if (!revealMapFogUpdateDisabled)
            return;
        WriteCode(CellFogUpdate, CellFogUpdateOriginalBytes);
        revealMapFogUpdateDisabled = false;
    }

    private void RestoreRevealMapState()
    {
        if (revealMapHouse != 0)
            RestoreRevealMapState(revealMapHouse);
    }

    private void RestoreRevealMapState(uint house)
    {
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            foreach (var (pointer, state) in originalRevealMapCells)
                RestoreRevealMapCell(pointer, state);
            WriteInt32(Map + MapRedrawsOffset, 1);
            WriteBytes(house + HouseVisionaryOffset, [originalVisionary]);
            WriteBytes(house + HouseMapIsClearOffset, [originalMapIsClear]);
            WriteBytes(house + HouseSpySatActiveOffset, [originalSpySatActive]);
        }
        finally
        {
            originalRevealMapCells.Clear();
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private void RevealMapCells()
    {
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;

            var items = ReadUInt32(Map + MapCellsOffset + 4);
            var capacity = ReadInt32(Map + MapCellsOffset + 8);
            var validCount = ReadInt32(Map + MapValidCellCountOffset);
            if (items == 0 || capacity is < 1 or > 262144 || validCount is < 1 or > 262144)
                throw new InvalidOperationException(
                    $"地图单元列表异常（容量 {capacity}，有效单元 {validCount}）。");

            var pointers = ReadBytes(items, checked(capacity * 4));
            originalRevealMapCells.Clear();
            for (var index = 0; index < capacity; index++)
            {
                var pointer = BitConverter.ToUInt32(pointers, index * 4);
                if (pointer == 0)
                    continue;

                var state = CaptureRevealMapCell(pointer);
                originalRevealMapCells[pointer] = state;
                ApplyRevealMapCell(pointer, state);
            }

            if (originalRevealMapCells.Count == 0)
                throw new InvalidOperationException(
                    $"地图单元数量不一致（读取 {originalRevealMapCells.Count}，预期 {validCount}）。");
            WriteInt32(Map + MapRedrawsOffset, 1);
        }
        catch
        {
            foreach (var (pointer, state) in originalRevealMapCells)
                RestoreRevealMapCell(pointer, state);
            originalRevealMapCells.Clear();
            throw;
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private RevealMapCellState CaptureRevealMapCell(uint pointer) => new(
        ReadBytes(pointer + CellVisibilityOffset, 2),
        ReadBytes(pointer + CellAltFlagsOffset, CellRevealFlagsLength));

    private void ApplyRevealMapCell(uint pointer, RevealMapCellState state)
    {
        var revealed = (byte[])state.RevealFlags.Clone();
        BitConverter.GetBytes(BitConverter.ToUInt32(revealed, 0x00) | 0x18u)
            .CopyTo(revealed, 0x00); // Mapped | NoFog
        Array.Clear(revealed, 0x04, 8); // ShroudCounter and gap coverage
        revealed[0x0C] = 1; // VisibilityChanged
        BitConverter.GetBytes(BitConverter.ToUInt32(revealed, 0x14) | 0x03u)
            .CopyTo(revealed, 0x14); // CenterRevealed | EdgeRevealed
        WriteBytes(pointer + CellVisibilityOffset, [0xFF, 0xFF]);
        WriteBytes(pointer + CellAltFlagsOffset, revealed);
    }

    private void RestoreRevealMapCell(uint pointer, RevealMapCellState state)
    {
        WriteBytes(pointer + CellVisibilityOffset, state.Visibility);
        WriteBytes(pointer + CellAltFlagsOffset, state.RevealFlags);
    }

    private void ToggleInfiniteMoney()
    {
        if (infiniteMoneyEnabled)
        {
            DisableInfiniteMoney();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine("[无限资金未开启] 当前玩家阵营指针无效。");
            return;
        }

        try
        {
            EnsureInfiniteMoney(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            Console.WriteLine($"[无限资金未开启] {error.Message}");
            return;
        }

        infiniteMoneyEnabled = true;
        nextInfiniteMoneyRefreshAt = DateTime.MinValue;
        Console.WriteLine($"[无限资金已开启] 我方资金下限为 {InfiniteMoneyFloor:N0}；在控制面板中取消勾选即可关闭。");
    }

    private void MaintainInfiniteMoney()
    {
        var now = DateTime.UtcNow;
        if (now < nextInfiniteMoneyRefreshAt)
            return;
        nextInfiniteMoneyRefreshAt = now + TimeSpan.FromMilliseconds(100);

        try
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            if (ReadInt32(house + HouseBalanceOffset) < InfiniteMoneyFloor)
                EnsureInfiniteMoney(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            nextInfiniteMoneyRefreshAt = now + TimeSpan.FromSeconds(1);
        }
    }

    private void EnsureInfiniteMoney(uint house)
    {
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            if (ReadUInt32(CurrentPlayer) != house)
                throw new InvalidOperationException("当前玩家阵营已经变化，已停止写入资金。");
            if (ReadInt32(house + HouseBalanceOffset) < InfiniteMoneyFloor)
                WriteInt32(house + HouseBalanceOffset, InfiniteMoneyFloor);
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private void DisableInfiniteMoney()
    {
        if (!infiniteMoneyEnabled)
            return;
        infiniteMoneyEnabled = false;
        Console.WriteLine("[无限资金已关闭] 当前余额保留，后续消费不再自动补充。");
    }

    private void ToggleOneHitKill()
    {
        if (oneHitKillEnabled)
        {
            DisableOneHitKill();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine("[秒杀未开启] 当前玩家阵营指针无效。");
            return;
        }

        oneHitKillObjects.Clear();
        int affected;
        try
        {
            affected = ApplyOneHitKillToOwnedTechnos(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            oneHitKillObjects.Clear();
            Console.WriteLine($"[秒杀未开启] {error.Message}");
            return;
        }

        oneHitKillHouse = house;
        oneHitKillEnabled = true;
        nextOneHitKillRefreshAt = DateTime.MinValue;
        Console.WriteLine($"[我方攻防强化已开启] 已修改 {affected} 个现有单位/建筑的实际火力与防御倍率；新单位会自动加入。在控制面板中取消勾选即可恢复。");
    }

    private void MaintainOneHitKill()
    {
        var now = DateTime.UtcNow;
        if (now < nextOneHitKillRefreshAt)
            return;
        nextOneHitKillRefreshAt = now + TimeSpan.FromMilliseconds(250);

        try
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            if (house != oneHitKillHouse)
            {
                oneHitKillObjects.Clear();
                oneHitKillHouse = house;
            }
            ApplyOneHitKillToOwnedTechnos(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            nextOneHitKillRefreshAt = now + TimeSpan.FromSeconds(1);
        }
    }

    private void DisableOneHitKill()
    {
        if (!oneHitKillEnabled)
            return;
        var restored = 0;
        var restoreFailed = false;
        try
        {
            if (!IsGameProcessUnavailable())
                restored = RestoreOneHitKillObjects();
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or GameProcessExitedException)
        {
            restoreFailed = true;
            Console.WriteLine($"[攻防恢复失败] {error.Message}");
        }
        finally
        {
            oneHitKillEnabled = false;
            oneHitKillHouse = 0;
            oneHitKillObjects.Clear();
            Console.WriteLine(restoreFailed
                ? "[我方攻防强化已关闭] 未能写回原倍率；请重新开始对局或重启游戏。"
                : $"[我方攻防强化已关闭] 已恢复 {restored} 个仍然存在的我方单位/建筑。");
        }
    }

    private int ApplyOneHitKillToOwnedTechnos(uint house)
    {
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            if (ReadUInt32(CurrentPlayer) != house)
                throw new InvalidOperationException("当前玩家阵营已经变化，已停止写入单位火力倍率。");

            var items = ReadUInt32(TechnoArray + 4);
            var count = ReadInt32(TechnoArray + 16);
            if (items == 0 || count is < 0 or > 10000)
                throw new InvalidOperationException($"TechnoClass 列表异常：{count}。");

            var affected = 0;
            for (var index = 0; index < count; index++)
            {
                var pointer = ReadUInt32(items + index * 4L);
                if (pointer == 0 || ReadUInt32(pointer + TechnoOwnerOffset) != house)
                    continue;
                var id = ReadInt32(pointer + 0x10);
                if (id <= 0)
                    continue;

                if (!oneHitKillObjects.TryGetValue(pointer, out var state) || state.Id != id)
                {
                    var originalFirepower = ReadDouble(pointer + TechnoFirepowerMultiplierOffset);
                    if (originalFirepower == LegacyOverflowingFirepowerMultiplier)
                        originalFirepower = 1.0;
                    var originalArmor = ReadDouble(pointer + TechnoArmorMultiplierOffset);
                    if (!IsReasonableFirepowerMultiplier(originalFirepower) ||
                        !IsReasonableFirepowerMultiplier(originalArmor))
                        continue;
                    state = new OneHitKillObjectState(id, originalFirepower, originalArmor);
                    oneHitKillObjects[pointer] = state;
                }

                if (ReadDouble(pointer + TechnoFirepowerMultiplierOffset) != OneHitKillFirepowerMultiplier)
                    WriteBytes(pointer + TechnoFirepowerMultiplierOffset,
                        BitConverter.GetBytes(OneHitKillFirepowerMultiplier));
                if (ReadDouble(pointer + TechnoArmorMultiplierOffset) != ExtremeDefenseArmorMultiplier)
                    WriteBytes(pointer + TechnoArmorMultiplierOffset,
                        BitConverter.GetBytes(ExtremeDefenseArmorMultiplier));
                affected++;
            }
            return affected;
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private int RestoreOneHitKillObjects()
    {
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            var items = ReadUInt32(TechnoArray + 4);
            var count = ReadInt32(TechnoArray + 16);
            if (items == 0 || count is < 0 or > 10000)
                throw new InvalidOperationException($"TechnoClass 列表异常：{count}。");

            var restored = 0;
            for (var index = 0; index < count; index++)
            {
                var pointer = ReadUInt32(items + index * 4L);
                if (pointer == 0 || !oneHitKillObjects.TryGetValue(pointer, out var state) ||
                    ReadInt32(pointer + 0x10) != state.Id ||
                    ReadUInt32(pointer + TechnoOwnerOffset) != oneHitKillHouse)
                    continue;
                WriteBytes(pointer + TechnoFirepowerMultiplierOffset,
                    BitConverter.GetBytes(state.OriginalFirepowerMultiplier));
                WriteBytes(pointer + TechnoArmorMultiplierOffset,
                    BitConverter.GetBytes(state.OriginalArmorMultiplier));
                restored++;
            }
            return restored;
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private static bool IsReasonableFirepowerMultiplier(double value) =>
        double.IsFinite(value) && value is > 0.0 and <= 1000.0;

    private void Tick()
    {
        var now = DateTime.UtcNow;
        for (var index = units.Count - 1; index >= 0; index--)
        {
            var state = units[index];
            if (IsCapturedUnitValid(state.Unit))
            {
                state.InvalidSince = null;
                continue;
            }

            if (state.InvalidSince is null)
            {
                state.InvalidSince = now;
                pendingMissions.Remove(state.Unit.Id);
                state.ActiveCrate = null;
                state.LastCommandAt = DateTime.MinValue;
                ResetTargetProgress(state);
                ResetWaitingState(state);
                continue;
            }

            if (now - state.InvalidSince.Value < TimeSpan.FromSeconds(2))
                continue;

            units.RemoveAt(index);
        }

        if (units.Count == 0)
        {
            DisableCrateActionLines();
            return;
        }

        var usableUnits = units.Where(state => state.InvalidSince is null).ToArray();
        if (usableUnits.Length == 0)
        {
            RefreshCrateActionLines([]);
            return;
        }

        var crates = ReadActiveCrates();
        var crateByKey = crates.ToDictionary(crate => new CrateKey(crate.Index, crate.X, crate.Y));
        foreach (var expired in recentlyCollected
                     .Where(entry => entry.Value <= now)
                     .Select(entry => entry.Key)
                     .ToArray())
            recentlyCollected.Remove(expired);

        var claimed = new HashSet<CrateKey>();
        var collectedThisTick = new HashSet<CrateKey>();
        foreach (var state in usableUnits)
        {
            foreach (var expired in state.UnreachableCrates
                         .Where(entry => entry.Value <= now)
                         .Select(entry => entry.Key)
                         .ToArray())
                state.UnreachableCrates.Remove(expired);

            var location = ReadUnitCell(state.Unit.Pointer);
            if (state.ActiveCrate is { } reached &&
                DistanceSquared(location, (reached.X, reached.Y)) == 0)
            {
                var reachedKey = new CrateKey(reached.Index, reached.X, reached.Y);
                collectedThisTick.Add(reachedKey);
                recentlyCollected[reachedKey] = now + TimeSpan.FromSeconds(3);
                state.ActiveCrate = null;
                state.LastCommandAt = DateTime.MinValue;
                ResetTargetProgress(state);
            }

            if (state.ActiveCrate is { } previous)
            {
                var previousKey = new CrateKey(previous.Index, previous.X, previous.Y);
                if (!crateByKey.ContainsKey(previousKey) || !claimed.Add(previousKey))
                {
                    state.ActiveCrate = null;
                    state.LastCommandAt = DateTime.MinValue;
                    ResetTargetProgress(state);
                    QueueGuard(state.Unit);
                }
            }

            if (state.ActiveCrate is { } target)
            {
                if (location != state.LastTargetObservedCell)
                {
                    state.LastTargetObservedCell = location;
                    state.LastTargetProgressAt = now;
                }
                else if (now - state.LastTargetProgressAt >= TimeSpan.FromSeconds(3))
                {
                    var key = new CrateKey(target.Index, target.X, target.Y);
                    state.UnreachableCrates[key] = now + TimeSpan.FromSeconds(8);
                    state.ActiveCrate = null;
                    state.LastCommandAt = DateTime.MinValue;
                    ResetTargetProgress(state);
                }
            }

        }

        if (collectedThisTick.Count != 0)
        {
            foreach (var state in usableUnits)
            {
                if (state.ActiveCrate is not { } target ||
                    !collectedThisTick.Contains(new CrateKey(target.Index, target.X, target.Y)))
                    continue;
                claimed.Remove(new CrateKey(target.Index, target.X, target.Y));
                state.ActiveCrate = null;
                state.LastCommandAt = DateTime.MinValue;
                ResetTargetProgress(state);
                QueueGuard(state.Unit);
            }
        }

        foreach (var state in usableUnits)
        {
            var location = ReadUnitCell(state.Unit.Pointer);
            if (state.ActiveCrate is null)
            {
                var nearest = crates
                    .Where(crate =>
                    {
                        var key = new CrateKey(crate.Index, crate.X, crate.Y);
                        return !recentlyCollected.ContainsKey(key) &&
                               !claimed.Contains(key) &&
                               !state.UnreachableCrates.ContainsKey(key);
                    })
                    .MinBy(crate => DistanceSquared(location, (crate.X, crate.Y)));

                if (nearest is not null)
                {
                    ResetWaitingState(state);
                    QueueMove(state.Unit, nearest.X, nearest.Y);
                    state.ActiveCrate = nearest;
                    state.LastCommandAt = now;
                    state.LastTargetObservedCell = location;
                    state.LastTargetProgressAt = now;
                    claimed.Add(new CrateKey(nearest.Index, nearest.X, nearest.Y));
                    continue;
                }

                WaitAtSafePlace(state, now);
                continue;
            }

            ResetWaitingState(state);
        }

        if (crateRouteLinesEnabled)
        {
            EnableCrateActionLines();
            RefreshCrateActionLines(usableUnits);
        }
        else
        {
            DisableCrateActionLines();
        }
    }

    private void EnableCrateActionLines()
    {
        if (crateActionLinesActive)
            return;
        var cave = nint.Zero;
        var patchInstalled = false;
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            var actual = ReadBytes(ActionLineSelectionCheck,
                ActionLineSelectionOriginalBytes.Length);
            if (!actual.AsSpan().SequenceEqual(ActionLineSelectionOriginalBytes))
                throw new InvalidOperationException("行动路线渲染函数指纹不匹配。");

            cave = Native.VirtualAllocEx(handle, 0, CrateActionLineCodeCaveSize,
                Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
            if (cave == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "分配行动路线渲染区失败");

            var caveAddress = cave.ToInt64();
            var countAddress = caveAddress + 64;
            var tableAddress = countAddress + 4;
            WriteBytes(caveAddress, new byte[CrateActionLineCodeCaveSize]);
            var filterCode = CreateCrateActionLineFilter(countAddress, tableAddress);
            WriteBytes(caveAddress, filterCode);
            if (!Native.FlushInstructionCache(handle, cave, (nuint)filterCode.Length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "刷新行动路线指令缓存失败");

            var patch = Enumerable.Repeat((byte)0x90,
                ActionLineSelectionOriginalBytes.Length).ToArray();
            patch[0] = 0xE8;
            BitConverter.GetBytes(checked((int)(caveAddress - (ActionLineSelectionCheck + 5))))
                .CopyTo(patch, 1);
            WriteCode(ActionLineSelectionCheck, patch);
            patchInstalled = true;

            originalActionLinesEnabled = ReadByte(ActionLinesEnabled);
            WriteBytes(ActionLinesEnabled, [1]);
            crateActionLineCodeCave = cave;
            crateActionLineCountAddress = countAddress;
            crateActionLineTableAddress = tableAddress;
            crateActionLinesActive = true;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or OverflowException)
        {
            var safeToFree = !patchInstalled;
            if (patchInstalled)
            {
                try
                {
                    WriteCode(ActionLineSelectionCheck, ActionLineSelectionOriginalBytes);
                    safeToFree = true;
                }
                catch (GameProcessExitedException)
                {
                    safeToFree = true;
                }
                catch (Win32Exception)
                {
                    crateActionLineCodeCave = cave;
                    crateActionLineCountAddress = cave.ToInt64() + 64;
                    crateActionLineTableAddress = crateActionLineCountAddress + 4;
                    crateActionLinesActive = true;
                }
            }
            if (cave != 0 && safeToFree)
                Native.VirtualFreeEx(handle, cave, 0, Native.MemRelease);
            crateRouteLinesEnabled = false;
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private void RefreshCrateActionLines(IEnumerable<UnitState> usableUnits)
    {
        if (!crateActionLinesActive)
            return;
        var usableSet = usableUnits.ToHashSet();
        var pointers = units
            .Where(state => usableSet.Contains(state) && state.ActiveCrate is not null)
            .Select(state => state.Unit.Pointer)
            .Distinct()
            .Take(MaximumCrateActionLineUnits)
            .ToArray();

        WriteInt32(crateActionLineCountAddress, 0);
        if (pointers.Length == 0)
            return;
        var pointerBytes = new byte[pointers.Length * sizeof(uint)];
        Buffer.BlockCopy(pointers, 0, pointerBytes, 0, pointerBytes.Length);
        WriteBytes(crateActionLineTableAddress, pointerBytes);
        WriteInt32(crateActionLineCountAddress, pointers.Length);
        WriteInt32(ActionLineTimerStart, ReadInt32(CurrentFrame));
        WriteInt32(ActionLineTimerTimeLeft, 25);
    }

    private void DisableCrateActionLines()
    {
        if (!crateActionLinesActive)
            return;
        var codeRestored = false;
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            WriteInt32(crateActionLineCountAddress, 0);
            WriteCode(ActionLineSelectionCheck, ActionLineSelectionOriginalBytes);
            codeRestored = true;
            WriteBytes(ActionLinesEnabled, [originalActionLinesEnabled]);
        }
        finally
        {
            if (codeRestored || IsGameProcessUnavailable())
            {
                if (crateActionLineCodeCave != 0)
                    Native.VirtualFreeEx(handle, crateActionLineCodeCave, 0, Native.MemRelease);
                crateActionLinesActive = false;
                crateActionLineCodeCave = 0;
                crateActionLineCountAddress = 0;
                crateActionLineTableAddress = 0;
            }
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private static byte[] CreateCrateActionLineFilter(long countAddress, long tableAddress)
    {
        var code = new List<byte>(48);
        code.AddRange(Convert.FromHexString("8A868300000084C0752351528B0D"));
        code.AddRange(BitConverter.GetBytes(checked((uint)countAddress)));
        code.Add(0xBA);
        code.AddRange(BitConverter.GetBytes(checked((uint)tableAddress)));
        code.AddRange(Convert.FromHexString(
            "85C9740A3932740A83C20449EBF230C0EB02B0015A5984C0C3"));
        return [.. code];
    }

    private List<CapturedUnit> CaptureSelectedUnits()
    {
        var items = ReadUInt32(CurrentObjects + 4);
        var count = ReadInt32(CurrentObjects + 16);
        var result = new List<CapturedUnit>();
        if (items == 0 || count is < 1 or > 100)
            return result;

        var currentPlayer = ReadUInt32(CurrentPlayer);
        var seen = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var pointer = ReadUInt32(items + index * 4L);
            if (pointer == 0 || !seen.Add(pointer) || !VectorContains(FootArray, pointer))
                continue;
            var id = ReadInt32(pointer + 0x10);
            if (id > 0 && ReadUInt32(pointer + TechnoOwnerOffset) == currentPlayer)
                result.Add(new CapturedUnit(pointer, id));
        }
        return result;
    }

    private bool IsCapturedUnitValid(CapturedUnit captured)
    {
        try
        {
            return ReadInt32(captured.Pointer + 0x10) == captured.Id &&
                   VectorContains(FootArray, captured.Pointer) &&
                   ReadUInt32(captured.Pointer + TechnoOwnerOffset) == ReadUInt32(CurrentPlayer) &&
                   ReadByte(captured.Pointer + ObjectIsOnMapOffset) != 0 &&
                   ReadByte(captured.Pointer + ObjectInLimboOffset) == 0 &&
                   ReadByte(captured.Pointer + ObjectIsAliveOffset) != 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private bool IsMultiplayerSession()
    {
        var gameMode = ReadInt32(Session);
        if (gameMode is not (3 or 4)) // GameMode::LAN / GameMode::Internet
            return false;
        return ReadInt32(MultiplayerPlayerCount) > 1;
    }

    private void WaitAtSafePlace(UnitState state, DateTime now)
    {
        var currentCell = ReadUnitCell(state.Unit.Pointer);
        if (!state.WaitingForCrate)
        {
            state.WaitingForCrate = true;
            state.ActiveCrate = null;
            state.SafeCell = ReadSafeCell();
            state.LastSafeObservedCell = currentCell;
            state.LastSafeProgressAt = now;

            if (state.SafeCell is { } target && DistanceSquared(currentCell, target) > 4)
            {
                QueueMove(state.Unit, target.X, target.Y);
            }
            else
            {
                QueueGuard(state.Unit);
                state.SafeCell = null;
            }
            state.LastCommandAt = now;
            return;
        }

        if (state.SafeCell is not { } destination ||
            now - state.LastCommandAt < TimeSpan.FromSeconds(5))
            return;

        if (currentCell != state.LastSafeObservedCell)
        {
            state.LastSafeObservedCell = currentCell;
            state.LastSafeProgressAt = now;
        }

        if (DistanceSquared(currentCell, destination) <= 4)
        {
            QueueGuard(state.Unit);
            state.SafeCell = null;
            state.LastCommandAt = now;
            return;
        }

        if (now - state.LastSafeProgressAt >= TimeSpan.FromSeconds(10))
        {
            QueueGuard(state.Unit);
            state.SafeCell = null;
            state.LastCommandAt = now;
            return;
        }

        QueueMove(state.Unit, destination.X, destination.Y);
        state.LastCommandAt = now;
    }

    private void ToggleMaximumPower()
    {
        if (maximumPowerEnabled)
        {
            DisableMaximumPower();
            return;
        }

        try
        {
            EnableMaximumPowerPatch();
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            Console.WriteLine($"[最高电力未开启] {error.Message}");
            return;
        }
        maximumPowerEnabled = true;
        nextPowerRefreshAt = DateTime.MinValue;
        MaintainMaximumPower();
        Console.WriteLine("[最高电力已开启] 电力输出已锁定为充足状态。");
    }

    private void MaintainMaximumPower()
    {
        var now = DateTime.UtcNow;
        if (now < nextPowerRefreshAt)
            return;
        nextPowerRefreshAt = now + TimeSpan.FromMilliseconds(100);

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;
        var drain = Math.Max(0, ReadInt32(house + HousePowerDrainOffset));
        StopTimer(house + HousePowerBlackoutTimerOffset);
        WriteInt32(house + HousePowerOutputOffset, Math.Max(LockedPowerOutput, drain + 100_000));
        WriteBytes(house + HouseRecheckPowerOffset, [0]);
    }

    private void DisableMaximumPower()
    {
        if (!maximumPowerEnabled)
            return;
        maximumPowerEnabled = false;
        DisableMaximumPowerPatch();
        var house = ReadUInt32(CurrentPlayer);
        if (house != 0)
            WriteBytes(house + HouseRecheckPowerOffset, [1]);
        Console.WriteLine("[最高电力已关闭] 已交还游戏重新计算电力。");
    }

    private void EnableMaximumPowerPatch()
    {
        var actual = ReadBytes(UpdatePowerFinalComparison, UpdatePowerOriginalBytes.Length);
        if (!actual.AsSpan().SequenceEqual(UpdatePowerOriginalBytes))
            throw new InvalidOperationException("电力刷新函数指纹不匹配，未修改游戏代码。");

        maximumPowerCodeCave = Native.VirtualAllocEx(handle, 0, 32,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (maximumPowerCodeCave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配电力补丁代码区失败");

        var caveAddress = maximumPowerCodeCave.ToInt64();
        var code = new List<byte>(20);
        code.AddRange(Convert.FromHexString("C786A453000040420F00")); // PowerOutput = 1,000,000
        code.AddRange(UpdatePowerOriginalBytes); // mov ecx,[esi+PowerOutput]
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(checked((int)
            (UpdatePowerFinalComparison + UpdatePowerOriginalBytes.Length - (caveAddress + code.Count + 4)))));
        WriteBytes(caveAddress, [.. code]);

        var jump = new byte[UpdatePowerOriginalBytes.Length];
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)(caveAddress - (UpdatePowerFinalComparison + 5))))
            .CopyTo(jump, 1);
        jump[5] = 0x90;
        WriteCode(UpdatePowerFinalComparison, jump);
        var house = ReadUInt32(CurrentPlayer);
        if (house != 0)
            WriteBytes(house + HouseRecheckPowerOffset, [1]);
    }

    private void DisableMaximumPowerPatch()
    {
        try
        {
            if (ReadBytes(UpdatePowerFinalComparison, 1)[0] == 0xE9)
                WriteCode(UpdatePowerFinalComparison, UpdatePowerOriginalBytes);
        }
        finally
        {
            if (maximumPowerCodeCave != 0)
            {
                Native.VirtualFreeEx(handle, maximumPowerCodeCave, 0, Native.MemRelease);
                maximumPowerCodeCave = 0;
            }
        }
    }

    private int PromoteSelectedUnits()
    {
        var selected = CaptureSelectedUnits();
        foreach (var unit in selected)
            WriteSingle(unit.Pointer + TechnoVeterancyOffset, 2.0f);
        Console.WriteLine(selected.Count == 0
            ? "[选中单位升星] 未找到己方可移动单位，请先在游戏中选择单位。"
            : $"[选中单位升星] 已将 {selected.Count} 个单位提升为三级精英。" );
        return selected.Count;
    }

    private int ArrangeSelectedFormation()
    {
        var selected = CaptureSelectedUnits();
        if (selected.Count == 0)
            return 0;

        const int spacing = 1;
        var columns = (int)Math.Ceiling(Math.Sqrt(selected.Count));
        var rows = (int)Math.Ceiling(selected.Count / (double)columns);
        var positionedUnits = selected
            .Select(unit => (Unit: unit, Position: ReadUnitCell(unit.Pointer)))
            .OrderBy(item => item.Position.Y)
            .ThenBy(item => item.Position.X)
            .ToArray();

        var centerX = (int)Math.Round(positionedUnits.Average(item => item.Position.X));
        var centerY = (int)Math.Round(positionedUnits.Average(item => item.Position.Y));
        var bounds = ReadBytes(Map + MapBoundsOffset, 16);
        var left = BitConverter.ToInt32(bounds, 0);
        var top = BitConverter.ToInt32(bounds, 4);
        var right = BitConverter.ToInt32(bounds, 8);
        var bottom = BitConverter.ToInt32(bounds, 12);
        var formationHeight = (rows - 1) * spacing;
        var firstRowY = Math.Clamp(centerY - formationHeight / 2, top, bottom - formationHeight);
        var destinations = new List<(short X, short Y)>(selected.Count);

        for (var row = 0; row < rows; row++)
        {
            var countInRow = Math.Min(columns, selected.Count - row * columns);
            var rowWidth = (countInRow - 1) * spacing;
            var firstColumnX = Math.Clamp(centerX - rowWidth / 2, left, right - rowWidth);
            for (var column = 0; column < countInRow; column++)
                destinations.Add((checked((short)(firstColumnX + column * spacing)),
                    checked((short)(firstRowY + row * spacing))));
        }

        var facingDelta = (X: 0, Y: 0);
        foreach (var candidate in new[] { (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0) })
        {
            if (!destinations.All(destination =>
                    destination.X - candidate.X >= left && destination.X - candidate.X <= right &&
                    destination.Y - candidate.Y >= top && destination.Y - candidate.Y <= bottom))
                continue;
            facingDelta = candidate;
            break;
        }
        if (facingDelta == (0, 0))
            return 0;

        formationMissions.Clear();
        foreach (var (item, destination) in positionedUnits.Zip(destinations))
        {
            pendingMissions.Remove(item.Unit.Id);
            var approach = (X: checked((short)(destination.X - facingDelta.X)),
                Y: checked((short)(destination.Y - facingDelta.Y)));
            formationMissions.Enqueue(new QueuedMission(item.Unit, 2, approach));
            formationMissions.Enqueue(new QueuedMission(item.Unit, 3, destination));
        }
        return selected.Count;
    }

    private void ToggleInstantBuild()
    {
        if (instantBuildEnabled)
        {
            DisableInstantBuild();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine("[瞬间建造未开启] 当前玩家阵营无效。");
            return;
        }
        instantBuildHouse = house;
        originalBuildSpeeds = Enumerable.Range(0, 5)
            .Select(index => ReadInt32(house + HouseBuildSpeedOffset + index * 4L)).ToArray();
        instantBuildEnabled = true;
        nextInstantBuildRefreshAt = DateTime.MinValue;
        MaintainInstantBuild();
        Console.WriteLine("[瞬间建造已开启] 生产倍率已锁定，并会立即推进当前生产项目。");
    }

    private void MaintainInstantBuild()
    {
        var now = DateTime.UtcNow;
        if (now < nextInstantBuildRefreshAt)
            return;
        nextInstantBuildRefreshAt = now + TimeSpan.FromMilliseconds(100);

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;
        if (house != instantBuildHouse)
        {
            RestoreBuildSpeeds();
            instantBuildHouse = house;
            originalBuildSpeeds = Enumerable.Range(0, 5)
                .Select(index => ReadInt32(house + HouseBuildSpeedOffset + index * 4L)).ToArray();
        }
        for (var index = 0; index < 5; index++)
            WriteInt32(house + HouseBuildSpeedOffset + index * 4L, 15);

        foreach (var factory in ReadVector(FactoryArray, 256))
        {
            if (ReadUInt32(factory + FactoryOwnerOffset) != house ||
                ReadUInt32(factory + FactoryObjectOffset) == 0)
                continue;
            if (ReadInt32(factory + FactoryProductionValueOffset) >= 54)
                continue;
            WriteInt32(factory + FactoryProductionValueOffset, 53);
            WriteBytes(factory + FactoryProductionChangedOffset, [0]);
            WriteInt32(factory + FactoryProductionTimerStartOffset, ReadInt32(CurrentFrame) - 1);
            WriteInt32(factory + FactoryProductionTimerTimeLeftOffset, 0);
            WriteInt32(factory + FactoryProductionRateOffset, 1);
            WriteInt32(factory + FactoryProductionStepOffset, 1);
        }
    }

    private void DisableInstantBuild()
    {
        if (!instantBuildEnabled)
            return;
        RestoreBuildSpeeds();
        instantBuildEnabled = false;
        instantBuildHouse = 0;
        originalBuildSpeeds = null;
        Console.WriteLine("[瞬间建造已关闭] 已恢复原生产倍率。");
    }

    private void RestoreBuildSpeeds()
    {
        if (instantBuildHouse == 0 || originalBuildSpeeds is null)
            return;
        for (var index = 0; index < originalBuildSpeeds.Length; index++)
            WriteInt32(instantBuildHouse + HouseBuildSpeedOffset + index * 4L, originalBuildSpeeds[index]);
    }

    private void ToggleBuildAnywhere()
    {
        if (buildAnywhereEnabled)
        {
            DisableBuildAnywhere();
            return;
        }

        var expected = Convert.FromHexString("A14C3DA800");
        var actual = ReadBytes(PassesProximityCheck, expected.Length);
        if (!actual.AsSpan().SequenceEqual(expected))
        {
            Console.WriteLine("[随地建造未开启] 邻近范围函数指纹不匹配，未修改游戏代码。");
            return;
        }
        originalProximityCheck = actual;
        WriteCode(PassesProximityCheck, Convert.FromHexString("B001C21000"));
        buildAnywhereEnabled = true;
        Console.WriteLine("[随地建造已开启] 已取消基地邻近范围限制，地形与占用规则仍保留。");
    }

    private void DisableBuildAnywhere()
    {
        if (!buildAnywhereEnabled)
            return;
        if (originalProximityCheck is not null)
            WriteCode(PassesProximityCheck, originalProximityCheck);
        buildAnywhereEnabled = false;
        originalProximityCheck = null;
        Console.WriteLine("[随地建造已关闭] 已恢复游戏原始范围检查。");
    }

    private void ToggleAutoRepair()
    {
        autoRepairEnabled = !autoRepairEnabled;
        nextAutoRepairAt = DateTime.MinValue;
        Console.WriteLine(autoRepairEnabled
            ? "[自动修理已开启] 将自动为受损且未维修的己方建筑下达维修命令。"
            : "[自动修理已关闭]");
    }

    private void MaintainAutoRepair()
    {
        var now = DateTime.UtcNow;
        if (now < nextAutoRepairAt)
            return;
        nextAutoRepairAt = now + TimeSpan.FromSeconds(1);

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;
        var queued = 0;
        foreach (var building in ReadVector(house + HouseBuildingsOffset, 4096))
        {
            var buildingType = ReadUInt32(building + BuildingTypeOffset);
            if (buildingType == 0)
                continue;
            var health = ReadInt32(building + ObjectHealthOffset);
            var strength = ReadInt32(buildingType + ObjectTypeStrengthOffset);
            if (ReadByte(building + ObjectIsAliveOffset) == 0 ||
                ReadByte(building + ObjectInLimboOffset) != 0 ||
                health <= 0 || strength <= 0 || health >= strength ||
                ReadByte(building + BuildingIsBeingRepairedOffset) != 0)
                continue;
            QueueRepair(building);
            if (++queued >= 4)
                break;
        }
    }

    private void QueueRepair(uint building)
    {
        var eventData = CreateEvent(0x15); // EventType::Repair
        BitConverter.GetBytes(ReadInt32(building + 0x10)).CopyTo(eventData, 7);
        eventData[11] = 52; // AbstractType::Abstract
        EnqueueEvent(eventData);
    }

    private void ToggleSuperWeaponNoCooldown()
    {
        superWeaponNoCooldownEnabled = !superWeaponNoCooldownEnabled;
        nextSuperWeaponRefreshAt = DateTime.MinValue;
        Console.WriteLine(superWeaponNoCooldownEnabled
            ? "[超级武器无冷却已开启] 已拥有的超级武器会持续进入就绪状态。"
            : "[超级武器无冷却已关闭] 后续冷却由游戏正常管理。");
    }

    private void MaintainSuperWeaponNoCooldown()
    {
        var now = DateTime.UtcNow;
        if (now < nextSuperWeaponRefreshAt)
            return;
        nextSuperWeaponRefreshAt = now + TimeSpan.FromMilliseconds(100);

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;
        foreach (var super in ReadVector(house + HouseSupersOffset, 256))
        {
            if (ReadByte(super + SuperIsPresentOffset) == 0 ||
                ReadByte(super + SuperIsSuspendedOffset) != 0)
                continue;
            WriteInt32(super + SuperRechargeStartOffset, ReadInt32(CurrentFrame) - 1);
            WriteInt32(super + SuperRechargeTimeLeftOffset, 0);
        }
    }

    private void StopTimer(long timerAddress)
    {
        WriteInt32(timerAddress, -1);
        WriteInt32(timerAddress + 8, 0);
    }

    private IEnumerable<uint> ReadVector(long vectorAddress, int maximumCount)
    {
        var items = ReadUInt32(vectorAddress + 4);
        var count = ReadInt32(vectorAddress + 16);
        if (items == 0 || count is < 0 || count > maximumCount)
            yield break;
        for (var index = 0; index < count; index++)
        {
            var pointer = ReadUInt32(items + index * 4L);
            if (pointer != 0)
                yield return pointer;
        }
    }

    private (short X, short Y)? ReadSafeCell()
    {
        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return null;

        var center = (X: ReadInt16(house + HouseBaseCenterOffset),
            Y: ReadInt16(house + HouseBaseCenterOffset + 2));
        if (center == (0, 0))
        {
            center = (ReadInt16(house + HouseBaseSpawnCellOffset),
                ReadInt16(house + HouseBaseSpawnCellOffset + 2));
        }

        var left = ReadInt32(Map + MapBoundsOffset);
        var top = ReadInt32(Map + MapBoundsOffset + 4);
        var right = ReadInt32(Map + MapBoundsOffset + 8);
        var bottom = ReadInt32(Map + MapBoundsOffset + 12);
        return center.X > 0 && center.Y > 0 &&
               center.X >= left && center.X <= right &&
               center.Y >= top && center.Y <= bottom
            ? center
            : null;
    }

    private static void ResetWaitingState(UnitState state)
    {
        state.WaitingForCrate = false;
        state.SafeCell = null;
        state.LastSafeObservedCell = default;
        state.LastSafeProgressAt = DateTime.MinValue;
    }

    private static void ResetTargetProgress(UnitState state)
    {
        state.LastTargetObservedCell = default;
        state.LastTargetProgressAt = DateTime.MinValue;
    }

    private bool VectorContains(long vectorAddress, uint pointer)
    {
        var items = ReadUInt32(vectorAddress + 4);
        var count = ReadInt32(vectorAddress + 16);
        if (items == 0 || count is < 0 or > 10000)
            return false;

        for (var index = 0; index < count; index++)
            if (ReadUInt32(items + index * 4L) == pointer)
                return true;
        return false;
    }

    private List<CrateSlot> ReadActiveCrates()
    {
        var bounds = ReadBytes(Map + MapBoundsOffset, 16);
        var left = BitConverter.ToInt32(bounds, 0);
        var top = BitConverter.ToInt32(bounds, 4);
        var right = BitConverter.ToInt32(bounds, 8);
        var bottom = BitConverter.ToInt32(bounds, 12);
        var crateData = ReadBytes(Map + CratesOffset, 256 * 16);
        var result = new List<CrateSlot>();

        for (var index = 0; index < 256; index++)
        {
            var offset = index * 16;
            var x = BitConverter.ToInt16(crateData, offset + 12);
            var y = BitConverter.ToInt16(crateData, offset + 14);
            if (x >= left && x <= right && y >= top && y <= bottom && x > 0 && y > 0)
                result.Add(new CrateSlot(index, x, y));
        }
        return result;
    }

    private (int X, int Y) ReadUnitCell(uint pointer)
    {
        var x = ReadInt32(pointer + 0x9C) / 256;
        var y = ReadInt32(pointer + 0xA0) / 256;
        return (x, y);
    }

    private void QueueMove(CapturedUnit captured, short x, short y) =>
        QueueMission(captured, 2, (x, y));

    private void QueueGuard(CapturedUnit captured) => QueueMission(captured, 5, null);

    private byte[] CreateEvent(byte eventType)
    {
        var eventData = new byte[EventSize];
        eventData[0] = eventType;
        eventData[2] = checked((byte)FindCurrentHouseIndex());
        var frame = ReadInt32(CurrentFrame) + Math.Max(0, ReadInt32(MaxAhead));
        BitConverter.GetBytes(frame).CopyTo(eventData, 3);
        return eventData;
    }

    private void QueueMission(CapturedUnit captured, byte mission, (short X, short Y)? destination)
    {
        var queued = new QueuedMission(captured, mission, destination);
        if (IsMultiplayerSession())
        {
            EnqueueEvent(CreateMissionEvent(queued));
            return;
        }
        pendingMissions[captured.Id] = queued;
    }

    private byte[] CreateMissionEvent(QueuedMission queued)
    {
        var eventData = CreateEvent(0x04); // EventType::MegaMission
        BitConverter.GetBytes(queued.Unit.Id).CopyTo(eventData, 7);
        eventData[11] = 52; // AbstractType::Abstract
        eventData[12] = queued.Mission;
        if (queued.Destination is { } cell)
        {
            BitConverter.GetBytes(cell.X + 1000 * cell.Y).CopyTo(eventData, 19);
            eventData[23] = 11; // AbstractType::Cell
        }
        return eventData;
    }

    private void FlushQueuedMissions(DateTime now)
    {
        if (pendingMissions.Count == 0 && formationMissions.Count == 0 || now < nextMissionFlushAt)
            return;
        nextMissionFlushAt = now + TimeSpan.FromMilliseconds(50);

        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            var count = ReadInt32(OutList);
            var tail = ReadInt32(OutList + 8);
            if (count is < 0 or > QueueCapacity || tail is < 0 or >= QueueCapacity)
                throw new InvalidOperationException("游戏事件队列状态异常，已停止写入。");

            var batchLimit = Math.Min(MissionEventsPerBatch, QueueCapacity - count);
            var batch = new List<QueuedMission>(batchLimit);
            while (formationMissions.Count != 0 && batch.Count < batchLimit)
                batch.Add(formationMissions.Dequeue());
            var formationBatchCount = batch.Count;
            if (batch.Count < batchLimit)
                batch.AddRange(pendingMissions.Values.Take(batchLimit - batch.Count));
            if (batch.Count == 0)
                return;

            var timestamp = Environment.TickCount;
            foreach (var queued in batch)
            {
                var eventData = CreateMissionEvent(queued);
                WriteBytes(OutList + 12 + tail * EventSize, eventData);
                WriteInt32(OutList + 12 + QueueCapacity * EventSize + tail * 4L, timestamp);
                tail = (tail + 1) & (QueueCapacity - 1);
                count++;
            }
            WriteInt32(OutList + 8, tail);
            WriteInt32(OutList, count);
            for (var index = formationBatchCount; index < batch.Count; index++)
                pendingMissions.Remove(batch[index].Unit.Id);
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private void EnqueueEvent(byte[] eventData)
    {
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            var count = ReadInt32(OutList);
            var tail = ReadInt32(OutList + 8);
            if (count is < 0 or >= QueueCapacity || tail is < 0 or >= QueueCapacity)
                throw new InvalidOperationException("游戏事件队列状态异常，已停止写入。");

            WriteBytes(OutList + 12 + tail * EventSize, eventData);
            WriteInt32(OutList + 12 + QueueCapacity * EventSize + tail * 4L, Environment.TickCount);
            WriteInt32(OutList + 8, (tail + 1) & (QueueCapacity - 1));
            WriteInt32(OutList, count + 1);
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private int FindCurrentHouseIndex()
    {
        var current = ReadUInt32(CurrentPlayer);
        var items = ReadUInt32(HouseArray + 4);
        var count = ReadInt32(HouseArray + 16);
        for (var index = 0; index < Math.Min(count, 8); index++)
            if (ReadUInt32(items + index * 4L) == current)
                return index;
        throw new InvalidOperationException("无法确定当前玩家编号。");
    }

    private void ValidateLayout()
    {
        var count = ReadInt32(CurrentObjects + 16);
        var houseCount = ReadInt32(HouseArray + 16);
        var queueCount = ReadInt32(OutList);
        var technoCount = ReadInt32(TechnoArray + 16);
        var currentHouse = ReadUInt32(CurrentPlayer);
        var failures = new List<string>();
        if (count is < 0 or > 500)
            failures.Add($"当前选择数量={count}");
        if (houseCount is < 1 or > 10)
            failures.Add($"HouseClass数量={houseCount}");
        if (queueCount is < 0 or > QueueCapacity)
            failures.Add($"事件队列数量={queueCount}");
        if (technoCount is < 0 or > 10000)
            failures.Add($"TechnoClass数量={technoCount}");
        if (currentHouse == 0)
            failures.Add("当前玩家阵营指针无效");
        if (failures.Count != 0)
            throw new InvalidOperationException(
                $"内存结构校验失败：{string.Join("，", failures)}。请进入对局后再启动；未进行任何写入。");
        _ = FindCurrentHouseIndex();
    }

    private static long DistanceSquared((int X, int Y) a, (int X, int Y) b)
    {
        var dx = (long)a.X - b.X;
        var dy = (long)a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private byte ReadByte(long address) => ReadBytes(address, 1)[0];
    private short ReadInt16(long address) => BitConverter.ToInt16(ReadBytes(address, 2));
    private int ReadInt32(long address) => BitConverter.ToInt32(ReadBytes(address, 4));
    private uint ReadUInt32(long address) => BitConverter.ToUInt32(ReadBytes(address, 4));
    private float ReadSingle(long address) => BitConverter.ToSingle(ReadBytes(address, 4));
    private double ReadDouble(long address) => BitConverter.ToDouble(ReadBytes(address, 8));

    private byte[] ReadBytes(long address, int length)
    {
        var data = new byte[length];
        if (!Native.ReadProcessMemory(handle, (nint)address, data, length, out var read) || read != length)
        {
            var error = Marshal.GetLastWin32Error();
            if (IsGameProcessUnavailable())
                throw new GameProcessExitedException();
            throw new Win32Exception(error, $"读取地址 0x{address:X} 失败");
        }
        return data;
    }

    private void WriteInt32(long address, int value) => WriteBytes(address, BitConverter.GetBytes(value));
    private void WriteSingle(long address, float value) => WriteBytes(address, BitConverter.GetBytes(value));

    private void WriteCode(long address, byte[] data)
    {
        if (!Native.VirtualProtectEx(handle, (nint)address, (nuint)data.Length,
                Native.PageExecuteReadWrite, out var previousProtection))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法修改地址 0x{address:X} 的页面保护");
        try
        {
            WriteBytes(address, data);
            if (!Native.FlushInstructionCache(handle, (nint)address, (nuint)data.Length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "刷新游戏指令缓存失败");
        }
        finally
        {
            Native.VirtualProtectEx(handle, (nint)address, (nuint)data.Length,
                previousProtection, out _);
        }
    }

    private void WriteBytes(long address, byte[] data)
    {
        if (!Native.WriteProcessMemory(handle, (nint)address, data, data.Length, out var written) || written != data.Length)
        {
            var error = Marshal.GetLastWin32Error();
            if (IsGameProcessUnavailable())
                throw new GameProcessExitedException();
            throw new Win32Exception(error, $"写入地址 0x{address:X} 失败");
        }
    }

    private static void CheckNtStatus(int status, string message)
    {
        if (status < 0)
            throw new InvalidOperationException($"{message}（NTSTATUS 0x{status:X8}）");
    }

    public void Dispose()
    {
        StopOverlay();
        try
        {
            if (!IsGameProcessUnavailable())
            {
                DisableCrateActionLines();
                DisableRevealMap();
                DisableInfiniteMoney();
                DisableOneHitKill();
                DisableMaximumPower();
                DisableInstantBuild();
                DisableBuildAnywhere();
            }
        }
        catch
        {
            // Closing the handle is still required if the game exits during cleanup.
        }
        finally
        {
            handle.Dispose();
        }
    }

    private sealed class UnitState
    {
        public UnitState(CapturedUnit unit) => Unit = unit;

        public CapturedUnit Unit { get; }
        public CrateSlot? ActiveCrate { get; set; }
        public DateTime LastCommandAt { get; set; } = DateTime.MinValue;
        public (int X, int Y) LastTargetObservedCell { get; set; }
        public DateTime LastTargetProgressAt { get; set; } = DateTime.MinValue;
        public Dictionary<CrateKey, DateTime> UnreachableCrates { get; } = [];
        public DateTime? InvalidSince { get; set; }
        public bool WaitingForCrate { get; set; }
        public (short X, short Y)? SafeCell { get; set; }
        public (int X, int Y) LastSafeObservedCell { get; set; }
        public DateTime LastSafeProgressAt { get; set; } = DateTime.MinValue;
    }

    private sealed record CapturedUnit(uint Pointer, int Id);

    private readonly record struct QueuedMission(
        CapturedUnit Unit,
        byte Mission,
        (short X, short Y)? Destination);
    private sealed record OneHitKillObjectState(
        int Id,
        double OriginalFirepowerMultiplier,
        double OriginalArmorMultiplier);
    private sealed record RevealMapCellState(
        byte[] Visibility,
        byte[] RevealFlags);
    private sealed record CrateSlot(int Index, short X, short Y);
    private readonly record struct CrateKey(int Index, short X, short Y);
}

internal sealed class GameProcessExitedException : Exception;

internal static class Native
{
    internal const uint ProcessVmRead = 0x0010;
    internal const uint ProcessVmWrite = 0x0020;
    internal const uint ProcessVmOperation = 0x0008;
    internal const uint ProcessQueryInformation = 0x0400;
    internal const uint ProcessSuspendResume = 0x0800;
    internal const uint PageExecuteReadWrite = 0x40;
    internal const uint MemCommit = 0x1000;
    internal const uint MemReserve = 0x2000;
    internal const uint MemRelease = 0x8000;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle process, nint address,
        [Out] byte[] buffer, int size, out int bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteProcessMemory(SafeProcessHandle process, nint address,
        byte[] buffer, int size, out int bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualProtectEx(SafeProcessHandle process, nint address,
        nuint size, uint newProtection, out uint oldProtection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FlushInstructionCache(SafeProcessHandle process, nint address, nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint VirtualAllocEx(SafeProcessHandle process, nint address,
        nuint size, uint allocationType, uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualFreeEx(SafeProcessHandle process, nint address,
        nuint size, uint freeType);

    [DllImport("ntdll.dll")]
    internal static extern int NtSuspendProcess(SafeProcessHandle process);

    [DllImport("ntdll.dll")]
    internal static extern int NtResumeProcess(SafeProcessHandle process);

}
