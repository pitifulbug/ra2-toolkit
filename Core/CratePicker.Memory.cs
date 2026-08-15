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

    private (short X, short Y)? ReadSafeCell(CapturedUnit unit, int attempt)
    {
        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return null;

        var fallback = (X: ReadInt16(house + HouseBaseCenterOffset),
            Y: ReadInt16(house + HouseBaseCenterOffset + 2));
        if (fallback == (0, 0))
        {
            fallback = (ReadInt16(house + HouseBaseSpawnCellOffset),
                ReadInt16(house + HouseBaseSpawnCellOffset + 2));
        }

        var reference = fallback == (0, 0)
            ? ReadUnitCell(unit.Pointer)
            : ((int X, int Y))fallback;
        var constructionYard = ReadMainBaseCell(house, reference);
        var anchor = constructionYard ?? fallback;

        if (ReadMapBounds() is not { } bounds)
            return null;
        if (anchor.X <= 0 || anchor.Y <= 0)
            return null;

        var reservedCells = units
            .Where(state => state.InvalidSince is null && state.Unit != unit &&
                            state.WaitingForCrate && state.SafeCell is not null)
            .Select(state => state.SafeCell!.Value)
            .ToHashSet();
        var start = (unit.Id + Math.Max(0, attempt)) % BaseReturnOffsets.Length;
        for (var index = 0; index < BaseReturnOffsets.Length; index++)
        {
            var offset = BaseReturnOffsets[(start + index) % BaseReturnOffsets.Length];
            var x = anchor.X + offset.X;
            var y = anchor.Y + offset.Y;
            if (x > 0 && y > 0 && x <= short.MaxValue && y <= short.MaxValue &&
                x >= bounds.Left && x <= bounds.Right &&
                y >= bounds.Top && y <= bounds.Bottom)
            {
                var candidate = (X: checked((short)x), Y: checked((short)y));
                if (!reservedCells.Contains(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private (short X, short Y)? ReadMainBaseCell(
        uint house, (int X, int Y) reference)
    {
        (short X, short Y)? result = null;
        var bestDistance = long.MaxValue;
        var bestId = int.MaxValue;
        foreach (var building in ReadVector(BuildingArray, 4096))
        {
            if (ReadUInt32(building + TechnoOwnerOffset) != house ||
                ReadByte(building + ObjectIsOnMapOffset) == 0 ||
                ReadByte(building + ObjectInLimboOffset) != 0 ||
                ReadByte(building + ObjectIsAliveOffset) == 0)
                continue;
            var type = ReadUInt32(building + BuildingTypeOffset);
            if (type == 0 || !ConstructionYardTypeIds.Contains(ReadTypeId(type)))
                continue;
            var cell = ReadUnitCell(building);
            if (cell.X is <= 0 or > short.MaxValue || cell.Y is <= 0 or > short.MaxValue)
                continue;
            var distance = DistanceSquared(cell, reference);
            var id = ReadInt32(building + 0x10);
            if (distance > bestDistance || distance == bestDistance && id >= bestId)
                continue;
            result = (checked((short)cell.X), checked((short)cell.Y));
            bestDistance = distance;
            bestId = id;
        }
        return result;
    }

    private static void ResetWaitingState(UnitState state)
    {
        state.WaitingForCrate = false;
        state.SafeCell = null;
        state.SafeCellAttempt = 0;
        state.AtSafePlace = false;
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
        if (ReadMapBounds() is not { } bounds)
            return [];
        var crateData = ReadBytes(Map + CratesOffset, 256 * 16);
        var result = new List<CrateSlot>();

        for (var index = 0; index < 256; index++)
        {
            var offset = index * 16;
            var x = BitConverter.ToInt16(crateData, offset + 12);
            var y = BitConverter.ToInt16(crateData, offset + 14);
            if (x >= bounds.Left && x <= bounds.Right &&
                y >= bounds.Top && y <= bounds.Bottom && x > 0 && y > 0)
                result.Add(new CrateSlot(index, x, y));
        }
        return result;
    }

    private (long Left, long Top, long Right, long Bottom)? ReadMapBounds()
    {
        var data = ReadBytes(Map + MapBoundsOffset, 16);
        var left = BitConverter.ToInt32(data, 0);
        var top = BitConverter.ToInt32(data, 4);
        var width = BitConverter.ToInt32(data, 8);
        var height = BitConverter.ToInt32(data, 12);
        if (width <= 0 || height <= 0)
            return null;
        return (left, top, (long)left + width - 1, (long)top + height - 1);
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

    internal static long DistanceSquared((int X, int Y) a, (int X, int Y) b)
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
        if (!Native.ReadProcessMemory(handle, (nint)address, data, (nuint)length, out var read) ||
            read != (nuint)length)
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
        if (!Native.WriteProcessMemory(handle, (nint)address, data, (nuint)data.Length, out var written) ||
            written != (nuint)data.Length)
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
                DisableRevealMapBestEffort();
                DisableInfiniteMoney();
                DisableOneHitKill();
                DisableHighDefense();
                DisableEliteUnits();
                DisableInfiniteRangeMode();
                DisableInfiniteSpeedMode();
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
        public int SafeCellAttempt { get; set; }
        public bool AtSafePlace { get; set; }
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
