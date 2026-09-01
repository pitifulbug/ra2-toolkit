using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private const int InfiniteRangeCleanupAttempts = 3;
    private bool processResumePending;

    private uint[] ReadVector(long vectorAddress, int maximumCount)
    {
        var header = ReadBytes(vectorAddress, 20);
        var items = BitConverter.ToUInt32(header, 4);
        var count = BitConverter.ToInt32(header, 16);
        if (items == 0 || count is < 0 || count > maximumCount)
            return [];
        if (count == 0)
            return [];

        return DecodePointerSnapshot(ReadBytes(items, checked(count * sizeof(uint))));
    }

    private uint[] ReadPointerTable(long vectorAddress, int maximumCount) =>
        ReadVector(vectorAddress, maximumCount);

    internal static uint[] DecodePointerSnapshot(ReadOnlySpan<byte> table)
    {
        if (table.Length % sizeof(uint) != 0)
            throw new ArgumentException("指针表长度必须是 4 的倍数。", nameof(table));

        var result = new List<uint>(table.Length / sizeof(uint));
        for (var offset = 0; offset < table.Length; offset += sizeof(uint))
        {
            var pointer = BitConverter.ToUInt32(table.Slice(offset, sizeof(uint)));
            if (pointer != 0)
                result.Add(pointer);
        }
        return [.. result];
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
        return DecodeMapBounds(data);
    }

    internal static (long Left, long Top, long Right, long Bottom)? DecodeMapBounds(
        ReadOnlySpan<byte> data)
    {
        if (data.Length < 16)
            return null;

        // MapClass + 0x124 is MapCoordBounds (LTRBStruct), not MapRect.
        var left = BitConverter.ToInt32(data[..4]);
        var top = BitConverter.ToInt32(data.Slice(4, 4));
        var right = BitConverter.ToInt32(data.Slice(8, 4));
        var bottom = BitConverter.ToInt32(data.Slice(12, 4));
        return right >= left && bottom >= top
            ? (left, top, right, bottom)
            : null;
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
        return CreateEventHeader(eventType, checked((byte)FindCurrentHouseIndex()),
            ReadInt32(CurrentFrame));
    }

    internal static byte[] CreateEventHeader(byte eventType, byte houseIndex,
        int currentFrame)
    {
        var eventData = new byte[EventSize];
        eventData[0] = eventType;
        eventData[2] = houseIndex;
        // Networking::AddEvent timestamps synchronized events at CurrentFrame.
        // MaxAhead is a scheduling/window value and must not be added here.
        BitConverter.GetBytes(currentFrame).CopyTo(eventData, 3);
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
        if (!formationModeEnabled && formationMissions.Count != 0)
            formationMissions.Clear();
        if (pendingMissions.Count == 0 && formationMissions.Count == 0 || now < nextMissionFlushAt)
            return;
        nextMissionFlushAt = now + TimeSpan.FromMilliseconds(50);

        var suspended = false;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;
            var count = ReadInt32(OutList);
            var tail = ReadInt32(OutList + 8);
            var originalCount = count;
            var originalTail = tail;
            if (count is < 0 or > QueueCapacity || tail is < 0 or >= QueueCapacity)
                throw new InvalidOperationException("游戏事件队列状态异常，已停止写入。");

            var batchLimit = Math.Min(MissionEventsPerBatch, QueueCapacity - count);
            var batch = new List<QueuedMission>(batchLimit);
            batch.AddRange(formationMissions.Take(batchLimit));
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
            try
            {
                WriteInt32(OutList + 8, tail);
                WriteInt32(OutList, count);
            }
            catch
            {
                RestoreEventQueueHeaderBestEffort(originalCount, originalTail);
                throw;
            }
            for (var index = 0; index < formationBatchCount; index++)
                _ = formationMissions.Dequeue();
            for (var index = formationBatchCount; index < batch.Count; index++)
                pendingMissions.Remove(batch[index].Unit.Id);
        }
        finally
        {
            if (suspended)
                ResumeSuspendedProcessOrThrow();
        }
    }

    private bool TryEnqueueEvent(byte[] eventData)
    {
        if (eventData.Length != EventSize)
            throw new ArgumentException($"游戏事件长度必须为 {EventSize} 字节。", nameof(eventData));

        var suspended = false;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;
            var count = ReadInt32(OutList);
            var tail = ReadInt32(OutList + 8);
            if (count is < 0 or > QueueCapacity || tail is < 0 or >= QueueCapacity)
                throw new InvalidOperationException("游戏事件队列状态异常，已停止写入。");
            if (count == QueueCapacity)
                return false;

            WriteBytes(OutList + 12 + tail * EventSize, eventData);
            WriteInt32(OutList + 12 + QueueCapacity * EventSize + tail * 4L, Environment.TickCount);
            try
            {
                WriteInt32(OutList + 8, (tail + 1) & (QueueCapacity - 1));
                WriteInt32(OutList, count + 1);
            }
            catch
            {
                RestoreEventQueueHeaderBestEffort(count, tail);
                throw;
            }
            return true;
        }
        finally
        {
            if (suspended)
                ResumeSuspendedProcessOrThrow();
        }
    }

    private void RestoreEventQueueHeaderBestEffort(int count, int tail)
    {
        try { WriteInt32(OutList + 8, tail); }
        catch (Exception error) when (error is Win32Exception or GameProcessExitedException)
        {
        }
        try { WriteInt32(OutList, count); }
        catch (Exception error) when (error is Win32Exception or GameProcessExitedException)
        {
        }
    }

    internal static bool CanEnqueueEventHeader(int count, int tail) =>
        count is >= 0 and < QueueCapacity && tail is >= 0 and < QueueCapacity;

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
        Exception? operationError = null;
        try
        {
            WriteBytes(address, data);
            if (!ReadBytes(address, data.Length).AsSpan().SequenceEqual(data))
                throw new InvalidOperationException($"地址 0x{address:X} 的代码写入后校验失败。");
            if (!Native.FlushInstructionCache(handle, (nint)address, (nuint)data.Length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "刷新游戏指令缓存失败");
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            if (!Native.VirtualProtectEx(handle, (nint)address, (nuint)data.Length,
                    previousProtection, out _))
            {
                var protectionError = new Win32Exception(Marshal.GetLastWin32Error(),
                    $"恢复地址 0x{address:X} 的页面保护失败");
                if (operationError is null)
                    throw protectionError;
                Console.Error.WriteLine($"[代码页保护恢复失败] {protectionError.Message}");
            }
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

    private int ResumeProcessWithRetry()
    {
        var status = int.MinValue;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            status = Native.NtResumeProcess(handle);
            if (status >= 0)
                return status;
            Thread.Sleep(1);
        }
        return status;
    }

    private void SuspendProcessOrThrow()
    {
        EnsureProcessResumed();
        CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
    }

    private void ResumeSuspendedProcessOrThrow()
    {
        var status = ResumeProcessWithRetry();
        if (status < 0)
        {
            processResumePending = true;
            throw new InvalidOperationException(
                $"恢复游戏进程失败，游戏可能仍处于暂停状态（NTSTATUS 0x{status:X8}）");
        }
        processResumePending = false;
    }

    private void WithSuspendedProcess(Action action)
    {
        var suspended = false;
        Exception? operationError = null;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;
            action();
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            if (suspended)
            {
                var status = ResumeProcessWithRetry();
                if (status < 0)
                {
                    processResumePending = true;
                    var resumeError = new InvalidOperationException(
                        $"恢复游戏进程失败，游戏可能仍处于暂停状态（NTSTATUS 0x{status:X8}）");
                    if (operationError is null)
                        throw resumeError;
                    throw new InvalidOperationException(
                        "游戏内存操作失败，且恢复游戏进程也失败。",
                        new AggregateException(operationError, resumeError));
                }
                processResumePending = false;
            }
        }
    }

    private void EnsureProcessResumed()
    {
        if (!processResumePending)
            return;
        var status = ResumeProcessWithRetry();
        if (status < 0)
        {
            throw new InvalidOperationException(
                $"再次恢复游戏进程失败，游戏可能仍处于暂停状态（NTSTATUS 0x{status:X8}）");
        }
        processResumePending = false;
    }

    private void WriteCodeCave(nint cave, byte[] code)
    {
        WriteBytes(cave.ToInt64(), code);
        if (!ReadBytes(cave.ToInt64(), code.Length).AsSpan().SequenceEqual(code))
            throw new InvalidOperationException("远程代码洞写入后校验失败。");
        if (!Native.FlushInstructionCache(handle, cave, (nuint)code.Length))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "刷新远程代码洞指令缓存失败");
    }

    internal static byte[] CreateRelativePatch(long address, int length, long target,
        byte opcode = 0xE9)
    {
        if (length < 5)
            throw new ArgumentOutOfRangeException(nameof(length), "相对跳转补丁至少需要 5 字节。");
        var patch = Enumerable.Repeat((byte)0x90, length).ToArray();
        patch[0] = opcode;
        BitConverter.GetBytes(checked((int)(target - (address + 5)))).CopyTo(patch, 1);
        return patch;
    }

    internal static bool IsRollbackCompatible(ReadOnlySpan<byte> actual,
        ReadOnlySpan<byte> original, ReadOnlySpan<byte> installed)
    {
        if (actual.Length != original.Length || actual.Length != installed.Length)
            return false;
        for (var index = 0; index < actual.Length; index++)
            if (actual[index] != original[index] && actual[index] != installed[index])
                return false;
        return true;
    }

    private bool CodeMatches(long address, byte[] expected) =>
        ReadBytes(address, expected.Length).AsSpan().SequenceEqual(expected);

    private bool TryRollbackFailedCodePatch(long address, byte[] original, byte[] installed)
    {
        try
        {
            var actual = ReadBytes(address, original.Length);
            if (actual.AsSpan().SequenceEqual(original))
                return true;
            if (!IsRollbackCompatible(actual, original, installed))
                return false;
            try { WriteCode(address, original); }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException)
            {
            }
            return CodeMatches(address, original);
        }
        catch (Exception error) when (error is Win32Exception or GameProcessExitedException)
        {
            return false;
        }
    }

    private void PublishCodePatch(long address, byte[] original, byte[] installed,
        string feature, ref bool mayBePublished)
    {
        var published = mayBePublished;
        try
        {
            WithSuspendedProcess(() =>
            {
                if (!CodeMatches(address, original))
                    throw new InvalidOperationException(
                        $"{feature}地址 0x{address:X} 指纹不匹配，未修改游戏代码。");
                try
                {
                    WriteCode(address, installed);
                    published = true;
                }
                catch
                {
                    published = !TryRollbackFailedCodePatch(address, original, installed);
                    throw;
                }
            });
        }
        finally
        {
            mayBePublished = published;
        }
    }

    private bool RestoreOwnedCodePatch(long address, byte[] original, byte[] installed)
    {
        var restored = false;
        WithSuspendedProcess(() =>
        {
            var actual = ReadBytes(address, original.Length);
            if (actual.AsSpan().SequenceEqual(original))
            {
                restored = true;
                return;
            }
            if (!actual.AsSpan().SequenceEqual(installed))
                return;
            WriteCode(address, original);
            restored = CodeMatches(address, original);
        });
        return restored;
    }

    private void RetireCodeCave(nint cave)
    {
        if (cave != 0)
            retiredCodeCaves.Add(cave);
    }

    private void RetireCodeCave(ref nint cave)
    {
        RetireCodeCave(cave);
        cave = 0;
    }

    private void FreeUnpublishedCodeCaveBestEffort(nint cave)
    {
        if (cave != 0)
            Native.VirtualFreeEx(handle, cave, 0, Native.MemRelease);
    }

    internal static int RunCleanupWithRetry(
        Func<bool> cleanupPending, Action cleanupAttempt, int maximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(cleanupPending);
        ArgumentNullException.ThrowIfNull(cleanupAttempt);
        if (maximumAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        var attempts = 0;
        do
        {
            cleanupAttempt();
            attempts++;
        } while (attempts < maximumAttempts && cleanupPending());
        return attempts;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeState, 1) != 0)
            return;
        exitRequested = true;
        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                          GameProcessExitedException)
            {
                Console.Error.WriteLine($"[清理失败] {error.Message}");
            }
        }

        try
        {
            if (!IsGameProcessUnavailable())
            {
                Cleanup(EnsureProcessResumed);
                Cleanup(DisableGamePause);
                Cleanup(DisableCratePickerForShutdown);
                Cleanup(DisableRevealMapBestEffort);
                Cleanup(DisableInfiniteMoney);
                Cleanup(DisableOneHitKill);
                Cleanup(DisableHighDefense);
                Cleanup(DisableEliteUnits);
                _ = RunCleanupWithRetry(
                    () => infiniteRangePatchInstalled,
                    () => Cleanup(() =>
                    {
                        EnsureProcessResumed();
                        DisableInfiniteRangeMode();
                    }),
                    InfiniteRangeCleanupAttempts);
                Cleanup(DisableInfiniteSpeedMode);
                Cleanup(ReleaseInfiniteRangePatch);
                Cleanup(DisableSpinningMcvMode);
                Cleanup(DisableMaximumPower);
                Cleanup(DisableFullTech);
                Cleanup(DisableUnlimitedProduction);
                chronoLegionnaireNoCooldownEnabled = false;
                Cleanup(DisableInstantBuild);
                Cleanup(() => StopAutoBuild(null));
                Cleanup(DisableBuildAnywhere);
                Cleanup(DisableDisabledGapGenerators);
                Cleanup(DisableInvadeMode);
                Cleanup(DisablePlayerEnhancements);
                Cleanup(DisableUnitEnhancements);
                Cleanup(RestoreMiscOverrides);
                Cleanup(DisableEnemyAndFunFeatures);
                Cleanup(RestoreRulesOverrides);
                Cleanup(RestorePendingLogicUpdatePatch);
                Cleanup(EnsureProcessResumed);
            }
            autoBuildState = null;
            formationModeEnabled = false;
            formationMissions.Clear();
            formationFacingStates.Clear();
            pendingMissions.Clear();
            autoRepairEnabled = false;
            superWeaponNoCooldownEnabled = false;
            paratrooperNoCooldownEnabled = false;
        }
        finally
        {
            handle.Dispose();
            process.Dispose();
            retiredCodeCaves.Clear();
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
