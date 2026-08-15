using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private void DisableBuildAnywhere()
    {
        if (!buildAnywhereEnabled)
            return;
        if (buildAnywhereWaterCodeCave != 0)
            RestoreHook(BuildAnywhereWater, BuildAnywhereWaterOriginalBytes,
                ref buildAnywhereWaterCodeCave);
        if (buildAnywhereGroundCodeCave != 0)
            RestoreHook(BuildAnywhereGround, BuildAnywhereGroundOriginalBytes,
                ref buildAnywhereGroundCodeCave);
        if (originalProximityCheck is not null)
            WriteCode(PassesProximityCheck, originalProximityCheck);
        buildAnywhereEnabled = false;
        originalProximityCheck = null;
        Console.WriteLine("[随地建造已关闭] 已恢复游戏原始范围检查。");
    }

    private void ToggleBuildAnywhere()
    {
        if (buildAnywhereEnabled)
        {
            DisableBuildAnywhere();
            return;
        }
        VerifyOriginalCode(BuildAnywhereGround, BuildAnywhereGroundOriginalBytes, "随意建筑陆地");
        VerifyOriginalCode(BuildAnywhereWater, BuildAnywhereWaterOriginalBytes, "随意建筑水面");
        buildAnywhereGroundCodeCave = InstallOwnedHook(BuildAnywhereGround,
            BuildAnywhereGroundOriginalBytes, cave => CreateAbsoluteJump(cave, 0x4A9063, "B801000000"),
            "随意建筑陆地");
        try
        {
            buildAnywhereWaterCodeCave = InstallOwnedHook(BuildAnywhereWater,
                BuildAnywhereWaterOriginalBytes, CreateBuildAnywhereWaterHook, "随意建筑水面");
            buildAnywhereEnabled = true;
        }
        catch
        {
            RestoreHook(BuildAnywhereGround, BuildAnywhereGroundOriginalBytes,
                ref buildAnywhereGroundCodeCave);
            throw;
        }
    }

    private byte[] CreateBuildAnywhereWaterHook(long cave)
    {
        var code = new List<byte>();
        code.AddRange(Convert.FromHexString("8B4C242483F900740B3B0D"));
        code.AddRange(BitConverter.GetBytes(checked((uint)CurrentPlayer)));
        code.AddRange(Convert.FromHexString("7503B9030000008B4C241C83F9FFE9"));
        code.AddRange(BitConverter.GetBytes(checked((int)
            (BuildAnywhereWater + BuildAnywhereWaterOriginalBytes.Length - (cave + code.Count + 4)))));
        return [.. code];
    }

    private byte[] CreateAbsoluteJump(long cave, long target, string prefix)
    {
        var code = new List<byte>();
        code.AddRange(Convert.FromHexString(prefix));
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(checked((int)(target - (cave + code.Count + 4)))));
        return [.. code];
    }

    private void ToggleInvadeMode()
    {
        if (invadeModeEnabled)
        {
            WriteCode(InvadeMode, InvadeModeOriginalBytes);
            invadeModeEnabled = false;
            return;
        }
        VerifyOriginalCode(InvadeMode, InvadeModeOriginalBytes, "侵略模式");
        WriteRelativeJump(InvadeMode, InvadeModeOriginalBytes.Length, 0x6F8604);
        invadeModeEnabled = true;
    }

    private void DisableInvadeMode()
    {
        if (!invadeModeEnabled)
            return;
        WriteCode(InvadeMode, InvadeModeOriginalBytes);
        invadeModeEnabled = false;
    }

    private void ToggleGamePause()
    {
        if (gamePaused)
        {
            WriteCode(LogicUpdateCall, LogicUpdateCallOriginalBytes);
            gamePaused = false;
            return;
        }
        VerifyOriginalCode(LogicUpdateCall, LogicUpdateCallOriginalBytes, "暂停游戏");
        WriteCode(LogicUpdateCall, Enumerable.Repeat((byte)0x90,
            LogicUpdateCallOriginalBytes.Length).ToArray());
        gamePaused = true;
    }

    private void DisableGamePause()
    {
        if (!gamePaused)
            return;
        WriteCode(LogicUpdateCall, LogicUpdateCallOriginalBytes);
        gamePaused = false;
    }

    private void VerifyOriginalCode(long address, byte[] originalBytes, string feature)
    {
        if (!ReadBytes(address, originalBytes.Length).AsSpan().SequenceEqual(originalBytes))
            throw new InvalidOperationException($"{feature}地址 0x{address:X} 指纹不匹配，未修改游戏代码。");
    }

    private void WriteRelativeJump(long address, int length, long target)
    {
        var jump = Enumerable.Repeat((byte)0x90, length).ToArray();
        jump[0] = 0xE9;
        BitConverter.GetBytes(checked((int)(target - (address + 5)))).CopyTo(jump, 1);
        WriteCode(address, jump);
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

}
