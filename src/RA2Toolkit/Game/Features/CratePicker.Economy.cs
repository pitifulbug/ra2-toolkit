using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private byte[]? canBuildEntryBytes;

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

        WithSuspendedProcess(() =>
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            var drain = Math.Max(0, ReadInt32(house + HousePowerDrainOffset));
            StopTimer(house + HousePowerBlackoutTimerOffset);
            WriteInt32(house + HousePowerOutputOffset,
                Math.Max(LockedPowerOutput, drain + 100_000));
            WriteBytes(house + HouseRecheckPowerOffset, [MaximumPowerRecheckFlag]);
        });
    }

    private void DisableMaximumPower()
    {
        if (!maximumPowerEnabled)
            return;
        maximumPowerEnabled = false;
        DisableMaximumPowerPatch();
        WithSuspendedProcess(() =>
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house != 0)
                WriteBytes(house + HouseRecheckPowerOffset, [1]);
        });
        Console.WriteLine("[最高电力已关闭] 已交还游戏重新计算电力。");
    }

    private void EnableMaximumPowerPatch()
    {
        maximumPowerCodeCave = InstallOwnedHook(UpdatePowerFinalComparison,
            UpdatePowerOriginalBytes,
            cave => CreateMaximumPowerHookCode(cave, CurrentPlayer,
                UpdatePowerFinalComparison + UpdatePowerOriginalBytes.Length),
            "电力刷新函数");
        try
        {
            WithSuspendedProcess(() =>
            {
                var house = ReadUInt32(CurrentPlayer);
                if (house != 0)
                    WriteBytes(house + HouseRecheckPowerOffset, [1]);
            });
        }
        catch
        {
            RestoreHook(UpdatePowerFinalComparison, UpdatePowerOriginalBytes,
                ref maximumPowerCodeCave);
            throw;
        }
    }

    private void DisableMaximumPowerPatch()
    {
        RestoreHook(UpdatePowerFinalComparison, UpdatePowerOriginalBytes,
            ref maximumPowerCodeCave);
    }

    internal static byte[] CreateMaximumPowerHookCode(
        long caveAddress, long currentPlayerAddress, long resumeAddress)
    {
        var code = new List<byte>(32);
        code.AddRange(Convert.FromHexString("3B35")); // cmp esi,[CurrentPlayer]
        code.AddRange(BitConverter.GetBytes(checked((uint)currentPlayerAddress)));
        code.AddRange(Convert.FromHexString("750A")); // other houses keep native power
        code.AddRange(Convert.FromHexString("C786A453000040420F00")); // PowerOutput = 1,000,000
        code.AddRange(UpdatePowerOriginalBytes); // mov ecx,[esi+PowerOutput]
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(checked((int)
            (resumeAddress - (caveAddress + code.Count + 4)))));
        return [.. code];
    }

    private void ToggleFullTech()
    {
        SetFullTechEnabled(!fullTechEnabled);
    }

    private void DisableFullTech()
    {
        if (fullTechEnabled)
            SetFullTechEnabled(false);
    }

    private void SetFullTechEnabled(bool enabled)
    {
        var previous = fullTechEnabled;
        fullTechEnabled = enabled;
        try
        {
            UpdateCanBuildPatch();
        }
        catch
        {
            fullTechEnabled = previous;
            throw;
        }
    }

    private void ToggleUnlimitedProduction()
    {
        SetUnlimitedProductionEnabled(!unlimitedProductionEnabled);
    }

    private void DisableUnlimitedProduction()
    {
        if (unlimitedProductionEnabled)
            SetUnlimitedProductionEnabled(false);
    }

    private void SetUnlimitedProductionEnabled(bool enabled)
    {
        var previous = unlimitedProductionEnabled;
        unlimitedProductionEnabled = enabled;
        try
        {
            UpdateCanBuildPatch();
        }
        catch
        {
            unlimitedProductionEnabled = previous;
            throw;
        }
    }

    private void UpdateCanBuildPatch()
    {
        const int fullTechFlagOffset = 0xF0;
        const int unlimitedProductionFlagOffset = 0xF4;
        var shouldInstall = fullTechEnabled || unlimitedProductionEnabled;
        var originalHookTarget = 0L;

        if (shouldInstall && !canBuildPatchInstalled)
        {
            canBuildEntryBytes = ReadBytes(HouseCanBuild, HouseCanBuildOriginalBytes.Length);
            originalHookTarget = ResolveCanBuildOriginalTarget(canBuildEntryBytes);
            fullTechCodeCave = Native.VirtualAllocEx(handle, 0, 256,
                Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
            if (fullTechCodeCave == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "分配建造规则补丁代码区失败");

            try
            {
                var caveAddress = fullTechCodeCave.ToInt64();
                var code = CreateCanBuildCode(caveAddress,
                    caveAddress + fullTechFlagOffset,
                    caveAddress + unlimitedProductionFlagOffset,
                    originalHookTarget);
                if (code.Length > fullTechFlagOffset)
                    throw new InvalidOperationException("建造规则补丁超过预留代码区。");
                WriteCodeCave(fullTechCodeCave, code);
                WriteInt32(caveAddress + fullTechFlagOffset, fullTechEnabled ? 1 : 0);
                WriteInt32(caveAddress + unlimitedProductionFlagOffset,
                    unlimitedProductionEnabled ? 1 : 0);
            }
            catch
            {
                FreeUnpublishedCodeCaveBestEffort(fullTechCodeCave);
                fullTechCodeCave = 0;
                canBuildEntryBytes = null;
                throw;
            }
        }

        var suspended = false;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;

            if (shouldInstall)
            {
                var caveAddress = fullTechCodeCave.ToInt64();
                WriteInt32(caveAddress + fullTechFlagOffset, fullTechEnabled ? 1 : 0);
                WriteInt32(caveAddress + unlimitedProductionFlagOffset,
                    unlimitedProductionEnabled ? 1 : 0);
                if (!canBuildPatchInstalled)
                {
                    var actual = ReadBytes(HouseCanBuild, HouseCanBuildOriginalBytes.Length);
                    if (canBuildEntryBytes is null ||
                        !actual.AsSpan().SequenceEqual(canBuildEntryBytes))
                        throw new InvalidOperationException("建造规则函数入口在安装期间发生变化，未修改游戏代码。");

                    var jump = CreateCanBuildEntryJump(caveAddress);
                    WriteCode(HouseCanBuild, jump);
                    canBuildPatchInstalled = true;
                }
            }
            else if (canBuildPatchInstalled)
            {
                WriteInt32(fullTechCodeCave.ToInt64() + fullTechFlagOffset, 0);
                WriteInt32(fullTechCodeCave.ToInt64() + unlimitedProductionFlagOffset, 0);
                var originalEntry = canBuildEntryBytes ??
                    throw new InvalidOperationException("建造规则函数缺少待恢复的入口字节。");
                var installedEntry = CreateCanBuildEntryJump(fullTechCodeCave.ToInt64());
                var actual = ReadBytes(HouseCanBuild, originalEntry.Length);
                if (actual.AsSpan().SequenceEqual(installedEntry))
                    WriteCode(HouseCanBuild, originalEntry);
                else if (!actual.AsSpan().SequenceEqual(originalEntry))
                    throw new InvalidOperationException(
                        "建造规则函数入口已被其他模块修改，未覆盖未知补丁。");
                canBuildPatchInstalled = false;
                // A game thread can still be returning through the old trampoline.
                // Leave this tiny allocation alive and use a fresh cave next time.
                fullTechCodeCave = 0;
                canBuildEntryBytes = null;
            }
            MarkTechTreeForRefresh();
        }
        finally
        {
            if (suspended)
                ResumeSuspendedProcessOrThrow();
        }
    }

    private static byte[] CreateCanBuildEntryJump(long caveAddress) =>
        CreateRelativePatch(HouseCanBuild, HouseCanBuildOriginalBytes.Length, caveAddress);

    private long ResolveCanBuildOriginalTarget(byte[] entryBytes)
    {
        if (entryBytes.AsSpan().SequenceEqual(HouseCanBuildOriginalBytes))
            return 0;
        if (entryBytes.Length < 5 || entryBytes[0] != 0xE9)
            throw new InvalidOperationException(
                "建造规则函数入口既不是原始指令，也不是可验证的 Ares 跳转，未修改游戏代码。");

        var target = HouseCanBuild + 5L + BitConverter.ToInt32(entryBytes, 1);
        try
        {
            foreach (ProcessModule module in process.Modules)
            {
                if (!module.ModuleName.Equals("Ares.dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                var start = module.BaseAddress.ToInt64();
                var end = checked(start + module.ModuleMemorySize);
                if (target >= start && target < end)
                    return target;
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException("无法严格验证 Ares.dll 模块范围，未修改游戏代码。", error);
        }

        throw new InvalidOperationException(
            $"建造规则函数的现有跳转目标 0x{target:X} 不在 Ares.dll 模块范围内，未修改游戏代码。");
    }

    internal static byte[] CreateCanBuildCode(
        long caveAddress, long fullTechFlagAddress, long unlimitedProductionFlagAddress,
        long originalHookTarget = 0)
    {
        var code = new List<byte>(192);
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        var fixups = new List<(int Offset, string Label)>();

        void Label(string name) => labels.Add(name, code.Count);
        void NearJump(byte condition, string label)
        {
            code.Add(0x0F);
            code.Add(condition);
            fixups.Add((code.Count, label));
            code.AddRange(new byte[4]);
        }
        void Jump(string label)
        {
            code.Add(0xE9);
            fixups.Add((code.Count, label));
            code.AddRange(new byte[4]);
        }
        void Call(string label)
        {
            code.Add(0xE8);
            fixups.Add((code.Count, label));
            code.AddRange(new byte[4]);
        }
        void CompareFlag(long address)
        {
            code.AddRange(Convert.FromHexString("833D"));
            code.AddRange(BitConverter.GetBytes(checked((uint)address)));
            code.Add(0);
        }

        code.AddRange(Convert.FromHexString("3B0D"));
        code.AddRange(BitConverter.GetBytes(checked((uint)CurrentPlayer)));
        NearJump(0x85, "original"); // other houses always use the original rules
        CompareFlag(fullTechFlagAddress);
        NearJump(0x85, "full-tech");
        CompareFlag(unlimitedProductionFlagAddress);
        NearJump(0x85, "unlimited-production");
        Jump("original");

        Label("full-tech");
        code.AddRange(Convert.FromHexString("8B44240485C0"));
        NearJump(0x84, "original");
        code.AddRange(Convert.FromHexString("80B8980C000000"));
        NearJump(0x85, "original"); // preserve Unbuildable types
        code.AddRange(Convert.FromHexString("83B834060000FF"));
        NearJump(0x84, "original"); // preserve TechLevel=-1 types
        code.AddRange(Convert.FromHexString("837C240800"));
        NearJump(0x84, "buildable");
        CompareFlag(unlimitedProductionFlagAddress);
        NearJump(0x84, "original");
        code.AddRange(Convert.FromHexString("83B8B803000000"));
        NearJump(0x84, "original"); // BuildLimit=0 means explicitly disabled
        Jump("buildable");

        Label("unlimited-production");
        code.AddRange(Convert.FromHexString("8B44240485C0"));
        NearJump(0x84, "original");
        code.AddRange(Convert.FromHexString("83B8B803000000"));
        NearJump(0x84, "original");
        code.AddRange(Convert.FromHexString("5150FFB0B8030000"));
        code.AddRange(Convert.FromHexString("C780B8030000FFFFFF7F"));
        code.AddRange(Convert.FromHexString("FF742418FF742418FF742418"));
        code.AddRange(Convert.FromHexString("8B4C2414"));
        Call("original");
        code.AddRange(Convert.FromHexString("8B5424048B0C24898AB803000083C40CC20C00"));

        Label("buildable");
        code.AddRange(Convert.FromHexString("B801000000C20C00"));

        Label("original");
        if (originalHookTarget != 0)
        {
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (originalHookTarget - (caveAddress + code.Count + 4)))));
        }
        else
        {
            code.AddRange(HouseCanBuildOriginalBytes);
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (HouseCanBuild + HouseCanBuildOriginalBytes.Length -
                 (caveAddress + code.Count + 4)))));
        }

        foreach (var (offset, label) in fixups)
        {
            var displacement = BitConverter.GetBytes(labels[label] - (offset + 4));
            for (var index = 0; index < displacement.Length; index++)
                code[offset + index] = displacement[index];
        }
        return [.. code];
    }

    private void MarkTechTreeForRefresh()
    {
        var house = ReadUInt32(CurrentPlayer);
        if (house != 0)
            WriteBytes(house + HouseRecheckTechTreeOffset, [1]);
    }

    private void MaintainChronoLegionnaireNoCooldown()
    {
        var now = DateTime.UtcNow;
        if (now < nextChronoLegionnaireRefreshAt)
            return;
        nextChronoLegionnaireRefreshAt = now + TimeSpan.FromMilliseconds(30);

        WithSuspendedProcess(() =>
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            foreach (var pointer in ReadVector(FootArray, 10000))
            {
                if (ReadUInt32(pointer + TechnoOwnerOffset) != house ||
                    ReadByte(pointer + ObjectIsAliveOffset) == 0 ||
                    ReadByte(pointer + ObjectInLimboOffset) != 0)
                    continue;

                var locomotor = ReadUInt32(pointer + FootLocomotorOffset);
                if (locomotor == 0 || ReadUInt32(locomotor) != TeleportLocomotorVTable)
                    continue;

                if (ReadInt32(pointer + TechnoChronoLockRemainingOffset) != 0)
                    WriteInt32(pointer + TechnoChronoLockRemainingOffset, 0);
                if (ReadInt32(pointer + TechnoReloadTimerTimeLeftOffset) != 0)
                    WriteInt32(pointer + TechnoReloadTimerTimeLeftOffset, 0);
                if (ReadInt32(pointer + TechnoRearmTimerTimeLeftOffset) != 0)
                    WriteInt32(pointer + TechnoRearmTimerTimeLeftOffset, 0);
                if (ReadInt32(locomotor + TeleportLocomotorTimerTimeLeftOffset) != 0)
                    WriteInt32(locomotor + TeleportLocomotorTimerTimeLeftOffset, 0);
            }
        });
    }

    private string ReadTypeId(uint type)
    {
        var idBytes = ReadBytes(type + AbstractTypeIdOffset, 0x18);
        var terminator = Array.IndexOf(idBytes, (byte)0);
        return Encoding.ASCII.GetString(idBytes, 0,
            terminator < 0 ? idBytes.Length : terminator);
    }

    private void ToggleEliteUnits()
    {
        if (eliteUnitsEnabled)
        {
            DisableEliteUnits();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine("[单位升到三级未开启] 当前玩家阵营指针无效。");
            return;
        }

        eliteUnitStates.Clear();
        eliteUnitsHouse = house;
        int affected;
        try
        {
            affected = ApplyEliteUnits(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            try
            {
                RestoreEliteUnits();
            }
            catch
            {
            }
            eliteUnitStates.Clear();
            eliteUnitsHouse = 0;
            Console.WriteLine($"[单位升到三级未开启] {error.Message}");
            return;
        }

        eliteUnitsEnabled = true;
        nextEliteUnitsRefreshAt = DateTime.MinValue;
        Console.WriteLine($"[单位升到三级已开启] 已升级 {affected} 个现有单位，新单位会自动升级。");
    }

    private void MaintainEliteUnits()
    {
        var now = DateTime.UtcNow;
        if (now < nextEliteUnitsRefreshAt)
            return;
        nextEliteUnitsRefreshAt = now + TimeSpan.FromMilliseconds(250);

        try
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            if (house != eliteUnitsHouse)
            {
                RestoreEliteUnits();
                eliteUnitStates.Clear();
                eliteUnitsHouse = house;
            }
            ApplyEliteUnits(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            nextEliteUnitsRefreshAt = now + TimeSpan.FromSeconds(1);
        }
    }

    private int ApplyEliteUnits(uint house)
    {
        var affected = 0;
        WithSuspendedProcess(() =>
        {
            if (ReadUInt32(CurrentPlayer) != house)
                throw new InvalidOperationException(
                    "当前玩家阵营已经变化，已停止修改单位等级。");
            foreach (var pointer in ReadVector(FootArray, 10000))
            {
                if (ReadUInt32(pointer + TechnoOwnerOffset) != house ||
                    ReadByte(pointer + ObjectIsAliveOffset) == 0 ||
                    ReadByte(pointer + ObjectInLimboOffset) != 0)
                    continue;
                var id = ReadInt32(pointer + 0x10);
                if (id <= 0)
                    continue;
                if (!eliteUnitStates.TryGetValue(pointer, out var state) || state.Id != id)
                {
                    var originalVeterancy = ReadSingle(pointer + TechnoVeterancyOffset);
                    if (!float.IsFinite(originalVeterancy) ||
                        originalVeterancy is < 0.0f or > 2.0f)
                        continue;
                    eliteUnitStates[pointer] = new EliteUnitState(id, originalVeterancy);
                }
                if (ReadSingle(pointer + TechnoVeterancyOffset) != 2.0f)
                    WriteSingle(pointer + TechnoVeterancyOffset, 2.0f);
                affected++;
            }
        });
        return affected;
    }

    private void DisableEliteUnits()
    {
        if (!eliteUnitsEnabled)
            return;
        var restored = 0;
        try
        {
            if (!IsGameProcessUnavailable())
                restored = RestoreEliteUnits();
        }
        catch (Exception error) when (error is Win32Exception or GameProcessExitedException)
        {
            Console.WriteLine($"[单位等级恢复失败] {error.Message}");
        }
        finally
        {
            eliteUnitsEnabled = false;
            eliteUnitsHouse = 0;
            eliteUnitStates.Clear();
            Console.WriteLine($"[单位升到三级已关闭] 已恢复 {restored} 个仍然存在的单位。");
        }
    }

    private int RestoreEliteUnits()
    {
        var restored = 0;
        WithSuspendedProcess(() =>
        {
            var liveUnits = ReadVector(FootArray, 10000).ToHashSet();
            foreach (var (pointer, state) in eliteUnitStates)
            {
                if (!liveUnits.Contains(pointer) ||
                    ReadInt32(pointer + 0x10) != state.Id ||
                    ReadUInt32(pointer + TechnoOwnerOffset) != eliteUnitsHouse)
                    continue;
                WriteSingle(pointer + TechnoVeterancyOffset, state.OriginalVeterancy);
                restored++;
            }
        });
        return restored;
    }

}
