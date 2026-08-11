using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "ra2-toolkit";

try
{
    using var picker = new CratePicker();
    picker.Run();
}
catch (Exception error)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n启动失败：{error.Message}");
    Console.ResetColor();
    Console.WriteLine("按 Enter 退出。");
    Console.ReadLine();
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
    private const long FootArray = 0x8B3DC0;
    private const long HouseArray = 0xA80228;
    private const long CurrentPlayer = 0xA83D4C;
    private const long OutList = 0xA802C8;
    private const long Map = 0x87F7E8;
    private const int MapBoundsOffset = 0x124;
    private const int CratesOffset = 0x158;
    private const int ObjectIsOnMapOffset = 0x74;
    private const int ObjectInLimboOffset = 0x81;
    private const int ObjectIsAliveOffset = 0x90;
    private const int TechnoOwnerOffset = 0x21C;
    private const int HouseBaseSpawnCellOffset = 0x5490;
    private const int HouseBaseCenterOffset = 0x5494;

    private readonly Process process;
    private readonly SafeProcessHandle handle;
    private readonly List<UnitState> units = [];
    private bool enabled;
    private bool f5WasDown;
    private bool f6WasDown;
    private bool f10WasDown;
    private readonly Dictionary<CrateKey, DateTime> recentlyCollected = [];

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
        Console.WriteLine("版本：2026.08.11-r7（多单位状态防误判）");
        Console.WriteLine("使用方法：");
        Console.WriteLine("1. 在游戏中框选一个或多个己方可移动单位。");
        Console.WriteLine("2. 按 F5 开始；每个单位会分配不同箱子。");
        Console.WriteLine("3. 按 F6 暂停，按 F10 退出。\n");
        Console.WriteLine("当前程序状态：已关闭，等待 F5。\n");

        while (!process.HasExited)
        {
            PollHotkeys();
            if (enabled)
                Tick();
            Thread.Sleep(15);
        }
        Console.WriteLine("游戏进程已经退出。");
    }

    private void PollHotkeys()
    {
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

        var f10Down = Native.GetAsyncKeyState(0x79) < 0;
        if (f10Down && !f10WasDown)
        {
            Pause();
            Environment.Exit(0);
        }
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
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[运行] 已锁定 {units.Count} 个单位：{string.Join(", ", units.Select(state => state.Unit.Id))}");
        Console.WriteLine("当前程序状态：已开启（F6 暂停）。");
        Console.ResetColor();
        UpdateConsoleTitle();
    }

    private void Pause()
    {
        if (enabled)
            Console.WriteLine("[暂停] 已停止发送移动指令。当前程序状态：已暂停。");
        enabled = false;
        units.Clear();
        recentlyCollected.Clear();
        Console.Title = "ra2-toolkit - 已暂停";
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
            Console.Title = "ra2-toolkit - 已停止";
            return;
        }

        var usableUnits = units.Where(state => state.InvalidSince is null).ToArray();
        if (usableUnits.Length == 0)
        {
            UpdateConsoleTitle();
            return;
        }

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

        UpdateConsoleTitle();
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

    private void UpdateConsoleTitle()
    {
        var usable = units.Count(state => state.InvalidSince is null);
        var unavailable = units.Count - usable;
        var collecting = units.Count(state => state.InvalidSince is null && state.ActiveCrate is not null);
        var waiting = usable - collecting;
        Console.Title = $"ra2-toolkit - 已开启 | {usable}可用 | {collecting}捡箱 | {waiting}待命 | {unavailable}暂不可用";
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

    private void QueueMission(CapturedUnit captured, byte mission, (short X, short Y)? destination)
    {
        var houseIndex = FindCurrentHouseIndex();
        var frame = ReadInt32(CurrentFrame) + Math.Max(0, ReadInt32(MaxAhead));
        var eventData = new byte[EventSize];
        eventData[0] = 0x04; // EventType::MegaMission
        eventData[2] = checked((byte)houseIndex);
        BitConverter.GetBytes(frame).CopyTo(eventData, 3);
        BitConverter.GetBytes(captured.Id).CopyTo(eventData, 7);
        eventData[11] = 52; // AbstractType::Abstract
        eventData[12] = mission;
        if (destination is { } cell)
        {
            BitConverter.GetBytes(cell.X + 1000 * cell.Y).CopyTo(eventData, 19);
            eventData[23] = 11; // AbstractType::Cell
        }

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
        var failures = new List<string>();
        if (count is < 0 or > 500)
            failures.Add($"当前选择数量={count}");
        if (houseCount is < 1 or > 10)
            failures.Add($"HouseClass数量={houseCount}");
        if (queueCount is < 0 or > QueueCapacity)
            failures.Add($"事件队列数量={queueCount}");
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

    public void Dispose() => handle.Dispose();

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
    private sealed record CrateSlot(int Index, short X, short Y);
    private readonly record struct CrateKey(int Index, short X, short Y);
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
