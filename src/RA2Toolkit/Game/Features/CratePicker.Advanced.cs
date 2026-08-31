using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private const int BuildingBeingProducedOffset = 0x6D4;
    private const int BuildingActuallyPlacedOnMapOffset = 0x6E4;
    private int autoRepairScanCursor;
    private uint autoRepairScanHouse;

    private void DisableBuildAnywhere()
    {
        if (!buildAnywhereEnabled)
            return;
        if (buildAnywhereWaterCodeCave != 0)
            RetireBuildAnywhereHook(BuildAnywhereWater, BuildAnywhereWaterOriginalBytes,
                ref buildAnywhereWaterCodeCave);
        if (buildAnywhereGroundCodeCave != 0)
            RetireBuildAnywhereHook(BuildAnywhereGround, BuildAnywhereGroundOriginalBytes,
                ref buildAnywhereGroundCodeCave);
        buildAnywhereEnabled = false;
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
            RetireBuildAnywhereHook(BuildAnywhereGround, BuildAnywhereGroundOriginalBytes,
                ref buildAnywhereGroundCodeCave);
            throw;
        }
    }

    private byte[] CreateBuildAnywhereWaterHook(long cave) =>
        CreateBuildAnywhereWaterHookCode(cave, CurrentPlayer,
            BuildAnywhereWater + BuildAnywhereWaterOriginalBytes.Length);

    internal static byte[] CreateBuildAnywhereWaterHookCode(
        long cave, long currentPlayerAddress, long resumeAddress)
    {
        var code = new List<byte>();
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        var shortFixups = new List<(int Offset, string Label)>();

        void Label(string name) => labels.Add(name, code.Count);
        void ShortJump(byte opcode, string label)
        {
            code.Add(opcode);
            shortFixups.Add((code.Count, label));
            code.Add(0);
        }

        code.AddRange(Convert.FromHexString("8B4C242483F900")); // candidate HouseClass*
        ShortJump(0x74, "original-mov");
        code.AddRange(Convert.FromHexString("3B0D"));
        code.AddRange(BitConverter.GetBytes(checked((uint)currentPlayerAddress)));
        ShortJump(0x75, "original-mov");
        code.AddRange(Convert.FromHexString("B903000000")); // current player may build on water
        ShortJump(0xEB, "compare");

        Label("original-mov");
        code.AddRange(Convert.FromHexString("8B4C241C")); // preserve the original path
        Label("compare");
        code.AddRange(Convert.FromHexString("83F9FF"));
        code.Add(0xE9);
        code.AddRange(BitConverter.GetBytes(checked((int)
            (resumeAddress - (cave + code.Count + 4)))));

        foreach (var (offset, label) in shortFixups)
        {
            var displacement = labels[label] - (offset + 1);
            if (displacement is < sbyte.MinValue or > sbyte.MaxValue)
                throw new InvalidOperationException("随处建造水面补丁的短跳转超出范围。");
            code[offset] = unchecked((byte)(sbyte)displacement);
        }
        return [.. code];
    }

    private void RetireBuildAnywhereHook(long address, byte[] originalBytes, ref nint cave)
    {
        RestoreHook(address, originalBytes, ref cave);
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
        var installed = CreateRelativePatch(InvadeMode,
            InvadeModeOriginalBytes.Length, 0x6F8604);
        if (invadeModeEnabled)
        {
            if (RestoreOwnedCodePatch(InvadeMode, InvadeModeOriginalBytes, installed))
                invadeModeEnabled = false;
            return;
        }

        var mayBePublished = false;
        try
        {
            PublishCodePatch(InvadeMode, InvadeModeOriginalBytes, installed,
                "侵略模式", ref mayBePublished);
        }
        finally
        {
            invadeModeEnabled = mayBePublished;
        }
    }

    private void DisableInvadeMode()
    {
        if (!invadeModeEnabled)
            return;
        var installed = CreateRelativePatch(InvadeMode,
            InvadeModeOriginalBytes.Length, 0x6F8604);
        if (RestoreOwnedCodePatch(InvadeMode, InvadeModeOriginalBytes, installed))
            invadeModeEnabled = false;
    }

    private void ToggleGamePause()
    {
        var installed = Enumerable.Repeat((byte)0x90,
            LogicUpdateCallOriginalBytes.Length).ToArray();
        if (gamePaused)
        {
            if (RestoreOwnedCodePatch(LogicUpdateCall, LogicUpdateCallOriginalBytes, installed))
                gamePaused = false;
            return;
        }

        var mayBePublished = false;
        try
        {
            PublishCodePatch(LogicUpdateCall, LogicUpdateCallOriginalBytes, installed,
                "暂停游戏", ref mayBePublished);
        }
        finally
        {
            gamePaused = mayBePublished;
        }
    }

    private void DisableGamePause()
    {
        if (!gamePaused)
            return;
        var installed = Enumerable.Repeat((byte)0x90,
            LogicUpdateCallOriginalBytes.Length).ToArray();
        if (RestoreOwnedCodePatch(LogicUpdateCall, LogicUpdateCallOriginalBytes, installed))
            gamePaused = false;
    }

    private void VerifyOriginalCode(long address, byte[] originalBytes, string feature)
    {
        if (!ReadBytes(address, originalBytes.Length).AsSpan().SequenceEqual(originalBytes))
            throw new InvalidOperationException($"{feature}地址 0x{address:X} 指纹不匹配，未修改游戏代码。");
    }

    private void ToggleAutoRepair()
    {
        autoRepairEnabled = !autoRepairEnabled;
        nextAutoRepairAt = DateTime.MinValue;
        autoRepairScanCursor = 0;
        autoRepairScanHouse = 0;
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
        if (house != autoRepairScanHouse)
        {
            autoRepairScanHouse = house;
            autoRepairScanCursor = 0;
        }

        var buildings = ReadPointerTable(house + HouseBuildingsOffset, 4096).ToArray();
        if (buildings.Length == 0)
        {
            autoRepairScanCursor = 0;
            return;
        }

        var start = autoRepairScanCursor % buildings.Length;
        var queued = 0;
        var examined = 0;
        while (examined < buildings.Length && queued < 4)
        {
            var building = buildings[(start + examined) % buildings.Length];
            examined++;
            if (building == 0 || !VectorContains(BuildingArray, building))
                continue;
            var id = ReadInt32(building + 0x10);
            if (!IsValidAutoRepairBuilding(building, house, id))
                continue;
            var buildingType = ReadUInt32(building + BuildingTypeOffset);
            if (buildingType == 0)
                continue;
            var health = ReadInt32(building + ObjectHealthOffset);
            var strength = ReadInt32(buildingType + ObjectTypeStrengthOffset);
            if (health <= 0 || strength <= 0 || health >= strength ||
                ReadByte(building + BuildingIsBeingRepairedOffset) != 0)
                continue;

            // Revalidate identity and lifecycle immediately before publishing an event.
            if (!IsValidAutoRepairBuilding(building, house, id))
                continue;
            try
            {
                if (!QueueRepair(id))
                {
                    nextAutoRepairAt = now + TimeSpan.FromMilliseconds(100);
                    break;
                }
                queued++;
            }
            catch (InvalidOperationException)
            {
                // The event queue can fill between scans. Retry fairly on the next tick.
                nextAutoRepairAt = now + TimeSpan.FromMilliseconds(100);
                break;
            }
        }

        autoRepairScanCursor = (start + Math.Max(1, examined)) % buildings.Length;
    }

    private bool IsValidAutoRepairBuilding(uint building, uint house, int expectedId) =>
        building != 0 &&
        expectedId > 0 &&
        VectorContains(BuildingArray, building) &&
        ReadInt32(building + 0x10) == expectedId &&
        ReadUInt32(building + TechnoOwnerOffset) == house &&
        ReadByte(building + ObjectIsOnMapOffset) != 0 &&
        ReadByte(building + ObjectIsAliveOffset) != 0 &&
        ReadByte(building + ObjectInLimboOffset) == 0 &&
        ReadByte(building + BuildingBeingProducedOffset) == 0 &&
        ReadByte(building + BuildingActuallyPlacedOnMapOffset) != 0;

    private bool QueueRepair(int buildingId)
    {
        var eventData = CreateEvent(0x15); // EventType::Repair
        BitConverter.GetBytes(buildingId).CopyTo(eventData, 7);
        eventData[11] = 52; // AbstractType::Abstract
        return TryEnqueueEvent(eventData);
    }

    private void ToggleSuperWeaponNoCooldown()
    {
        superWeaponNoCooldownEnabled = !superWeaponNoCooldownEnabled;
        nextSuperWeaponRefreshAt = DateTime.MinValue;
        Console.WriteLine(superWeaponNoCooldownEnabled
            ? "[超级武器无冷却已开启] 已拥有的超级武器会持续进入就绪状态。"
            : paratrooperNoCooldownEnabled
                ? "[超级武器无冷却已关闭] 伞兵仍由“伞兵无冷却”保持就绪，其他超级武器恢复正常冷却。"
                : "[超级武器无冷却已关闭] 后续冷却由游戏正常管理。");
    }

    private void MaintainSuperWeaponNoCooldown()
    {
        var now = DateTime.UtcNow;
        if (now < nextSuperWeaponRefreshAt)
            return;
        nextSuperWeaponRefreshAt = now + TimeSpan.FromMilliseconds(100);

        WithSuspendedProcess(() =>
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            var currentFrame = ReadInt32(CurrentFrame);
            foreach (var super in ReadVector(house + HouseSupersOffset, 256))
            {
                if (ReadByte(super + SuperIsPresentOffset) == 0 ||
                    ReadByte(super + SuperIsSuspendedOffset) != 0)
                    continue;
                var typeKind = int.MinValue;
                if (!superWeaponNoCooldownEnabled)
                {
                    var type = ReadUInt32(super + SuperTypeOffset);
                    if (type == 0)
                        continue;
                    typeKind = ReadInt32(type + SuperWeaponTypeKindOffset);
                }
                if (!ShouldRefreshSuperWeapon(superWeaponNoCooldownEnabled,
                        paratrooperNoCooldownEnabled, typeKind))
                    continue;
                WriteInt32(super + SuperRechargeStartOffset, currentFrame - 1);
                WriteInt32(super + SuperRechargeTimeLeftOffset, 0);
            }
        });
    }

    private void ToggleParatrooperNoCooldown()
    {
        paratrooperNoCooldownEnabled = !paratrooperNoCooldownEnabled;
        nextSuperWeaponRefreshAt = DateTime.MinValue;
        Console.WriteLine(paratrooperNoCooldownEnabled
            ? "[伞兵无冷却已开启] 普通伞兵和美国伞兵会持续进入就绪状态。"
            : superWeaponNoCooldownEnabled
                ? "[伞兵无冷却已关闭] “超级武器无冷却”仍会让伞兵保持就绪。"
                : "[伞兵无冷却已关闭] 后续冷却由游戏正常管理。");
    }

    internal static bool IsParatrooperSuperWeaponType(int type) =>
        type is ParaDropSuperWeaponType or AmericanParaDropSuperWeaponType;

    internal static bool ShouldRefreshSuperWeapon(
        bool allSuperWeapons, bool paratroopers, int type) =>
        allSuperWeapons || paratroopers && IsParatrooperSuperWeaponType(type);

    private void StopTimer(long timerAddress)
    {
        WriteInt32(timerAddress, -1);
        WriteInt32(timerAddress + 8, 0);
    }

}
