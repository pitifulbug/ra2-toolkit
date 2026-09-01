using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private void DisableSpinningMcvMode()
    {
        spinningMcvModeEnabled = false;
        spinningMcvs.Clear();
    }

    private int ToggleSelectedInfiniteSpeed()
    {
        var selected = CaptureSelectedUnits();
        if (selected.Count == 0)
            return 0;

        if (!infiniteSpeedModeEnabled)
            infiniteSpeedModeEnabled = true;

        var speedBoost = ReadDouble(PowerupArguments + PowerupSpeedArgumentIndex * sizeof(double));
        if (!IsReasonableSpeedMultiplier(speedBoost))
            throw new InvalidOperationException("游戏速度箱倍率无效，未修改选中单位。");

        var restoreSelected = selected.All(infiniteSpeedUnits.ContainsKey);
        var affected = 0;
        var suspended = false;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;
            foreach (var unit in selected)
            {
                if (restoreSelected)
                {
                    if (!infiniteSpeedUnits.TryGetValue(unit, out var restoredMultiplier))
                        continue;
                    if (IsCapturedFootIdentityValid(unit))
                        WriteBytes(unit.Pointer + FootSpeedMultiplierOffset,
                            BitConverter.GetBytes(restoredMultiplier));
                    infiniteSpeedUnits.Remove(unit);
                    affected++;
                    continue;
                }

                if (infiniteSpeedUnits.ContainsKey(unit) || !IsCapturedUnitValid(unit))
                    continue;
                var originalMultiplier = ReadDouble(unit.Pointer + FootSpeedMultiplierOffset);
                if (!IsReasonableSpeedMultiplier(originalMultiplier))
                    continue;
                var boostedMultiplier = CalculateInfiniteSpeedMultiplier(originalMultiplier, speedBoost);
                if (boostedMultiplier == originalMultiplier)
                    continue;
                infiniteSpeedUnits.Add(unit, originalMultiplier);
                WriteBytes(unit.Pointer + FootSpeedMultiplierOffset,
                    BitConverter.GetBytes(boostedMultiplier));
                affected++;
            }
        }
        finally
        {
            if (suspended)
                ResumeSuspendedProcessOrThrow();
        }

        return restoreSelected ? -affected : affected;
    }

    private void DisableInfiniteSpeedMode()
    {
        if (!infiniteSpeedModeEnabled && infiniteSpeedUnits.Count == 0)
            return;
        if (IsGameProcessUnavailable())
        {
            infiniteSpeedModeEnabled = false;
            infiniteSpeedUnits.Clear();
            return;
        }

        RestoreInfiniteSpeedUnits();
        infiniteSpeedModeEnabled = false;
    }

    private void RestoreInfiniteSpeedUnits()
    {
        if (infiniteSpeedUnits.Count == 0)
            return;

        var suspended = false;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;
            var restoreFailures = 0;
            foreach (var entry in infiniteSpeedUnits.ToArray())
            {
                if (!IsCapturedFootIdentityValid(entry.Key))
                {
                    infiniteSpeedUnits.Remove(entry.Key);
                    continue;
                }
                try
                {
                    WriteBytes(entry.Key.Pointer + FootSpeedMultiplierOffset,
                        BitConverter.GetBytes(entry.Value));
                    infiniteSpeedUnits.Remove(entry.Key);
                }
                catch (Win32Exception)
                {
                    restoreFailures++;
                }
            }
            if (restoreFailures != 0)
                throw new InvalidOperationException(
                    $"有 {restoreFailures} 个单位的原始移动速度仍待恢复，请重试关闭无限移速。");
        }
        finally
        {
            if (suspended)
                ResumeSuspendedProcessOrThrow();
        }
    }

    private bool IsCapturedFootIdentityValid(CapturedUnit unit)
    {
        try
        {
            return ReadInt32(unit.Pointer + 0x10) == unit.Id &&
                   VectorContains(FootArray, unit.Pointer);
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    internal static bool IsReasonableSpeedMultiplier(double value) =>
        double.IsFinite(value) && value is >= 0.05 and <= 10.0;

    internal static double CalculateInfiniteSpeedMultiplier(
        double originalMultiplier, double speedBoost)
    {
        if (!IsReasonableSpeedMultiplier(originalMultiplier) ||
            !IsReasonableSpeedMultiplier(speedBoost))
            return originalMultiplier;

        var boosted = originalMultiplier * speedBoost;
        return IsReasonableSpeedMultiplier(boosted) ? boosted : originalMultiplier;
    }

    private int ToggleSelectedInfiniteRange()
    {
        var selected = CaptureSelectedUnits();
        if (selected.Count == 0)
            return 0;

        if (!infiniteRangeModeEnabled)
        {
            InstallInfiniteRangePatch();
            infiniteRangeModeEnabled = true;
            nextInfiniteRangeValidationAt = DateTime.MinValue;
        }

        int affected;
        if (selected.All(infiniteRangeUnits.Contains))
        {
            foreach (var unit in selected)
                infiniteRangeUnits.Remove(unit);
            affected = -selected.Count;
        }
        else
        {
            var additions = selected.Where(unit => !infiniteRangeUnits.Contains(unit)).ToArray();
            if (infiniteRangeUnits.Count + additions.Length > MaximumInfiniteRangeUnits)
                throw new InvalidOperationException($"无限射程最多支持 {MaximumInfiniteRangeUnits} 个单位。");
            foreach (var unit in additions)
                infiniteRangeUnits.Add(unit);
            affected = additions.Length;
        }

        UpdateInfiniteRangeTable();
        return affected;
    }

    private int DeleteSelectedObjects()
    {
        var selected = CaptureSelectedObjects(technoOnly: false);
        if (selected.Count == 0)
            return 0;
        InvokeSelectedObjectAction(selected, takeOwnership: false);
        return selected.Count;
    }

    private int TakeOwnershipOfSelectedObjects()
    {
        var selected = CaptureSelectedObjects(technoOnly: true);
        if (selected.Count == 0)
            return 0;
        InvokeSelectedObjectAction(selected, takeOwnership: true);
        return selected.Count;
    }

    private List<CapturedUnit> CaptureSelectedObjects(bool technoOnly)
    {
        var items = ReadUInt32(CurrentObjects + 4);
        var count = ReadInt32(CurrentObjects + 16);
        var result = new List<CapturedUnit>();
        if (items == 0 || count is < 1 or > 100)
            return result;

        var seen = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var pointer = ReadUInt32(items + index * 4L);
            if (pointer == 0 || !seen.Add(pointer) ||
                technoOnly && !VectorContains(TechnoArray, pointer))
                continue;
            var id = ReadInt32(pointer + 0x10);
            if (id > 0)
                result.Add(new CapturedUnit(pointer, id));
        }
        return result;
    }

    private void InvokeSelectedObjectAction(IReadOnlyList<CapturedUnit> objects, bool takeOwnership) =>
        InvokeObjectAction(objects, takeOwnership, CurrentObjects, 100);

    private void InvokeObjectAction(IReadOnlyList<CapturedUnit> objects, bool takeOwnership,
        long membershipVector, int maximumCount)
    {
        if (!ReadBytes(LogicUpdate, LogicUpdateOriginalBytes.Length)
                .AsSpan().SequenceEqual(LogicUpdateOriginalBytes))
            throw new InvalidOperationException("游戏主循环函数指纹不匹配，未执行选中对象操作。");
        var house = takeOwnership ? ReadUInt32(CurrentPlayer) : 0;
        if (takeOwnership && house == 0)
            throw new InvalidOperationException("当前玩家阵营指针无效。");

        const int headerSize = 64;
        var codeCaveSize = headerSize + objects.Count * 96 +
            LogicUpdateOriginalBytes.Length + 16;
        var codeCave = Native.VirtualAllocEx(handle, 0, (nuint)codeCaveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (codeCave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配选中对象原生调用区失败");

        var markerAddress = codeCave.ToInt64() + codeCaveSize - 4;
        var mayBePublished = false;
        byte[]? installedJump = null;
        try
        {
            var code = new List<byte>(codeCaveSize) { 0x60 }; // pushad
            code.AddRange(Convert.FromHexString("833D")); // already completed
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.Add(0);
            var completedJump = code.Count;
            code.AddRange([0x0F, 0x85, 0, 0, 0, 0]);

            foreach (var obj in objects)
            {
                var skipFixups = new List<int>();
                void SkipIf(byte condition)
                {
                    code.AddRange([0x0F, condition]);
                    skipFixups.Add(code.Count);
                    code.AddRange(new byte[4]);
                }
                void Skip()
                {
                    code.Add(0xE9);
                    skipFixups.Add(code.Count);
                    code.AddRange(new byte[4]);
                }

                code.Add(0xA1); // mov eax,[membershipVector.Items]
                code.AddRange(BitConverter.GetBytes(checked((uint)(membershipVector + 4))));
                code.AddRange(Convert.FromHexString("8B15")); // mov edx,[membershipVector.Count]
                code.AddRange(BitConverter.GetBytes(checked((uint)(membershipVector + 16))));
                code.AddRange(Convert.FromHexString("85C0")); // test eax,eax
                SkipIf(0x84);
                code.AddRange(Convert.FromHexString("85D2")); // test edx,edx
                SkipIf(0x8E);
                code.AddRange(Convert.FromHexString("81FA")); // cmp edx,maximumCount
                code.AddRange(BitConverter.GetBytes(maximumCount));
                SkipIf(0x8F);

                var searchLoop = code.Count;
                code.AddRange(Convert.FromHexString("8138")); // cmp dword ptr [eax],pointer
                code.AddRange(BitConverter.GetBytes(obj.Pointer));
                var foundFixup = code.Count;
                code.AddRange([0x0F, 0x84, 0, 0, 0, 0]);
                code.AddRange(Convert.FromHexString("83C0044A")); // add eax,4; dec edx
                code.AddRange([0x0F, 0x85]);
                var loopFixup = code.Count;
                code.AddRange(new byte[4]);
                Skip();

                var found = code.Count;
                code.AddRange(Convert.FromHexString("8B08")); // mov ecx,[eax]
                code.AddRange(Convert.FromHexString("817910")); // cmp [ecx+10],id
                code.AddRange(BitConverter.GetBytes(obj.Id));
                SkipIf(0x85);
                code.AddRange(Convert.FromHexString("8B01"));
                if (takeOwnership)
                {
                    code.AddRange([0x6A, 0x01, 0x68]); // announce=true, house
                    code.AddRange(BitConverter.GetBytes(house));
                    code.AddRange(Convert.FromHexString("FF90D4030000"));
                }
                else
                {
                    code.AddRange(Convert.FromHexString("FF90F8000000"));
                }

                var skip = code.Count;
                BitConverter.GetBytes(found - (foundFixup + 6))
                    .CopyTo(CollectionsMarshal.AsSpan(code).Slice(foundFixup + 2, 4));
                BitConverter.GetBytes(searchLoop - (loopFixup + 4))
                    .CopyTo(CollectionsMarshal.AsSpan(code).Slice(loopFixup, 4));
                foreach (var fixup in skipFixups)
                    BitConverter.GetBytes(skip - (fixup + 4))
                        .CopyTo(CollectionsMarshal.AsSpan(code).Slice(fixup, 4));
            }

            code.AddRange([0xC7, 0x05]);
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.AddRange(BitConverter.GetBytes(1));
            var completed = code.Count;
            code.Add(0x61); // popad
            code.AddRange(LogicUpdateOriginalBytes);
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (LogicUpdate + LogicUpdateOriginalBytes.Length -
                 (codeCave.ToInt64() + code.Count + 4)))));
            if (code.Count > codeCaveSize - 4)
                throw new InvalidOperationException("选中对象原生调用区容量不足。");
            var completedDisplacement = BitConverter.GetBytes(completed - (completedJump + 6));
            for (var index = 0; index < completedDisplacement.Length; index++)
                code[completedJump + 2 + index] = completedDisplacement[index];
            WriteCodeCave(codeCave, [.. code]);

            installedJump = CreateRelativePatch(LogicUpdate,
                LogicUpdateOriginalBytes.Length, codeCave.ToInt64());
            PublishCodePatch(LogicUpdate, LogicUpdateOriginalBytes, installedJump,
                "游戏主循环", ref mayBePublished);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline && ReadInt32(markerAddress) != 1)
                Thread.Sleep(5);
            if (ReadInt32(markerAddress) != 1)
                throw new InvalidOperationException("等待游戏主线程执行选中对象操作超时。");
        }
        finally
        {
            if (mayBePublished && installedJump is not null)
            {
                try { RestoreOwnedCodePatch(LogicUpdate, LogicUpdateOriginalBytes, installedJump); }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                              GameProcessExitedException)
                {
                }
                RetireCodeCave(codeCave);
            }
            else
                FreeUnpublishedCodeCaveBestEffort(codeCave);
        }
    }

    private void MaintainInfiniteRangeUnits()
    {
        var now = DateTime.UtcNow;
        if (now < nextInfiniteRangeValidationAt)
            return;
        nextInfiniteRangeValidationAt = now + TimeSpan.FromMilliseconds(500);

        if (infiniteRangeUnits.RemoveWhere(unit => !IsCapturedUnitValid(unit)) != 0)
            UpdateInfiniteRangeTable();
    }

    private void InstallInfiniteRangePatch()
    {
        if (infiniteRangePatchInstalled)
            return;

        if (!ReadBytes(TechnoRangeValue, TechnoRangeValueOriginalBytes.Length)
                .AsSpan().SequenceEqual(TechnoRangeValueOriginalBytes))
            throw new InvalidOperationException(
                $"射程值函数 0x{TechnoRangeValue:X} 指纹不匹配，未修改游戏代码。");

        if (infiniteRangeCodeCave == 0)
        {
            infiniteRangeCodeCave = Native.VirtualAllocEx(handle, 0, InfiniteRangeCodeCaveSize,
                Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
            if (infiniteRangeCodeCave == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "分配无限射程补丁代码区失败");
            if ((ulong)infiniteRangeCodeCave.ToInt64() > uint.MaxValue)
            {
                Native.VirtualFreeEx(handle, infiniteRangeCodeCave, 0, Native.MemRelease);
                infiniteRangeCodeCave = 0;
                throw new InvalidOperationException("游戏是 32 位进程，但补丁代码区位于 32 位地址范围之外。");
            }
            infiniteRangeCountAddress = infiniteRangeCodeCave.ToInt64() + InfiniteRangeCountOffset;
            infiniteRangeTableAddress = infiniteRangeCodeCave.ToInt64() + InfiniteRangeTableOffset;

            var rangeHookAddress = infiniteRangeCodeCave.ToInt64();
            try
            {
                WriteCodeCave(infiniteRangeCodeCave, CreateSelectedRangeHook(
                    rangeHookAddress, TechnoRangeValue, TechnoRangeValueOriginalBytes,
                    infiniteRangeCountAddress, infiniteRangeTableAddress));
            }
            catch
            {
                FreeUnpublishedCodeCaveBestEffort(infiniteRangeCodeCave);
                infiniteRangeCodeCave = 0;
                infiniteRangeCountAddress = 0;
                infiniteRangeTableAddress = 0;
                throw;
            }
        }

        UpdateInfiniteRangeTable();
        var mayBePublished = false;
        var rangeJump = CreateRelativePatch(TechnoRangeValue,
            TechnoRangeValueOriginalBytes.Length, infiniteRangeCodeCave.ToInt64());
        try
        {
            PublishCodePatch(TechnoRangeValue, TechnoRangeValueOriginalBytes, rangeJump,
                "射程值函数", ref mayBePublished);
            infiniteRangePatchInstalled = true;
        }
        catch
        {
            infiniteRangePatchInstalled = mayBePublished;
            throw;
        }
    }

    private void DisableInfiniteRangeMode()
    {
        infiniteRangeModeEnabled = false;
        infiniteRangeUnits.Clear();
        if (!infiniteRangePatchInstalled)
            return;

        try
        {
            if (infiniteRangeCodeCave == 0)
                throw new InvalidOperationException("无限射程补丁缺少代码区，保留补丁所有权以供重试。");
            var rangeJump = CreateRelativePatch(TechnoRangeValue,
                TechnoRangeValueOriginalBytes.Length, infiniteRangeCodeCave.ToInt64());
            if (!UpdateInstalledAfterRestore(ref infiniteRangePatchInstalled,
                    () => RestoreOwnedCodePatch(
                        TechnoRangeValue, TechnoRangeValueOriginalBytes, rangeJump)))
            {
                throw new InvalidOperationException(
                    "无限射程补丁未能确认恢复，已保留补丁所有权以供重试。");
            }
        }
        finally
        {
            if (infiniteRangeCountAddress != 0)
            {
                try { WriteInt32(infiniteRangeCountAddress, 0); }
                catch (Exception error) when (error is Win32Exception or GameProcessExitedException)
                {
                }
            }
        }
    }

    private void ToggleFastTurn()
    {
        fastTurnEnabled = !fastTurnEnabled;
        nextFastTurnAt = DateTime.MinValue;
        if (fastTurnEnabled)
            MaintainFastTurn();
    }

    private void MaintainFastTurn()
    {
        var now = DateTime.UtcNow;
        if (now < nextFastTurnAt)
            return;
        nextFastTurnAt = now + TimeSpan.FromMilliseconds(75);

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;
        var candidates = new List<CapturedUnit>();
        foreach (var pointer in ReadVector(TechnoArray, 10000))
        {
            if (ReadUInt32(pointer + TechnoOwnerOffset) != house ||
                ReadByte(pointer + ObjectIsAliveOffset) == 0 ||
                ReadByte(pointer + ObjectInLimboOffset) != 0)
                continue;
            var id = ReadInt32(pointer + 0x10);
            if (id > 0)
                candidates.Add(new CapturedUnit(pointer, id));
        }
        if (candidates.Count == 0)
            return;

        WithSuspendedProcess(() =>
        {
            if (ReadUInt32(CurrentPlayer) != house)
                return;
            foreach (var candidate in candidates)
            {
                if (ReadInt32(candidate.Pointer + 0x10) != candidate.Id ||
                    ReadUInt32(candidate.Pointer + TechnoOwnerOffset) != house ||
                    ReadByte(candidate.Pointer + ObjectIsAliveOffset) == 0 ||
                    ReadByte(candidate.Pointer + ObjectIsOnMapOffset) == 0 ||
                    ReadByte(candidate.Pointer + ObjectInLimboOffset) != 0)
                    continue;
                SnapFacing(candidate.Pointer + TechnoPrimaryFacingOffset);
                SnapFacing(candidate.Pointer + TechnoSecondaryFacingOffset);
            }
        });
    }

    private void SnapFacing(long address)
    {
        var desired = ReadUInt16(address);
        if (ReadUInt16(address + 4) != desired)
            WriteUInt16(address + 4, desired);
        if (ReadInt32(address + 16) != 0)
            WriteInt32(address + 16, 0);
    }

    private void ToggleDisabledGapGenerators()
    {
        disableGapGeneratorsEnabled = !disableGapGeneratorsEnabled;
        nextGapGeneratorRefreshAt = DateTime.MinValue;
        if (!disableGapGeneratorsEnabled)
            RestoreGapGeneratorLocks();
    }

    private void DisableDisabledGapGenerators()
    {
        if (!disableGapGeneratorsEnabled && gapGeneratorStates.Count == 0)
            return;
        disableGapGeneratorsEnabled = false;
        RestoreGapGeneratorLocks();
    }

    private void MaintainDisabledGapGenerators()
    {
        var now = DateTime.UtcNow;
        if (now < nextGapGeneratorRefreshAt)
            return;
        nextGapGeneratorRefreshAt = now + TimeSpan.FromMilliseconds(100);
        WithSuspendedProcess(() =>
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            foreach (var building in ReadVector(BuildingArray, 4096))
            {
                if (ReadUInt32(building + TechnoOwnerOffset) == house ||
                    ReadByte(building + ObjectIsAliveOffset) == 0 ||
                    ReadByte(building + ObjectIsOnMapOffset) == 0 ||
                    ReadByte(building + ObjectInLimboOffset) != 0)
                    continue;
                var type = ReadUInt32(building + BuildingTypeOffset);
                if (type == 0 ||
                    !ReadTypeId(type).Equals("GAGAP", StringComparison.OrdinalIgnoreCase))
                    continue;
                var id = ReadInt32(building + 0x10);
                if (id <= 0)
                    continue;
                if (!gapGeneratorStates.TryGetValue(building, out var state) || state.Id != id)
                    gapGeneratorStates[building] = (id, ReadInt32(building + 0x504));
                WriteInt32(building + 0x504, 2);
            }
        });
    }

    private void RestoreGapGeneratorLocks()
    {
        WithSuspendedProcess(() =>
        {
            var liveBuildings = ReadVector(BuildingArray, 4096).ToHashSet();
            foreach (var (pointer, state) in gapGeneratorStates)
            {
                if (!liveBuildings.Contains(pointer) ||
                    ReadInt32(pointer + 0x10) != state.Id ||
                    ReadByte(pointer + ObjectIsAliveOffset) == 0 ||
                    ReadByte(pointer + ObjectIsOnMapOffset) == 0 ||
                    ReadByte(pointer + ObjectInLimboOffset) != 0)
                    continue;
                var type = ReadUInt32(pointer + BuildingTypeOffset);
                if (type != 0 &&
                    ReadTypeId(type).Equals("GAGAP", StringComparison.OrdinalIgnoreCase))
                    WriteInt32(pointer + 0x504, state.OriginalLock);
            }
        });
        gapGeneratorStates.Clear();
    }

    private nint InstallOwnedHook(long address, byte[] originalBytes,
        Func<long, byte[]> createCode, string feature)
    {
        var cave = Native.VirtualAllocEx(handle, 0, 128,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (cave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"分配{feature}代码区失败");
        var mayBePublished = false;
        try
        {
            var code = createCode(cave.ToInt64());
            if (code.Length > 128)
                throw new InvalidOperationException($"{feature}代码区容量不足。");
            WriteCodeCave(cave, code);
            var jump = CreateRelativePatch(address, originalBytes.Length, cave.ToInt64());
            PublishCodePatch(address, originalBytes, jump, feature, ref mayBePublished);
            return cave;
        }
        catch
        {
            if (mayBePublished)
                RetireCodeCave(cave);
            else
                FreeUnpublishedCodeCaveBestEffort(cave);
            throw;
        }
    }

    internal static bool UpdateInstalledAfterRestore(
        ref bool installed, Func<bool> restore)
    {
        ArgumentNullException.ThrowIfNull(restore);
        var restored = restore();
        if (restored)
            installed = false;
        return restored;
    }

    private void RestoreHook(long address, byte[] originalBytes, ref nint cave)
    {
        if (cave == 0)
            return;
        try
        {
            var installed = CreateRelativePatch(address, originalBytes.Length, cave.ToInt64());
            RestoreOwnedCodePatch(address, originalBytes, installed);
        }
        finally
        {
            RetireCodeCave(ref cave);
        }
    }

    private void ReleaseInfiniteRangePatch()
    {
        if (!RetireCaveIfUninstalled(infiniteRangePatchInstalled,
                ref infiniteRangeCodeCave, RetireCodeCave))
            return;
        infiniteRangeCountAddress = 0;
        infiniteRangeTableAddress = 0;
    }

    internal static bool RetireCaveIfUninstalled(
        bool installed, ref nint cave, Action<nint> retire)
    {
        ArgumentNullException.ThrowIfNull(retire);
        if (installed || cave == 0)
            return false;
        retire(cave);
        cave = 0;
        return true;
    }

    private void UpdateInfiniteRangeTable()
    {
        if (infiniteRangeCodeCave == 0)
            return;
        var selectedUnits = infiniteRangeUnits.ToArray();
        var table = new byte[MaximumInfiniteRangeUnits * 8];
        for (var index = 0; index < selectedUnits.Length; index++)
        {
            BitConverter.GetBytes(selectedUnits[index].Pointer).CopyTo(table, index * 8);
            BitConverter.GetBytes(selectedUnits[index].Id).CopyTo(table, index * 8 + 4);
        }

        WithSuspendedProcess(() =>
        {
            WriteInt32(infiniteRangeCountAddress, 0);
            WriteBytes(infiniteRangeTableAddress, table);
            WriteInt32(infiniteRangeCountAddress, selectedUnits.Length);
        });
    }

    internal static byte[] CreateSelectedRangeHook(long hookAddress, long originalAddress,
        byte[] originalBytes, long countAddress, long tableAddress)
    {
        var code = new List<byte>(96);
        code.AddRange([0x9C, 0x60]); // preserve flags and all registers
        code.Add(0xA1); // mov eax,[count]
        code.AddRange(BitConverter.GetBytes(checked((uint)countAddress)));
        code.AddRange([0x85, 0xC0]); // test eax,eax
        var noEntriesJump = code.Count;
        code.AddRange([0x74, 0]);
        code.Add(0xBA); // mov edx,table
        code.AddRange(BitConverter.GetBytes(checked((uint)tableAddress)));
        var loop = code.Count;
        code.AddRange([0x39, 0x32]); // cmp [edx],esi
        var pointerMismatchJump = code.Count;
        code.AddRange([0x75, 0]);
        code.AddRange([0x8B, 0x4A, 0x04]); // mov ecx,[edx+4] (captured object id)
        code.AddRange([0x39, 0x4E, 0x10]); // cmp [esi+10],ecx
        var matchedJump = code.Count;
        code.AddRange([0x74, 0]);
        var next = code.Count;
        code.AddRange([0x83, 0xC2, 0x08, 0x48]);
        var loopJump = code.Count;
        code.AddRange([0x75, 0]);
        var noMatch = code.Count;
        code.AddRange([0x61, 0x9D]);
        code.AddRange(originalBytes);
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(checked((int)(originalAddress + originalBytes.Length -
            (hookAddress + code.Count + 4)))));
        var matched = code.Count;
        code.AddRange([0x61, 0x9D, 0xBF, 0x00, 0xF9, 0x00, 0x00, 0xE9]);
        code.AddRange(BitConverter.GetBytes(checked((int)(originalAddress + originalBytes.Length -
            (hookAddress + code.Count + 4)))));
        code[noEntriesJump + 1] = checked((byte)(noMatch - (noEntriesJump + 2)));
        code[pointerMismatchJump + 1] = checked((byte)(next - (pointerMismatchJump + 2)));
        code[matchedJump + 1] = checked((byte)(matched - (matchedJump + 2)));
        code[loopJump + 1] = unchecked((byte)(sbyte)(loop - (loopJump + 2)));
        return [.. code];
    }

    private int ToggleSelectedSpinningMcvs()
    {
        var selectedMcvs = CaptureSelectedUnits()
            .Where(IsMcv)
            .ToArray();
        if (selectedMcvs.Length == 0)
            return 0;

        if (!spinningMcvModeEnabled)
        {
            spinningMcvModeEnabled = true;
            nextMcvSpinAt = DateTime.MinValue;
        }

        if (selectedMcvs.All(spinningMcvs.ContainsKey))
        {
            foreach (var unit in selectedMcvs)
                spinningMcvs.Remove(unit);
            return -selectedMcvs.Length;
        }

        var added = 0;
        foreach (var unit in selectedMcvs)
        {
            if (spinningMcvs.ContainsKey(unit))
                continue;
            var facing = ReadUInt16(unit.Pointer + TechnoPrimaryFacingOffset);
            spinningMcvs.Add(unit, new SpinningMcvState(unit, facing));
            QueueGuard(unit);
            added++;
        }
        return added;
    }

    private void MaintainSpinningMcvs()
    {
        var now = DateTime.UtcNow;
        if (now < nextMcvSpinAt)
            return;
        nextMcvSpinAt = now + TimeSpan.FromMilliseconds(45);

        WithSuspendedProcess(() =>
        {
            foreach (var state in spinningMcvs.Values.ToArray())
            {
                if (!IsCapturedUnitValid(state.Unit))
                {
                    spinningMcvs.Remove(state.Unit);
                    continue;
                }

                state.Facing = unchecked((ushort)(state.Facing + McvSpinFacingStep));
                WriteUInt16(state.Unit.Pointer + TechnoPrimaryFacingOffset, state.Facing);
                WriteUInt16(state.Unit.Pointer + TechnoPrimaryFacingOffset + 4, state.Facing);
            }
        });
    }

    private bool IsMcv(CapturedUnit unit)
    {
        if (!VectorContains(UnitArray, unit.Pointer))
            return false;
        var type = ReadUInt32(unit.Pointer + UnitTypeOffset);
        if (type == 0)
            return false;
        var idBytes = ReadBytes(type + AbstractTypeIdOffset, 0x18);
        var terminator = Array.IndexOf(idBytes, (byte)0);
        var length = terminator >= 0 ? terminator : idBytes.Length;
        var id = System.Text.Encoding.ASCII.GetString(idBytes, 0, length);
        return McvTypeIds.Contains(id);
    }

    private int ArrangeSelectedFormation()
    {
        var selected = CaptureSelectedUnits();
        if (selected.Count == 0)
            return 0;

        const int spacing = 1;
        const int infantryPerCell = 3;
        var infantryPointers = ReadVector(InfantryArray, 4096).ToHashSet();
        var isInfantryFormation = selected.All(unit => infantryPointers.Contains(unit.Pointer));
        var occupiedCellCount = isInfantryFormation
            ? (int)Math.Ceiling(selected.Count / (double)infantryPerCell)
            : selected.Count;
        var columns = (int)Math.Ceiling(Math.Sqrt(occupiedCellCount));
        var rows = (int)Math.Ceiling(occupiedCellCount / (double)columns);
        var positionedUnits = selected
            .Select(unit => (Unit: unit, Position: ReadUnitCell(unit.Pointer)))
            .OrderBy(item => item.Position.Y)
            .ThenBy(item => item.Position.X)
            .ToArray();

        var centerX = (int)Math.Round(positionedUnits.Average(item => item.Position.X));
        var centerY = (int)Math.Round(positionedUnits.Average(item => item.Position.Y));
        if (ReadMapBounds() is not { } bounds)
            return 0;
        var (left, top, right, bottom) = bounds;
        var formationWidth = (columns - 1) * spacing;
        var formationHeight = (rows - 1) * spacing;
        var firstColumnX = Math.Clamp(centerX - formationWidth / 2, left, right - formationWidth);
        var firstRowY = Math.Clamp(centerY - formationHeight / 2, top, bottom - formationHeight);
        var destinations = new List<(short X, short Y)>(selected.Count);

        for (var row = 0; row < rows; row++)
        {
            var countInRow = Math.Min(columns, occupiedCellCount - row * columns);
            var columnOffset = (columns - countInRow) / 2;
            for (var column = 0; column < countInRow; column++)
            {
                var destination = (X: checked((short)(firstColumnX +
                                                       (columnOffset + column) * spacing)),
                    Y: checked((short)(firstRowY + row * spacing)));
                var unitsInCell = isInfantryFormation
                    ? Math.Min(infantryPerCell, selected.Count - destinations.Count)
                    : 1;
                for (var index = 0; index < unitsInCell; index++)
                    destinations.Add(destination);
            }
        }

        var facingDelta = (X: 0, Y: 0);
        // Increasing map X runs northwest to southeast on the isometric battlefield.
        // Approach every slot from one cell northwest so all ranks enter in the same direction.
        foreach (var candidate in new[] { (X: 1, Y: 0), (X: 0, Y: -1), (X: 0, Y: 1), (X: -1, Y: 0) })
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
        formationFacingStates.Clear();
        var commonFacing = GetFormationFacing(facingDelta);
        var facingDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
        foreach (var (item, destination) in positionedUnits.Zip(destinations))
        {
            pendingMissions.Remove(item.Unit.Id);
            var approach = (X: checked((short)(destination.X - facingDelta.X)),
                Y: checked((short)(destination.Y - facingDelta.Y)));
            formationMissions.Enqueue(new QueuedMission(item.Unit, 2, approach));
            formationMissions.Enqueue(new QueuedMission(item.Unit, 3, destination));
            formationFacingStates.Add(new FormationFacingState(
                item.Unit, destination, commonFacing, facingDeadline));
        }
        return selected.Count;
    }

    private void MaintainFormationFacing()
    {
        var now = DateTime.UtcNow;
        if (now < nextFormationFacingAt)
            return;
        nextFormationFacingAt = now + TimeSpan.FromMilliseconds(50);

        WithSuspendedProcess(() =>
        {
            for (var index = formationFacingStates.Count - 1; index >= 0; index--)
            {
                var state = formationFacingStates[index];
                if (!IsCapturedUnitValid(state.Unit))
                {
                    formationFacingStates.RemoveAt(index);
                    continue;
                }

                var reachedDestination = ReadUnitCell(state.Unit.Pointer) == state.Destination;
                if (!reachedDestination && now < state.Deadline)
                    continue;
                if (!reachedDestination && state.AlignedSince is null)
                    QueueGuard(state.Unit);

                SetUnitFacing(state.Unit, state.Facing);
                state.AlignedSince ??= now;
                if (now - state.AlignedSince.Value >= TimeSpan.FromMilliseconds(750))
                    formationFacingStates.RemoveAt(index);
            }
        });
    }

    private void SetUnitFacing(CapturedUnit unit, ushort facing)
    {
        WriteUInt16(unit.Pointer + TechnoPrimaryFacingOffset, facing);
        WriteUInt16(unit.Pointer + TechnoPrimaryFacingOffset + 4, facing);
        WriteUInt16(unit.Pointer + TechnoSecondaryFacingOffset, facing);
        WriteUInt16(unit.Pointer + TechnoSecondaryFacingOffset + 4, facing);
    }

    internal static ushort GetFormationFacing((int X, int Y) direction) => direction switch
    {
        (1, 0) => 0x6000,
        (0, -1) => 0x2000,
        (0, 1) => 0xA000,
        _ => 0xE000
    };

}
