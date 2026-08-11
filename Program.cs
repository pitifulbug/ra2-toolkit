using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

Console.SetOut(new StringWriter());

try
{
    using var picker = new CratePicker();
    picker.Run();
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
    private const long CurrentFrame = 0xA8ED84;
    private const long MaxAhead = 0xA8B550;
    private const long CurrentObjects = 0xA8ECB8;
    private const long TechnoArray = 0xA8EC78;
    private const long FootArray = 0x8B3DC0;
    private const long BuildingTypeArray = 0xA83C68;
    private const long FactoryArray = 0xA83E30;
    private const long HouseArray = 0xA80228;
    private const long CurrentPlayer = 0xA83D4C;
    private const long OutList = 0xA802C8;
    private const long Map = 0x87F7E8;
    private const long ActiveFoundationBuffer = 0x8A041C;
    private const int MapBoundsOffset = 0x124;
    private const int CratesOffset = 0x158;
    private const int CurrentFoundationCenterOffset = 0x1174;
    private const int CurrentFoundationTopLeftOffset = 0x1178;
    private const int CurrentFoundationDataOffset = 0x117C;
    private const int CurrentFoundationProximityValidOffset = 0x1180;
    private const int CurrentFoundationTerrainValidOffset = 0x1181;
    private const int CurrentBuildingOffset = 0x11A4;
    private const int CurrentBuildingTypeOffset = 0x11A8;
    private const int CurrentBuildingOwnerOffset = 0x11AC;
    private const int DisplayModeFlagsOffset = 0x11B0;
    private const int CurrentSuperWeaponOffset = 0x11B8;
    private const int MapCellsItemsOffset = 0x13C;
    private const int CellPlacementFlagOffset = 0x12C;
    private const int ActivePlacementFlag = 2;
    private const int PersistentMarkerFlag = 4;
    private const int ObjectIsOnMapOffset = 0x74;
    private const int ObjectInLimboOffset = 0x81;
    private const int ObjectIsAliveOffset = 0x90;
    private const int TechnoArmorMultiplierOffset = 0x158;
    private const int TechnoFirepowerMultiplierOffset = 0x160;
    private const int TechnoOwnerOffset = 0x21C;
    private const int AbstractTypeIdOffset = 0x24;
    private const int BuildingTypeOffset = 0x520;
    private const int BuildingTypeFoundationDataOffset = 0xDFC;
    private const int BuildingTypeBuildCatOffset = 0xE08;
    private const int FactoryObjectOffset = 0x58;
    private const int FactoryOwnerOffset = 0x6C;
    private const int FactoryIsSuspendedOffset = 0x70;
    private const int HouseBalanceOffset = 0x30C;
    private const int HouseBaseSpawnCellOffset = 0x5490;
    private const int HouseBaseCenterOffset = 0x5494;
    private const double LegacyOverflowingFirepowerMultiplier = 99999.0;
    private const double OneHitKillFirepowerMultiplier = 1000.0;
    private const double ExtremeDefenseArmorMultiplier = 1000.0;
    private const int InfiniteMoneyFloor = 100_000_000;

    private readonly Process process;
    private readonly SafeProcessHandle handle;
    private readonly List<UnitState> units = [];
    private bool enabled;
    private bool f2WasDown;
    private bool f4WasDown;
    private bool f5WasDown;
    private bool f6WasDown;
    private bool f7WasDown;
    private bool f8WasDown;
    private bool f9WasDown;
    private bool f10WasDown;
    private bool leftMouseWasDown;
    private bool rightMouseWasDown;
    private readonly Dictionary<CrateKey, DateTime> recentlyCollected = [];
    private readonly List<AutoBuildTarget> autoBuildTargets = [];
    private readonly Queue<BuildPlan> buildPlans = [];
    private AutoBuildTarget? planningTarget;
    private bool planningPreviewActive;
    private int nextBuildPlanNumber = 1;
    private BuildPlan? activeBuildPlan;
    private bool autoBuildEnabled;
    private DateTime nextAutoBuildActionAt = DateTime.MinValue;
    private DateTime nextMarkerRefreshAt = DateTime.MinValue;
    private bool oneHitKillEnabled;
    private uint oneHitKillHouse;
    private readonly Dictionary<uint, OneHitKillObjectState> oneHitKillObjects = [];
    private DateTime nextOneHitKillRefreshAt = DateTime.MinValue;
    private bool infiniteMoneyEnabled;
    private DateTime nextInfiniteMoneyRefreshAt = DateTime.MinValue;
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
                            Native.ProcessVmWrite | Native.ProcessSuspendResume;
        handle = Native.OpenProcess(access, false, process.Id);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开游戏进程");

        ValidateLayout();
        LoadAutoBuildTargets();
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
        Console.WriteLine("规划建造：F7=光棱塔，F8=爱国者导弹；左键记录后保留安全待建标记，后台完成部署时再移除。\n");
        Console.WriteLine("版本：2026.08.11-r22（独立桌面控制中心）");
        Console.WriteLine("使用方法：");
        Console.WriteLine("-1. F2 开启/关闭我方无限资金（资金低于 1 亿时自动补满）。");
        Console.WriteLine("0. F4 开启/关闭我方安全秒杀与千倍防御（仅建议战役/遭遇战）。");
        Console.WriteLine("1. 在游戏中框选一个或多个己方可移动单位。");
        Console.WriteLine("2. 按 F5 开始；每个单位会分配不同箱子。");
        Console.WriteLine("3. F7/F8 立即规划光棱塔/爱国者；左键记录并忽略地形限制，右键取消。");
        Console.WriteLine("4. 可连续规划多个坐标；F9 清空建造队列，F10 退出。\n");
        Console.WriteLine("桌面控制中心已启动；可从任务栏切换，关闭窗口会安全退出工具。\n");
        Console.WriteLine("当前程序状态：已关闭，等待 F5。\n");

        while (!process.HasExited && !exitRequested)
        {
            PollHotkeys();
            ProcessOverlayCommands();
            if (infiniteMoneyEnabled)
                MaintainInfiniteMoney();
            if (oneHitKillEnabled)
                MaintainOneHitKill();
            if (enabled)
                Tick();
            if (autoBuildEnabled)
                TickAutoBuild();
            RefreshOverlay();
            Thread.Sleep(15);
        }
        if (process.HasExited)
            Console.WriteLine("游戏进程已经退出。");
    }

    private void PollHotkeys()
    {
        var f2Down = Native.GetAsyncKeyState(0x71) < 0;
        if (f2Down && !f2WasDown)
            ToggleInfiniteMoney();
        f2WasDown = f2Down;

        var f4Down = Native.GetAsyncKeyState(0x73) < 0;
        if (f4Down && !f4WasDown)
            ToggleOneHitKill();
        f4WasDown = f4Down;

        var f5Down = Native.GetAsyncKeyState(0x74) < 0;
        if (f5Down && !f5WasDown)
        {
            if (!enabled)
                Start();
        }
        f5WasDown = f5Down;

        var f6Down = Native.GetAsyncKeyState(0x75) < 0;
        if (f6Down && !f6WasDown && enabled)
            Pause();
        f6WasDown = f6Down;

        var f7Down = Native.GetAsyncKeyState(0x76) < 0;
        var f8Down = Native.GetAsyncKeyState(0x77) < 0;
        if (f7Down && !f7WasDown)
            StartPlanningPlacement("ATESLA", "光棱塔");
        if (f8Down && !f8WasDown)
            StartPlanningPlacement("NASAM", "爱国者导弹");
        f7WasDown = f7Down;
        f8WasDown = f8Down;

        PollPlanningMouse();

        var f9Down = Native.GetAsyncKeyState(0x78) < 0;
        if (f9Down && !f9WasDown)
            StopAutoBuild();
        f9WasDown = f9Down;

        var f10Down = Native.GetAsyncKeyState(0x79) < 0;
        if (f10Down && !f10WasDown)
            RequestExit();
        f10WasDown = f10Down;
    }

    private void Start()
    {
        var selectedUnits = CaptureSelectedUnits();
        if (selectedUnits.Count == 0)
        {
            Console.WriteLine("[未启动] 请先框选一个或多个己方可移动单位，再按 F5。");
            return;
        }
        units.Clear();
        units.AddRange(selectedUnits.Select(selected => new UnitState(selected)));
        enabled = true;
        recentlyCollected.Clear();
        Console.WriteLine($"[运行] 已锁定 {units.Count} 个单位：{string.Join(", ", units.Select(state => state.Unit.Id))}");
        Console.WriteLine("当前程序状态：已开启（F6 暂停）。");
    }

    private void Pause()
    {
        if (enabled)
            Console.WriteLine("[暂停] 已停止发送移动指令。当前程序状态：已暂停。");
        enabled = false;
        units.Clear();
        recentlyCollected.Clear();
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
                        var bufferedOutput = Console.Out.ToString() ?? string.Empty;
                        Console.SetOut(TextWriter.Synchronized(new DesktopLogWriter(panel.AppendLog)));
                        foreach (var line in bufferedOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                            Console.WriteLine(line);
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
            switch (command)
            {
                case OverlayCommand.ToggleInfiniteMoney:
                    ToggleInfiniteMoney();
                    break;
                case OverlayCommand.ToggleCombatBoost:
                    ToggleOneHitKill();
                    break;
                case OverlayCommand.ToggleCratePicker:
                    if (enabled)
                        Pause();
                    else
                        Start();
                    break;
                case OverlayCommand.PlanPrismTower:
                    StartPlanningPlacement("ATESLA", "光棱塔");
                    break;
                case OverlayCommand.PlanPatriotMissile:
                    StartPlanningPlacement("NASAM", "爱国者导弹");
                    break;
                case OverlayCommand.ClearBuildQueue:
                    StopAutoBuild();
                    break;
                case OverlayCommand.ExitProgram:
                    RequestExit();
                    break;
            }
        }
    }

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
            infiniteMoneyEnabled,
            oneHitKillEnabled,
            enabled,
            planningPreviewActive,
            buildPlans.Count + (activeBuildPlan is null ? 0 : 1)));
    }

    private void StopOverlay()
    {
        var panel = overlay;
        if (panel is not null)
            panel.RequestClose();
        if (overlayThread is { IsAlive: true } thread && thread != Thread.CurrentThread)
            thread.Join(2000);
    }

    private void RequestExit()
    {
        if (exitRequested)
            return;
        DisableInfiniteMoney();
        DisableOneHitKill();
        Pause();
        CancelPlanningPreview();
        StopAutoBuild();
        exitRequested = true;
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
        Console.WriteLine($"[无限资金已开启] 我方资金下限为 {InfiniteMoneyFloor:N0}；再次按 F2 关闭。");
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
        Console.WriteLine($"[我方攻防强化已开启] 已修改 {affected} 个现有单位/建筑的实际火力与防御倍率；新单位会自动加入。再次按 F4 恢复。");
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
            if (!process.HasExited)
                restored = RestoreOneHitKillObjects();
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
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

    private void LoadAutoBuildTargets()
    {
        foreach (var (id, name) in new[]
                 {
                     ("ATESLA", "光棱塔"),
                     ("NASAM", "爱国者导弹")
                 })
        {
            var found = FindBuildingType(id);
            if (found is not { } buildingType)
            {
                Console.WriteLine($"[自动建造] 当前规则中没有 {name}（{id}），已跳过。");
                continue;
            }

            autoBuildTargets.Add(new AutoBuildTarget(
                id, name, buildingType.Index, buildingType.Pointer,
                ReadInt32(buildingType.Pointer + BuildingTypeBuildCatOffset)));
        }
    }

    private (uint Pointer, int Index)? FindBuildingType(string id)
    {
        var items = ReadUInt32(BuildingTypeArray + 4);
        var count = ReadInt32(BuildingTypeArray + 16);
        if (items == 0 || count is < 1 or > 2000)
            return null;

        for (var index = 0; index < count; index++)
        {
            var pointer = ReadUInt32(items + index * 4L);
            if (pointer == 0)
                continue;
            var rawId = ReadBytes(pointer + AbstractTypeIdOffset, 0x18);
            var terminator = Array.IndexOf(rawId, (byte)0);
            var actualId = Encoding.ASCII.GetString(rawId, 0, terminator < 0 ? rawId.Length : terminator);
            if (actualId.Equals(id, StringComparison.OrdinalIgnoreCase))
                return (pointer, index);
        }

        return null;
    }

    private void StartPlanningPlacement(string id, string name)
    {
        var target = autoBuildTargets.FirstOrDefault(candidate => candidate.Id == id);
        if (target is null)
        {
            Console.WriteLine($"[未接受] 当前规则中找不到 {name}（{id}）。");
            return;
        }

        if (planningPreviewActive)
            CancelPlanningPreview();
        if (ReadUInt32(Map + CurrentBuildingTypeOffset) != 0 || IsConflictingTacticalModeActive())
        {
            Console.WriteLine("[未接受] 游戏当前已有建筑或其他鼠标模式，请先完成或右键取消。");
            return;
        }

        planningTarget = target;
        try
        {
            if (!TryActivatePlanningPreview(target))
            {
                planningTarget = null;
                Console.WriteLine("[未接受] 游戏当前的放置状态正被其他操作占用。");
                return;
            }
        }
        catch (Exception error) when (error is InvalidOperationException or OverflowException)
        {
            CancelPlanningPreview();
            Console.WriteLine($"[安全停止] {error.Message}");
            return;
        }

        planningPreviewActive = true;
        Console.WriteLine($"[立即规划] {target.Name}：左键加入后台队列；地形限制已忽略，超出建造范围仍会拒绝；右键取消。");
    }

    private void PollPlanningMouse()
    {
        var leftDown = Native.GetAsyncKeyState(0x01) < 0;
        var rightDown = Native.GetAsyncKeyState(0x02) < 0;
        if (planningPreviewActive)
        {
            if (rightDown && !rightMouseWasDown)
            {
                CancelPlanningPreview();
                Console.WriteLine("[规划取消] 未记录坐标。");
            }
            else if (leftDown && !leftMouseWasDown)
            {
                CapturePlannedPlacement();
            }
        }
        leftMouseWasDown = leftDown;
        rightMouseWasDown = rightDown;
    }

    private void CapturePlannedPlacement()
    {
        var target = planningTarget;
        if (target is null || ReadUInt32(Map + CurrentBuildingTypeOffset) != target.TypePointer)
        {
            CancelPlanningPreview();
            return;
        }
        if (ReadByte(Map + CurrentFoundationProximityValidOffset) == 0)
        {
            Console.WriteLine("[位置无效] 当前坐标超出允许建造范围，没有加入队列。");
            return;
        }
        var terrainRestrictionIgnored = ReadByte(Map + CurrentFoundationTerrainValidOffset) == 0;

        var centerX = ReadInt16(Map + CurrentFoundationCenterOffset);
        var centerY = ReadInt16(Map + CurrentFoundationCenterOffset + 2);
        var offsetX = ReadInt16(Map + CurrentFoundationTopLeftOffset);
        var offsetY = ReadInt16(Map + CurrentFoundationTopLeftOffset + 2);
        var cell = (X: checked((short)(centerX + offsetX)), Y: checked((short)(centerY + offsetY)));
        if (!IsCellWithinMapBounds(cell))
        {
            Console.WriteLine("[位置无效] 当前坐标超出地图范围，没有加入队列。");
            return;
        }
        var markerCells = ReadCurrentFoundationCells();
        if (markerCells.Count == 0)
        {
            Console.WriteLine("[位置无效] 无法取得当前地基单元格，没有加入队列。");
            return;
        }
        var markerSet = markerCells.ToHashSet();
        if (EnumerateBuildPlans().Any(plan => plan.MarkerCells.Any(markerSet.Contains)))
        {
            Console.WriteLine($"[位置重复] ({cell.X},{cell.Y}) 与已有绿色待建标记重叠，请选择其他位置。");
            return;
        }
        if (buildPlans.Count >= 512)
        {
            Console.WriteLine("[队列已满] 最多保留 512 个待建坐标，请等待后台完成一部分。");
            return;
        }

        var plan = new BuildPlan(nextBuildPlanNumber++, target, cell, markerCells);
        buildPlans.Enqueue(plan);
        autoBuildEnabled = true;
        nextAutoBuildActionAt = DateTime.MinValue;
        CancelPlanningPreview(preserveCurrentMarker: true);
        Console.WriteLine($"[已加入 #{plan.Number}] {target.Name} → ({cell.X},{cell.Y})；后台待处理 {buildPlans.Count + (activeBuildPlan is null ? 0 : 1)} 个。");
        if (terrainRestrictionIgnored)
        {
            Console.WriteLine($"[地形限制已忽略 #{plan.Number}] 已记录该坐标；若游戏引擎最终拒绝部署，将自动取消成品并继续队列。");
        }
    }

    private void StopAutoBuild()
    {
        CancelPlanningPreview();
        var discarded = buildPlans.Count + (activeBuildPlan is null ? 0 : 1);
        ClearPlanMarkers(EnumerateBuildPlans().ToArray());
        buildPlans.Clear();
        activeBuildPlan = null;
        autoBuildEnabled = false;
        if (discarded > 0)
            Console.WriteLine($"[后台建造停止] 已清除 {discarded} 个任务；已经提交给游戏的生产或放置事件不会撤回。");
    }

    private void TickAutoBuild()
    {
        var now = DateTime.UtcNow;
        RefreshPlanMarkers(now);
        if (now < nextAutoBuildActionAt)
            return;
        if (activeBuildPlan is null)
        {
            if (!buildPlans.TryDequeue(out activeBuildPlan))
            {
                autoBuildEnabled = false;
                return;
            }
            Console.WriteLine($"[后台开始 #{activeBuildPlan.Number}] {activeBuildPlan.Target.Name} → ({activeBuildPlan.Cell.X},{activeBuildPlan.Cell.Y})。");
        }

        var plan = activeBuildPlan!;
        var state = ReadDefenseFactoryState(plan.Target);
        switch (plan.Stage)
        {
            case BuildPlanStage.WaitingToProduce:
                if (state.HasRequestedProduct)
                {
                    plan.Stage = BuildPlanStage.WaitingForProduct;
                    plan.StageStartedAt = now;
                    nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                    return;
                }
                if (state.HasSameCategoryProduct)
                {
                    nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(500);
                    return;
                }
                if (plan.Target.UnavailableUntil > now)
                {
                    nextAutoBuildActionAt = plan.Target.UnavailableUntil;
                    return;
                }
                QueueProduction(plan.Target);
                plan.ProductionAttempts++;
                plan.Stage = BuildPlanStage.WaitingForProduct;
                plan.StageStartedAt = now;
                Console.WriteLine($"[后台 #{plan.Number}] 已请求生产 {plan.Target.Name}。");
                nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                return;

            case BuildPlanStage.WaitingForProduct:
                if (state.HasRequestedProduct)
                {
                    if (!state.IsReady)
                    {
                        nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                        return;
                    }
                    RemovePlanMarker(plan);
                    QueuePlacement(plan.Target, plan.Cell);
                    plan.Stage = BuildPlanStage.WaitingForPlacementResult;
                    plan.StageStartedAt = now;
                    Console.WriteLine($"[后台 #{plan.Number}] 成品完成，已提交预定坐标 ({plan.Cell.X},{plan.Cell.Y})。");
                    nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                    return;
                }
                if (now - plan.StageStartedAt < TimeSpan.FromSeconds(3))
                {
                    nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                    return;
                }
                if (plan.ProductionAttempts >= 3)
                {
                    CompleteActiveBuildPlan(false, "连续三次无法开始生产");
                    return;
                }
                plan.Target.UnavailableUntil = now + TimeSpan.FromSeconds(5);
                plan.Stage = BuildPlanStage.WaitingToProduce;
                nextAutoBuildActionAt = plan.Target.UnavailableUntil;
                return;

            case BuildPlanStage.WaitingForPlacementResult:
                if (!state.HasRequestedProduct)
                {
                    CompleteActiveBuildPlan(true, "已放置");
                    return;
                }
                if (now - plan.StageStartedAt < TimeSpan.FromSeconds(3))
                {
                    nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                    return;
                }
                QueueAbandon(plan.Target);
                plan.AbandonAttempts = 1;
                plan.Stage = BuildPlanStage.WaitingForAbandon;
                plan.StageStartedAt = now;
                Console.WriteLine($"[后台 #{plan.Number}] 预定坐标已经失效，已安全取消该成品，避免队列卡住。");
                nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                return;

            case BuildPlanStage.WaitingForAbandon:
                if (!state.HasRequestedProduct)
                {
                    CompleteActiveBuildPlan(false, "部署时坐标失效");
                    return;
                }
                if (now - plan.StageStartedAt < TimeSpan.FromSeconds(3))
                {
                    nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                    return;
                }
                if (plan.AbandonAttempts >= 3)
                {
                    StopStuckBuildQueue(plan);
                    return;
                }
                QueueAbandon(plan.Target);
                plan.AbandonAttempts++;
                plan.StageStartedAt = now;
                nextAutoBuildActionAt = now + TimeSpan.FromMilliseconds(100);
                return;
        }
    }

    private void CompleteActiveBuildPlan(bool success, string result)
    {
        var plan = activeBuildPlan!;
        RemovePlanMarker(plan);
        Console.WriteLine($"[后台 {(success ? "完成" : "跳过")} #{plan.Number}] {plan.Target.Name} ({plan.Cell.X},{plan.Cell.Y})：{result}。");
        activeBuildPlan = null;
        autoBuildEnabled = buildPlans.Count > 0;
        nextAutoBuildActionAt = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
    }

    private void StopStuckBuildQueue(BuildPlan plan)
    {
        var discarded = buildPlans.Count;
        ClearPlanMarkers(EnumerateBuildPlans().ToArray());
        buildPlans.Clear();
        activeBuildPlan = null;
        autoBuildEnabled = false;
        Console.WriteLine($"[安全停止] #{plan.Number} 的成品连续三次无法取消，后台已停止并清除后续 {discarded} 个任务；请在建造栏手动处理该成品。");
    }

    private bool TryActivatePlanningPreview(AutoBuildTarget target)
    {
        if (ReadUInt32(Map + CurrentBuildingOffset) != 0 ||
            ReadUInt32(Map + CurrentBuildingTypeOffset) != 0 ||
            ReadUInt32(Map + CurrentFoundationDataOffset) != 0 ||
            IsConflictingTacticalModeActive())
            return false;

        var layout = ReadFoundationLayout(target);
        var emptyCell = ReadInt32(0x8A03F8);
        var topLeft = PackCell(layout.TopLeftX, layout.TopLeftY);
        var ownerIndex = FindCurrentHouseIndex();
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            if (ReadUInt32(Map + CurrentBuildingOffset) != 0 ||
                ReadUInt32(Map + CurrentBuildingTypeOffset) != 0 ||
                ReadUInt32(Map + CurrentFoundationDataOffset) != 0 ||
                IsConflictingTacticalModeActive())
                return false;

            WriteBytes(ActiveFoundationBuffer, layout.Data);
            WriteInt32(Map + CurrentFoundationCenterOffset, emptyCell);
            WriteInt32(Map + CurrentFoundationTopLeftOffset, unchecked((int)topLeft));
            WriteBytes(Map + CurrentFoundationProximityValidOffset, [0]);
            WriteBytes(Map + CurrentFoundationTerrainValidOffset, [0]);
            WriteInt32(Map + CurrentFoundationDataOffset, checked((int)ActiveFoundationBuffer));
            WriteInt32(Map + CurrentBuildingOwnerOffset, ownerIndex);
            WriteInt32(Map + CurrentBuildingOffset, 0);
            WriteInt32(Map + CurrentBuildingTypeOffset, unchecked((int)target.TypePointer));
            return true;
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private FoundationLayout ReadFoundationLayout(AutoBuildTarget target)
    {
        var source = ReadUInt32(target.TypePointer + BuildingTypeFoundationDataOffset);
        if (source == 0)
            throw new InvalidOperationException($"{target.Name} 没有有效地基数据，已拒绝显示预览。");
        var data = ReadBytes(source, 120 * 4);
        var cellCount = 0;
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        for (var index = 0; index < 120; index++)
        {
            var x = BitConverter.ToInt16(data, index * 4);
            var y = BitConverter.ToInt16(data, index * 4 + 2);
            if (x == 0x7FFF && y == 0x7FFF)
            {
                if (cellCount == 0)
                    break;
                var width = maxX - minX + 1;
                var height = maxY - minY + 1;
                return new FoundationLayout(
                    data, checked((short)(-(width - 1) / 2)), checked((short)(-(height - 1) / 2)));
            }
            if (x is < -64 or > 64 || y is < -64 or > 64)
                break;
            cellCount++;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
        throw new InvalidOperationException($"{target.Name} 的地基数据异常或缺少结束标记，已拒绝写入。");
    }

    private void CancelPlanningPreview(bool preserveCurrentMarker = false)
    {
        var target = planningTarget;
        if (target is null)
        {
            planningPreviewActive = false;
            return;
        }

        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            var currentType = ReadUInt32(Map + CurrentBuildingTypeOffset);
            if (ReadUInt32(Map + CurrentBuildingOffset) == 0 &&
                ReadUInt32(Map + CurrentFoundationDataOffset) == ActiveFoundationBuffer &&
                (currentType == target.TypePointer || currentType == 0))
            {
                if (preserveCurrentMarker)
                    ConvertCurrentFoundationToPersistentMarker();
                else
                    UnmarkCurrentFoundation();
                var emptyCell = ReadInt32(0x8A03F8);
                WriteInt32(Map + CurrentBuildingTypeOffset, 0);
                WriteInt32(Map + CurrentBuildingOwnerOffset, -1);
                WriteInt32(Map + CurrentFoundationDataOffset, 0);
                WriteInt32(Map + CurrentFoundationCenterOffset, emptyCell);
                WriteInt32(Map + CurrentFoundationTopLeftOffset, emptyCell);
                WriteBytes(Map + CurrentFoundationProximityValidOffset, [0]);
                WriteBytes(Map + CurrentFoundationTerrainValidOffset, [0]);
            }
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
        planningTarget = null;
        planningPreviewActive = false;
    }

    private void UnmarkCurrentFoundation()
    {
        var center = ReadInt32(Map + CurrentFoundationCenterOffset);
        if (center == ReadInt32(0x8A03F8))
            return;
        var centerX = (short)center;
        var centerY = (short)(center >> 16);
        var topLeft = ReadInt32(Map + CurrentFoundationTopLeftOffset);
        var baseX = centerX + (short)topLeft;
        var baseY = centerY + (short)(topLeft >> 16);
        var cellsItems = ReadUInt32(Map + MapCellsItemsOffset);
        if (cellsItems == 0)
            return;

        var foundation = ReadBytes(ActiveFoundationBuffer, 120 * 4);
        for (var index = 0; index < 120; index++)
        {
            var offsetX = BitConverter.ToInt16(foundation, index * 4);
            var offsetY = BitConverter.ToInt16(foundation, index * 4 + 2);
            if (offsetX == 0x7FFF && offsetY == 0x7FFF)
                break;
            var x = baseX + offsetX;
            var y = baseY + offsetY;
            if (x is < 0 or >= 512 || y is < 0 or >= 512)
                continue;
            var cell = ReadUInt32(cellsItems + (y * 512L + x) * 4);
            if (cell != 0)
                WriteInt32(cell + CellPlacementFlagOffset,
                    ReadInt32(cell + CellPlacementFlagOffset) & ~ActivePlacementFlag);
        }
    }

    private void ConvertCurrentFoundationToPersistentMarker()
    {
        var cellsItems = ReadUInt32(Map + MapCellsItemsOffset);
        if (cellsItems == 0)
            throw new InvalidOperationException("无法取得地图单元格，已拒绝保留待建标记。");
        foreach (var cellLocation in ReadCurrentFoundationCells())
        {
            var cell = ReadUInt32(cellsItems + (cellLocation.Y * 512L + cellLocation.X) * 4);
            if (cell == 0)
                continue;
            var flags = ReadInt32(cell + CellPlacementFlagOffset);
            var updated = (flags & ~ActivePlacementFlag) | PersistentMarkerFlag;
            if (updated != flags)
                WriteInt32(cell + CellPlacementFlagOffset, updated);
        }
    }

    private IReadOnlyList<(short X, short Y)> ReadCurrentFoundationCells()
    {
        var foundation = ReadUInt32(Map + CurrentFoundationDataOffset);
        var center = ReadInt32(Map + CurrentFoundationCenterOffset);
        if (foundation == 0 || center == ReadInt32(0x8A03F8))
            return [];

        var topLeft = ReadInt32(Map + CurrentFoundationTopLeftOffset);
        var baseX = (short)center + (short)topLeft;
        var baseY = (short)(center >> 16) + (short)(topLeft >> 16);
        var data = ReadBytes(foundation, 120 * 4);
        var cells = new List<(short X, short Y)>();
        for (var index = 0; index < 120; index++)
        {
            var offsetX = BitConverter.ToInt16(data, index * 4);
            var offsetY = BitConverter.ToInt16(data, index * 4 + 2);
            if (offsetX == 0x7FFF && offsetY == 0x7FFF)
                return cells;
            var x = baseX + offsetX;
            var y = baseY + offsetY;
            if (x is < 0 or >= 512 || y is < 0 or >= 512)
                return [];
            cells.Add((checked((short)x), checked((short)y)));
        }
        return [];
    }

    private IEnumerable<BuildPlan> EnumerateBuildPlans()
    {
        if (activeBuildPlan is not null)
            yield return activeBuildPlan;
        foreach (var plan in buildPlans)
            yield return plan;
    }

    private void RefreshPlanMarkers(DateTime now)
    {
        if (now < nextMarkerRefreshAt)
            return;
        nextMarkerRefreshAt = now + TimeSpan.FromMilliseconds(250);
        var plans = EnumerateBuildPlans().Where(plan => plan.MarkerVisible).ToArray();
        if (plans.Length == 0 || !PlanMarkerCellsNeedRefresh(plans))
            return;

        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            SetPlanMarkerCells(plans, true);
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private bool PlanMarkerCellsNeedRefresh(IEnumerable<BuildPlan> plans)
    {
        var cellsItems = ReadUInt32(Map + MapCellsItemsOffset);
        if (cellsItems == 0)
            return false;
        foreach (var cellLocation in plans.SelectMany(plan => plan.MarkerCells).Distinct())
        {
            var cell = ReadUInt32(cellsItems + (cellLocation.Y * 512L + cellLocation.X) * 4);
            if (cell != 0 && (ReadInt32(cell + CellPlacementFlagOffset) & PersistentMarkerFlag) == 0)
                return true;
        }
        return false;
    }

    private void RemovePlanMarker(BuildPlan plan)
    {
        if (!plan.MarkerVisible)
            return;
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            SetPlanMarkerCells([plan], false);
            plan.MarkerVisible = false;
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private void ClearPlanMarkers(IReadOnlyCollection<BuildPlan> plans)
    {
        var visible = plans.Where(plan => plan.MarkerVisible).ToArray();
        if (visible.Length == 0)
            return;

        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            SetPlanMarkerCells(visible, false);
            foreach (var plan in visible)
                plan.MarkerVisible = false;
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private void SetPlanMarkerCells(IEnumerable<BuildPlan> plans, bool marked)
    {
        var cellsItems = ReadUInt32(Map + MapCellsItemsOffset);
        if (cellsItems == 0)
            return;
        foreach (var cellLocation in plans.SelectMany(plan => plan.MarkerCells).Distinct())
        {
            var cell = ReadUInt32(cellsItems + (cellLocation.Y * 512L + cellLocation.X) * 4);
            if (cell == 0)
                continue;
            var flags = ReadInt32(cell + CellPlacementFlagOffset);
            var updated = marked ? flags | PersistentMarkerFlag : flags & ~PersistentMarkerFlag;
            if (updated != flags)
                WriteInt32(cell + CellPlacementFlagOffset, updated);
        }
    }

    private bool IsCellWithinMapBounds((short X, short Y) cell)
    {
        var left = ReadInt32(Map + MapBoundsOffset);
        var top = ReadInt32(Map + MapBoundsOffset + 4);
        var right = ReadInt32(Map + MapBoundsOffset + 8);
        var bottom = ReadInt32(Map + MapBoundsOffset + 12);
        return cell.X >= left && cell.X <= right && cell.Y >= top && cell.Y <= bottom;
    }

    private static uint PackCell(short x, short y) =>
        (uint)(ushort)x | (uint)(ushort)y << 16;

    private bool IsConflictingTacticalModeActive()
    {
        var flags = ReadBytes(Map + DisplayModeFlagsOffset, 5);
        return flags.Any(value => value != 0) || ReadInt32(Map + CurrentSuperWeaponOffset) != -1;
    }

    private FactoryState ReadDefenseFactoryState(AutoBuildTarget target)
    {
        var items = ReadUInt32(FactoryArray + 4);
        var count = ReadInt32(FactoryArray + 16);
        if (items == 0 || count is < 0 or > 1000)
            return default;

        var currentPlayer = ReadUInt32(CurrentPlayer);
        var hasSameCategoryProduct = false;
        for (var index = 0; index < count; index++)
        {
            var factory = ReadUInt32(items + index * 4L);
            if (factory == 0 || ReadUInt32(factory + FactoryOwnerOffset) != currentPlayer)
                continue;
            var product = ReadUInt32(factory + FactoryObjectOffset);
            if (product == 0)
                continue;
            var productType = ReadUInt32(product + BuildingTypeOffset);
            if (productType == target.TypePointer)
                return new FactoryState(
                    true, true, ReadByte(factory + FactoryIsSuspendedOffset) != 0);
            if (productType != 0 && VectorContains(BuildingTypeArray, productType) &&
                ReadInt32(productType + BuildingTypeBuildCatOffset) == target.BuildCategory)
                hasSameCategoryProduct = true;
        }

        return new FactoryState(false, hasSameCategoryProduct, false);
    }

    private void Tick()
    {
        var now = DateTime.UtcNow;
        for (var index = units.Count - 1; index >= 0; index--)
        {
            var state = units[index];
            if (IsCapturedUnitValid(state.Unit))
            {
                if (state.InvalidSince is not null)
                    Console.WriteLine($"[恢复] 单位 {state.Unit.Id} 已恢复有效状态，重新加入调度。");
                state.InvalidSince = null;
                continue;
            }

            if (state.InvalidSince is null)
            {
                state.InvalidSince = now;
                state.ActiveCrate = null;
                state.LastCommandAt = DateTime.MinValue;
                ResetTargetProgress(state);
                ResetWaitingState(state);
                Console.WriteLine($"[等待确认] 单位 {state.Unit.Id} 暂时无效，已释放箱子认领。");
                continue;
            }

            if (now - state.InvalidSince.Value < TimeSpan.FromSeconds(2))
                continue;

            Console.WriteLine($"[移除] 单位 {state.Unit.Id} 连续 2 秒无效，判定为阵亡或已离场。");
            units.RemoveAt(index);
        }

        if (units.Count == 0)
        {
            enabled = false;
            recentlyCollected.Clear();
            Console.WriteLine("[停止] 全部锁定单位均已失效，请重新框选后按 F5。");
            return;
        }

        var usableUnits = units.Where(state => state.InvalidSince is null).ToArray();
        if (usableUnits.Length == 0)
            return;

        var crates = ReadActiveCrates();
        foreach (var expired in recentlyCollected
                     .Where(entry => entry.Value <= now)
                     .Select(entry => entry.Key)
                     .ToArray())
            recentlyCollected.Remove(expired);

        var claimed = new HashSet<CrateKey>();
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
                recentlyCollected[new CrateKey(reached.Index, reached.X, reached.Y)] =
                    now + TimeSpan.FromMilliseconds(750);
                state.ActiveCrate = null;
                state.LastCommandAt = DateTime.MinValue;
                ResetTargetProgress(state);
            }

            if (state.ActiveCrate is { } previous && !crates.Contains(previous))
            {
                state.ActiveCrate = null;
                ResetTargetProgress(state);
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
                    Console.WriteLine(
                        $"[改派] 单位 {state.Unit.Id} 连续 3 秒没有移动，8 秒内跳过箱子 #{target.Index} ({target.X},{target.Y})。");
                }
            }

            if (state.ActiveCrate is { } active)
                claimed.Add(new CrateKey(active.Index, active.X, active.Y));
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
                    Console.WriteLine($"[指令] 单位 {state.Unit.Id} → 箱子 #{nearest.Index} ({nearest.X},{nearest.Y})");
                    continue;
                }

                WaitAtSafePlace(state, now);
                continue;
            }

            ResetWaitingState(state);
            if (now - state.LastCommandAt >= TimeSpan.FromMilliseconds(750))
            {
                var target = state.ActiveCrate!;
                QueueMove(state.Unit, target.X, target.Y);
                state.LastCommandAt = now;
            }
        }

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
                Console.WriteLine($"[待命] 单位 {state.Unit.Id} → 基地 ({target.X},{target.Y})");
            }
            else
            {
                QueueGuard(state.Unit);
                state.SafeCell = null;
                Console.WriteLine($"[待命] 单位 {state.Unit.Id} 原地警戒。");
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
            Console.WriteLine($"[待命] 单位 {state.Unit.Id} 已到达基地，原地警戒。");
            return;
        }

        if (now - state.LastSafeProgressAt >= TimeSpan.FromSeconds(10))
        {
            QueueGuard(state.Unit);
            state.SafeCell = null;
            state.LastCommandAt = now;
            Console.WriteLine($"[待命] 单位 {state.Unit.Id} 无法到达基地，改为原地警戒。");
            return;
        }

        QueueMove(state.Unit, destination.X, destination.Y);
        state.LastCommandAt = now;
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

    private void QueueProduction(AutoBuildTarget target)
    {
        var eventData = CreateEvent(0x0E); // EventType::Produce
        BitConverter.GetBytes(7).CopyTo(eventData, 7); // AbstractType::BuildingType
        BitConverter.GetBytes(target.HeapIndex).CopyTo(eventData, 11);
        BitConverter.GetBytes(0).CopyTo(eventData, 15); // IsNaval
        EnqueueEvent(eventData);
    }

    private void QueuePlacement(AutoBuildTarget target, (short X, short Y) cell)
    {
        var eventData = CreateEvent(0x0B); // EventType::Place
        BitConverter.GetBytes(7).CopyTo(eventData, 7); // AbstractType::BuildingType
        BitConverter.GetBytes(target.HeapIndex).CopyTo(eventData, 11);
        BitConverter.GetBytes(0).CopyTo(eventData, 15); // IsNaval
        BitConverter.GetBytes(cell.X).CopyTo(eventData, 19);
        BitConverter.GetBytes(cell.Y).CopyTo(eventData, 21);
        EnqueueEvent(eventData);
    }

    private void QueueAbandon(AutoBuildTarget target)
    {
        var eventData = CreateEvent(0x10); // EventType::Abandon
        BitConverter.GetBytes(7).CopyTo(eventData, 7); // AbstractType::BuildingType
        BitConverter.GetBytes(target.HeapIndex).CopyTo(eventData, 11);
        BitConverter.GetBytes(0).CopyTo(eventData, 15); // IsNaval
        EnqueueEvent(eventData);
    }

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
        var eventData = CreateEvent(0x04); // EventType::MegaMission
        BitConverter.GetBytes(captured.Id).CopyTo(eventData, 7);
        eventData[11] = 52; // AbstractType::Abstract
        eventData[12] = mission;
        if (destination is { } cell)
        {
            BitConverter.GetBytes(cell.X + 1000 * cell.Y).CopyTo(eventData, 19);
            eventData[23] = 11; // AbstractType::Cell
        }

        EnqueueEvent(eventData);
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
        var buildingTypeCount = ReadInt32(BuildingTypeArray + 16);
        var factoryCount = ReadInt32(FactoryArray + 16);
        var technoCount = ReadInt32(TechnoArray + 16);
        var currentHouse = ReadUInt32(CurrentPlayer);
        var failures = new List<string>();
        if (count is < 0 or > 500)
            failures.Add($"当前选择数量={count}");
        if (houseCount is < 1 or > 10)
            failures.Add($"HouseClass数量={houseCount}");
        if (queueCount is < 0 or > QueueCapacity)
            failures.Add($"事件队列数量={queueCount}");
        if (buildingTypeCount is < 1 or > 2000)
            failures.Add($"BuildingTypeClass数量={buildingTypeCount}");
        if (factoryCount is < 0 or > 1000)
            failures.Add($"FactoryClass数量={factoryCount}");
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
    private double ReadDouble(long address) => BitConverter.ToDouble(ReadBytes(address, 8));

    private byte[] ReadBytes(long address, int length)
    {
        var data = new byte[length];
        if (!Native.ReadProcessMemory(handle, (nint)address, data, length, out var read) || read != length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"读取地址 0x{address:X} 失败");
        return data;
    }

    private void WriteInt32(long address, int value) => WriteBytes(address, BitConverter.GetBytes(value));

    private void WriteBytes(long address, byte[] data)
    {
        if (!Native.WriteProcessMemory(handle, (nint)address, data, data.Length, out var written) || written != data.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"写入地址 0x{address:X} 失败");
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
            if (!process.HasExited)
            {
                DisableInfiniteMoney();
                DisableOneHitKill();
                CancelPlanningPreview();
                ClearPlanMarkers(EnumerateBuildPlans().ToArray());
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
    private sealed record OneHitKillObjectState(
        int Id,
        double OriginalFirepowerMultiplier,
        double OriginalArmorMultiplier);
    private sealed record CrateSlot(int Index, short X, short Y);
    private readonly record struct CrateKey(int Index, short X, short Y);
    private readonly record struct FactoryState(
        bool HasRequestedProduct,
        bool HasSameCategoryProduct,
        bool IsReady);

    private sealed record FoundationLayout(
        byte[] Data,
        short TopLeftX,
        short TopLeftY);

    private enum BuildPlanStage
    {
        WaitingToProduce,
        WaitingForProduct,
        WaitingForPlacementResult,
        WaitingForAbandon
    }

    private sealed class BuildPlan(
        int number,
        AutoBuildTarget target,
        (short X, short Y) cell,
        IReadOnlyList<(short X, short Y)> markerCells)
    {
        public int Number { get; } = number;
        public AutoBuildTarget Target { get; } = target;
        public (short X, short Y) Cell { get; } = cell;
        public IReadOnlyList<(short X, short Y)> MarkerCells { get; } = markerCells;
        public bool MarkerVisible { get; set; } = true;
        public BuildPlanStage Stage { get; set; } = BuildPlanStage.WaitingToProduce;
        public DateTime StageStartedAt { get; set; } = DateTime.MinValue;
        public int ProductionAttempts { get; set; }
        public int AbandonAttempts { get; set; }
    }

    private sealed class AutoBuildTarget(
        string id,
        string name,
        int heapIndex,
        uint typePointer,
        int buildCategory)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public int HeapIndex { get; } = heapIndex;
        public uint TypePointer { get; } = typePointer;
        public int BuildCategory { get; } = buildCategory;
        public DateTime UnavailableUntil { get; set; } = DateTime.MinValue;
    }
}

internal static class Native
{
    internal const uint ProcessVmRead = 0x0010;
    internal const uint ProcessVmWrite = 0x0020;
    internal const uint ProcessQueryInformation = 0x0400;
    internal const uint ProcessSuspendResume = 0x0800;

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

    [DllImport("ntdll.dll")]
    internal static extern int NtSuspendProcess(SafeProcessHandle process);

    [DllImport("ntdll.dll")]
    internal static extern int NtResumeProcess(SafeProcessHandle process);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

}
