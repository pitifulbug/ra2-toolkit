using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
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

    private IEnumerable<uint> ReadPointerTable(long vectorAddress, int maximumCount)
    {
        var items = ReadUInt32(vectorAddress + 4);
        var count = ReadInt32(vectorAddress + 16);
        if (items == 0 || count is < 0 || count > maximumCount)
            yield break;

        var table = ReadBytes(items, count * 4);
        for (var index = 0; index < count; index++)
        {
            var pointer = BitConverter.ToUInt32(table, index * 4);
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
    private ushort ReadUInt16(long address) => BitConverter.ToUInt16(ReadBytes(address, 2));
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
    private void WriteUInt16(long address, ushort value) => WriteBytes(address, BitConverter.GetBytes(value));
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
                DisableHighDefense();
                DisableEliteUnits();
                DisableInfiniteRangeMode();
                ReleaseInfiniteRangePatch();
                DisableSpinningMcvMode();
                DisableMaximumPower();
                DisableFullTech();
                DisableUnlimitedProduction();
                chronoLegionnaireNoCooldownEnabled = false;
            DisableInstantBuild();
            StopAutoBuild(null);
            DisableBuildAnywhere();
            DisableDisabledGapGenerators();
            DisableInvadeMode();
            DisableGamePause();
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

    private sealed class SpinningMcvState(CapturedUnit unit, ushort facing)
    {
        public CapturedUnit Unit { get; } = unit;
        public ushort Facing { get; set; } = facing;
    }

    private sealed class FormationFacingState(
        CapturedUnit unit,
        (short X, short Y) destination,
        ushort facing,
        DateTime deadline)
    {
        public CapturedUnit Unit { get; } = unit;
        public (short X, short Y) Destination { get; } = destination;
        public ushort Facing { get; } = facing;
        public DateTime Deadline { get; } = deadline;
        public DateTime? AlignedSince { get; set; }
    }

    private enum AutoBuildPhase
    {
        WaitingForProduction,
        FindingPlacement,
        WaitingForPlacement
    }

    private sealed class AutoBuildState(
        uint typePointer,
        int typeIndex,
        string displayName,
        uint house,
        Queue<(short X, short Y)> candidates)
    {
        public uint TypePointer { get; } = typePointer;
        public int TypeIndex { get; } = typeIndex;
        public string DisplayName { get; } = displayName;
        public uint House { get; } = house;
        public Queue<(short X, short Y)> Candidates { get; } = candidates;
        public AutoBuildPhase Phase { get; set; }
        public uint Factory { get; set; }
        public bool HasCompletedObject { get; set; }
        public DateTime NextActionAt { get; set; }
        public DateTime Deadline { get; set; }
        public (short X, short Y) CurrentCandidate { get; set; }
        public int BuildingCountBeforePlacement { get; set; }
        public int BuiltCount { get; set; }
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
    private sealed record EliteUnitState(int Id, float OriginalVeterancy);
    private sealed record CrateSlot(int Index, short X, short Y);
    private readonly record struct CrateKey(int Index, short X, short Y);
}
