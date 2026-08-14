using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
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

        if (shouldInstall && !canBuildPatchInstalled)
        {
            fullTechCodeCave = Native.VirtualAllocEx(handle, 0, 256,
                Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
            if (fullTechCodeCave == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "分配建造规则补丁代码区失败");

            var caveAddress = fullTechCodeCave.ToInt64();
            var code = CreateCanBuildCode(caveAddress,
                caveAddress + fullTechFlagOffset,
                caveAddress + unlimitedProductionFlagOffset);
            if (code.Length > fullTechFlagOffset)
                throw new InvalidOperationException("建造规则补丁超过预留代码区。");
            WriteBytes(caveAddress, code);
            WriteInt32(caveAddress + fullTechFlagOffset, fullTechEnabled ? 1 : 0);
            WriteInt32(caveAddress + unlimitedProductionFlagOffset,
                unlimitedProductionEnabled ? 1 : 0);
            if (!Native.FlushInstructionCache(handle, fullTechCodeCave, (nuint)code.Length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "刷新建造规则补丁指令缓存失败");
        }

        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
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
                    if (!actual.AsSpan().SequenceEqual(HouseCanBuildOriginalBytes))
                        throw new InvalidOperationException("建造规则函数指纹不匹配，未修改游戏代码。");

                    var jump = new byte[HouseCanBuildOriginalBytes.Length];
                    jump[0] = 0xE9;
                    BitConverter.GetBytes(checked((int)(caveAddress - (HouseCanBuild + 5))))
                        .CopyTo(jump, 1);
                    jump[5] = 0x90;
                    jump[6] = 0x90;
                    WriteCode(HouseCanBuild, jump);
                    canBuildPatchInstalled = true;
                }
            }
            else if (canBuildPatchInstalled)
            {
                WriteInt32(fullTechCodeCave.ToInt64() + fullTechFlagOffset, 0);
                WriteInt32(fullTechCodeCave.ToInt64() + unlimitedProductionFlagOffset, 0);
                WriteCode(HouseCanBuild, HouseCanBuildOriginalBytes);
                canBuildPatchInstalled = false;
                // A game thread can still be returning through the old trampoline.
                // Leave this tiny allocation alive and use a fresh cave next time.
                fullTechCodeCave = 0;
            }
            MarkTechTreeForRefresh();
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private static byte[] CreateCanBuildCode(
        long caveAddress, long fullTechFlagAddress, long unlimitedProductionFlagAddress)
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
        code.AddRange(HouseCanBuildOriginalBytes);
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(checked((int)
            (HouseCanBuild + HouseCanBuildOriginalBytes.Length -
             (caveAddress + code.Count + 4)))));

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
                if (!float.IsFinite(originalVeterancy) || originalVeterancy is < 0.0f or > 2.0f)
                    continue;
                eliteUnitStates[pointer] = new EliteUnitState(id, originalVeterancy);
            }
            if (ReadSingle(pointer + TechnoVeterancyOffset) != 2.0f)
                WriteSingle(pointer + TechnoVeterancyOffset, 2.0f);
            affected++;
        }
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
        foreach (var (pointer, state) in eliteUnitStates)
        {
            if (ReadInt32(pointer + 0x10) != state.Id ||
                ReadUInt32(pointer + TechnoOwnerOffset) != eliteUnitsHouse)
                continue;
            WriteSingle(pointer + TechnoVeterancyOffset, state.OriginalVeterancy);
            restored++;
        }
        return restored;
    }

}
