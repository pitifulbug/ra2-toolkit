using System.ComponentModel;
using System.Runtime.InteropServices;

internal sealed partial class CratePicker
{
    private const long SellCursorCheck = 0x6929D1;
    private const long SellOwnershipCheck = 0x4C6F48;
    private const long SellOwnershipAllowedPath = 0x4C6F9C;
    private const long SellBuilderCheck = 0x44711B;
    private const long SellCursorTerrainPath = 0x6929D8;
    private const long SellCursorAllowedPath = 0x692A20;
    private const long ReceiveDamageHealthUpdate = 0x5F5509;
    private const long TemporalEraseCall = 0x71AE9D;
    private const long TemporalEraseProtectedPath = 0x71AE5D;
    private const long TemporalEraseReturnPath = 0x71AEA4;
    private const long ScenarioClassInstance = 0xA8B230;
    private const long TechnoTypeArray = 0xA8EB00;
    private const int ScenarioFreeRadarOffset = 0x34A4;
    private const int HouseRecheckRadarOffset = 0x5779;
    private const int HouseSpySatelliteActiveOffset = 0x577A;
    private const int TechnoTypeDetectDisguiseOffset = 0xD31;
    private const int TechnoTypeImmuneToPsionicsOffset = 0xD35;
    private const int TechnoTypeImmuneToPsionicWeaponsOffset = 0xD36;
    private static readonly byte[] SellCursorOriginal = Convert.FromHexString("8B068BCEFF503C");
    private static readonly byte[] SellOwnershipOriginal = Convert.FromHexString("0F85BB110000");
    private static readonly byte[] SellBuilderOriginal = Convert.FromHexString("0F84A4000000");
    private static readonly byte[] SellBuilderPatch = Convert.FromHexString("909090909090");
    private static readonly byte[] ReceiveDamageOriginal = Convert.FromHexString("2BC285C089466C");
    private static readonly byte[] TemporalEraseOriginal = Convert.FromHexString("8B068BCEFF502C");

    private bool godModeEnabled;
    private bool godModePatchInstalled;
    private nint godModeCodeCave;
    private bool godModeTemporalPatchInstalled;
    private nint godModeTemporalCodeCave;
    private bool sellEverythingEnabled;
    private bool sellCursorPatchInstalled;
    private nint sellCursorCodeCave;
    private bool sellOwnershipPatchInstalled;
    private bool sellBuilderPatchInstalled;
    private bool spySatelliteAndRadarEnabled;
    private uint radarHouse;
    private uint radarScenario;
    private byte originalSpySatelliteActive;
    private byte originalFreeRadar;
    private bool radarOwnsReveal;
    private bool detectDisguisesEnabled;
    private bool mindControlImmunityEnabled;
    private readonly Dictionary<uint, byte> detectDisguiseTypeStates = [];
    private readonly Dictionary<uint, (byte Psionics, byte PsionicWeapons)> mindImmunityTypeStates = [];
    private DateTime nextOwnedTypeRefreshAt = DateTime.MinValue;

    private int? SetMoneyAmount(OverlayCommandRequest request)
    {
        var amount = RequireWholeNumber(request, 0, 2_000_000_000, "金钱数量");
        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            throw new InvalidOperationException("当前玩家阵营指针无效。");
        if (infiniteMoneyEnabled)
            DisableInfiniteMoney();
        WithSuspendedProcess(() =>
        {
            if (ReadUInt32(CurrentPlayer) != house)
                throw new InvalidOperationException("当前玩家阵营已经变化，未修改金钱。");
            WriteInt32(house + HouseBalanceOffset, amount);
        });
        return amount;
    }

    private void ToggleGodMode()
    {
        if (godModeEnabled || godModePatchInstalled || godModeTemporalPatchInstalled)
        {
            DisableGodMode();
            return;
        }

        const int caveSize = 64;
        var cave = Native.VirtualAllocEx(handle, 0, caveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (cave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配无敌模式代码区失败");
        var temporalCave = Native.VirtualAllocEx(handle, 0, caveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (temporalCave == 0)
        {
            FreeUnpublishedCodeCaveBestEffort(cave);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配无敌模式超时空代码区失败");
        }

        var damageMayBePublished = false;
        var temporalMayBePublished = false;
        try
        {
            var code = new List<byte>(40)
            {
                0x51,                         // push ecx
                0x8B, 0x8E, 0x1C, 0x02, 0x00, 0x00, // mov ecx,[esi+21C]
                0x3B, 0x0D                    // cmp ecx,[CurrentPlayer]
            };
            code.AddRange(BitConverter.GetBytes(checked((uint)CurrentPlayer)));
            code.AddRange([
                0x59,             // pop ecx
                0x75, 0x02,       // jne applyDamage
                0xEB, 0x02,       // jmp updateHealth
                0x2B, 0xC2,       // applyDamage: sub eax,edx
                0x85, 0xC0,       // updateHealth: test eax,eax
                0x89, 0x46, 0x6C, // mov [esi+6C],eax
                0xE9
            ]);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (ReceiveDamageHealthUpdate + ReceiveDamageOriginal.Length -
                 (cave.ToInt64() + code.Count + sizeof(int))))));
            WriteCodeCave(cave, [.. code]);
            var damagePatch = CreateRelativePatch(ReceiveDamageHealthUpdate,
                ReceiveDamageOriginal.Length, cave.ToInt64());
            var temporalCode = new List<byte>(48)
            {
                0x50,                               // push eax
                0x8B, 0x86, 0x1C, 0x02, 0x00, 0x00, // mov eax,[esi+21C]
                0x3B, 0x05                          // cmp eax,[CurrentPlayer]
            };
            temporalCode.AddRange(BitConverter.GetBytes(checked((uint)CurrentPlayer)));
            temporalCode.Add(0x58);                 // pop eax (preserves flags)
            temporalCode.AddRange([0x0F, 0x84]);    // je protectedPath
            var protectedFixup = temporalCode.Count;
            temporalCode.AddRange(new byte[4]);
            temporalCode.AddRange(TemporalEraseOriginal);
            temporalCode.Add(0xE9);
            temporalCode.AddRange(BitConverter.GetBytes(checked((int)
                (TemporalEraseReturnPath -
                 (temporalCave.ToInt64() + temporalCode.Count + sizeof(int))))));
            var protectedLabel = temporalCode.Count;
            temporalCode.Add(0xE9);
            temporalCode.AddRange(BitConverter.GetBytes(checked((int)
                (TemporalEraseProtectedPath -
                 (temporalCave.ToInt64() + temporalCode.Count + sizeof(int))))));
            var protectedDisplacement = BitConverter.GetBytes(
                protectedLabel - (protectedFixup + sizeof(int)));
            for (var index = 0; index < protectedDisplacement.Length; index++)
                temporalCode[protectedFixup + index] = protectedDisplacement[index];
            WriteCodeCave(temporalCave, [.. temporalCode]);
            var temporalPatch = CreateRelativePatch(TemporalEraseCall,
                TemporalEraseOriginal.Length, temporalCave.ToInt64());

            PublishCodePatch(ReceiveDamageHealthUpdate, ReceiveDamageOriginal, damagePatch,
                "无敌模式伤害保护", ref damageMayBePublished);
            PublishCodePatch(TemporalEraseCall, TemporalEraseOriginal, temporalPatch,
                "无敌模式超时空保护", ref temporalMayBePublished);
            godModeCodeCave = cave;
            godModePatchInstalled = damageMayBePublished;
            godModeTemporalCodeCave = temporalCave;
            godModeTemporalPatchInstalled = temporalMayBePublished;
            godModeEnabled = true;
        }
        catch
        {
            if (temporalMayBePublished)
            {
                var patch = CreateRelativePatch(TemporalEraseCall,
                    TemporalEraseOriginal.Length, temporalCave.ToInt64());
                if (TryRollbackFailedCodePatch(TemporalEraseCall,
                        TemporalEraseOriginal, patch))
                    RetireCodeCave(temporalCave);
                else
                {
                    godModeTemporalCodeCave = temporalCave;
                    godModeTemporalPatchInstalled = true;
                }
            }
            else
                FreeUnpublishedCodeCaveBestEffort(temporalCave);
            if (damageMayBePublished)
            {
                var patch = CreateRelativePatch(ReceiveDamageHealthUpdate,
                    ReceiveDamageOriginal.Length, cave.ToInt64());
                if (TryRollbackFailedCodePatch(ReceiveDamageHealthUpdate,
                        ReceiveDamageOriginal, patch))
                    RetireCodeCave(cave);
                else
                {
                    godModeCodeCave = cave;
                    godModePatchInstalled = true;
                }
            }
            else
                FreeUnpublishedCodeCaveBestEffort(cave);
            godModeEnabled = godModePatchInstalled || godModeTemporalPatchInstalled;
            throw;
        }
    }

    private void DisableGodMode()
    {
        if (!godModePatchInstalled && !godModeTemporalPatchInstalled)
        {
            godModeEnabled = false;
            return;
        }
        var failed = false;
        if (godModeTemporalPatchInstalled)
        {
            var temporalPatch = CreateRelativePatch(TemporalEraseCall,
                TemporalEraseOriginal.Length, godModeTemporalCodeCave.ToInt64());
            if (RestoreOwnedCodePatch(TemporalEraseCall, TemporalEraseOriginal, temporalPatch))
                godModeTemporalPatchInstalled = false;
            else
                failed = true;
        }
        if (!godModeTemporalPatchInstalled && godModeTemporalCodeCave != 0)
            RetireCodeCave(ref godModeTemporalCodeCave);

        if (godModePatchInstalled)
        {
            var damagePatch = CreateRelativePatch(ReceiveDamageHealthUpdate,
                ReceiveDamageOriginal.Length, godModeCodeCave.ToInt64());
            if (RestoreOwnedCodePatch(ReceiveDamageHealthUpdate, ReceiveDamageOriginal, damagePatch))
                godModePatchInstalled = false;
            else
                failed = true;
        }
        if (!godModePatchInstalled && godModeCodeCave != 0)
            RetireCodeCave(ref godModeCodeCave);

        godModeEnabled = godModePatchInstalled || godModeTemporalPatchInstalled;
        if (failed)
            throw new InvalidOperationException("无敌模式补丁已被其他程序修改，未覆盖外部更改。");
    }

    private void ToggleSellEverything()
    {
        if (sellEverythingEnabled || sellCursorPatchInstalled ||
            sellOwnershipPatchInstalled || sellBuilderPatchInstalled)
        {
            DisableSellEverything();
            return;
        }

        const int caveSize = 64;
        var cave = Native.VirtualAllocEx(handle, 0, caveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (cave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配随意出售代码区失败");

        try
        {
            var code = new List<byte>(40);
            code.AddRange(SellCursorOriginal);       // call WhatAmI()
            code.AddRange([0x83, 0xF8, 0x24]);       // cmp eax,Terrain
            code.AddRange([0x0F, 0x84]);             // je terrainPath
            var terrainFixup = code.Count;
            code.AddRange(new byte[4]);
            code.AddRange([0xB8, 0x0C, 0x00, 0x00, 0x00]); // mov eax,Sell
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (SellCursorAllowedPath - (cave.ToInt64() + code.Count + sizeof(int))))));
            var terrainLabel = code.Count;
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (SellCursorTerrainPath - (cave.ToInt64() + code.Count + sizeof(int))))));
            var terrainDisplacement = BitConverter.GetBytes(
                terrainLabel - (terrainFixup + sizeof(int)));
            for (var index = 0; index < terrainDisplacement.Length; index++)
                code[terrainFixup + index] = terrainDisplacement[index];
            WriteCodeCave(cave, [.. code]);

            var cursorPatch = CreateRelativePatch(SellCursorCheck,
                SellCursorOriginal.Length, cave.ToInt64());
            PublishCodePatch(SellCursorCheck, SellCursorOriginal, cursorPatch,
                "随意出售光标", ref sellCursorPatchInstalled);
            sellCursorCodeCave = cave;
            var ownershipPatch = CreateRelativePatch(SellOwnershipCheck,
                SellOwnershipOriginal.Length, SellOwnershipAllowedPath);
            PublishCodePatch(SellOwnershipCheck, SellOwnershipOriginal, ownershipPatch,
                "随意出售所有权检查", ref sellOwnershipPatchInstalled);
            PublishCodePatch(SellBuilderCheck, SellBuilderOriginal, SellBuilderPatch,
                "随意出售建造者检查", ref sellBuilderPatchInstalled);
            sellEverythingEnabled = true;
        }
        catch
        {
            var cursorWasPossiblyPublished = sellCursorPatchInstalled;
            if (sellCursorPatchInstalled && sellCursorCodeCave == 0)
                sellCursorCodeCave = cave;
            var freeUnpublishedCave = !sellCursorPatchInstalled && sellCursorCodeCave == 0;
            try { DisableSellEverything(); }
            catch
            {
            }
            if (freeUnpublishedCave)
                FreeUnpublishedCodeCaveBestEffort(cave);
            if (!sellCursorPatchInstalled && sellCursorCodeCave != 0)
            {
                if (cursorWasPossiblyPublished)
                    RetireCodeCave(ref sellCursorCodeCave);
                else
                {
                    FreeUnpublishedCodeCaveBestEffort(sellCursorCodeCave);
                    sellCursorCodeCave = 0;
                }
            }
            throw;
        }
    }

    private void DisableSellEverything()
    {
        var failed = false;
        if (sellBuilderPatchInstalled)
        {
            if (RestoreOwnedCodePatch(SellBuilderCheck, SellBuilderOriginal, SellBuilderPatch))
                sellBuilderPatchInstalled = false;
            else
                failed = true;
        }
        if (sellOwnershipPatchInstalled)
        {
            var ownershipPatch = CreateRelativePatch(SellOwnershipCheck,
                SellOwnershipOriginal.Length, SellOwnershipAllowedPath);
            if (RestoreOwnedCodePatch(SellOwnershipCheck, SellOwnershipOriginal, ownershipPatch))
                sellOwnershipPatchInstalled = false;
            else
                failed = true;
        }
        if (sellCursorPatchInstalled)
        {
            var cursorPatch = CreateRelativePatch(SellCursorCheck,
                SellCursorOriginal.Length, sellCursorCodeCave.ToInt64());
            if (RestoreOwnedCodePatch(SellCursorCheck, SellCursorOriginal, cursorPatch))
                sellCursorPatchInstalled = false;
            else
                failed = true;
        }
        if (!sellCursorPatchInstalled && sellCursorCodeCave != 0)
            RetireCodeCave(ref sellCursorCodeCave);
        sellEverythingEnabled = sellCursorPatchInstalled || sellOwnershipPatchInstalled ||
            sellBuilderPatchInstalled;
        if (failed)
            throw new InvalidOperationException("随意出售补丁已被其他程序修改，未覆盖外部更改。");
    }

    private void ToggleSpySatelliteAndRadar()
    {
        if (spySatelliteAndRadarEnabled)
        {
            DisableSpySatelliteAndRadar();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        var scenario = ReadUInt32(ScenarioClassInstance);
        if (house == 0 || scenario == 0)
            throw new InvalidOperationException("当前对局的阵营或场景指针无效。");
        originalSpySatelliteActive = ReadByte(house + HouseSpySatelliteActiveOffset);
        originalFreeRadar = ReadByte(scenario + ScenarioFreeRadarOffset);
        var shouldReveal = !revealMapEnabled;
        var revealCompleted = false;
        try
        {
            WithSuspendedProcess(() =>
            {
                if (ReadUInt32(CurrentPlayer) != house ||
                    ReadUInt32(ScenarioClassInstance) != scenario)
                    throw new InvalidOperationException("对局状态已经变化，未开启雷达。");
                try
                {
                    WriteBytes(house + HouseSpySatelliteActiveOffset, [1]);
                    WriteBytes(scenario + ScenarioFreeRadarOffset, [1]);
                    WriteBytes(house + HouseRecheckRadarOffset, [1]);
                }
                catch (Exception writeError) when (writeError is Win32Exception or
                                                   InvalidOperationException)
                {
                    try
                    {
                        WriteBytes(house + HouseSpySatelliteActiveOffset,
                            [originalSpySatelliteActive]);
                        WriteBytes(scenario + ScenarioFreeRadarOffset, [originalFreeRadar]);
                        WriteBytes(house + HouseRecheckRadarOffset, [1]);
                    }
                    catch (Exception rollbackError) when (rollbackError is Win32Exception or
                                                          InvalidOperationException)
                    {
                        throw new InvalidOperationException(
                            "雷达状态写入失败，且未能完整回滚。",
                            new AggregateException(writeError, rollbackError));
                    }
                    throw new InvalidOperationException("雷达状态写入失败，已恢复原值。", writeError);
                }
            });
            if (shouldReveal)
            {
                InvokeRevealMapLikeCrate(house);
                revealCompleted = true;
            }
            radarHouse = house;
            radarScenario = scenario;
            radarOwnsReveal = shouldReveal;
            spySatelliteAndRadarEnabled = true;
        }
        catch
        {
            var flagsRollbackFailed = false;
            try
            {
                if (ReadUInt32(CurrentPlayer) == house &&
                    ReadUInt32(ScenarioClassInstance) == scenario)
                {
                    WithSuspendedProcess(() =>
                    {
                        if (ReadByte(house + HouseSpySatelliteActiveOffset) == 1)
                            WriteBytes(house + HouseSpySatelliteActiveOffset,
                                [originalSpySatelliteActive]);
                        if (ReadByte(scenario + ScenarioFreeRadarOffset) == 1)
                            WriteBytes(scenario + ScenarioFreeRadarOffset, [originalFreeRadar]);
                        WriteBytes(house + HouseRecheckRadarOffset, [1]);
                    });
                }
            }
            catch (Exception rollbackError) when (rollbackError is Win32Exception or
                                                  InvalidOperationException or
                                                  GameProcessExitedException)
            {
                flagsRollbackFailed = true;
                Console.Error.WriteLine($"[雷达状态回滚失败] {rollbackError.Message}");
            }
            var revealRollbackFailed = false;
            if (revealCompleted && !revealMapEnabled)
            {
                try { InvokeHouseReshroudMap(house); }
                catch (Exception reshroudError) when (reshroudError is Win32Exception or
                                                      InvalidOperationException or
                                                      GameProcessExitedException)
                {
                    revealRollbackFailed = true;
                    Console.Error.WriteLine($"[雷达视野回滚失败] {reshroudError.Message}");
                }
            }
            if (flagsRollbackFailed || revealRollbackFailed)
            {
                radarHouse = house;
                radarScenario = scenario;
                radarOwnsReveal = revealRollbackFailed;
                spySatelliteAndRadarEnabled = true;
            }
            else
                radarOwnsReveal = false;
            throw;
        }
    }

    private void DisableSpySatelliteAndRadar()
    {
        if (!spySatelliteAndRadarEnabled)
            return;
        if (ReadUInt32(CurrentPlayer) == radarHouse &&
            ReadUInt32(ScenarioClassInstance) == radarScenario)
        {
            WithSuspendedProcess(() =>
            {
                if (ReadByte(radarHouse + HouseSpySatelliteActiveOffset) == 1)
                    WriteBytes(radarHouse + HouseSpySatelliteActiveOffset,
                        [originalSpySatelliteActive]);
                if (ReadByte(radarScenario + ScenarioFreeRadarOffset) == 1)
                    WriteBytes(radarScenario + ScenarioFreeRadarOffset, [originalFreeRadar]);
                WriteBytes(radarHouse + HouseRecheckRadarOffset, [1]);
            });
            if (radarOwnsReveal && !revealMapEnabled)
                InvokeHouseReshroudMap(radarHouse);
        }
        spySatelliteAndRadarEnabled = false;
        radarHouse = 0;
        radarScenario = 0;
        radarOwnsReveal = false;
    }

    private void ToggleDetectDisguises()
    {
        if (detectDisguisesEnabled)
        {
            RestoreOwnedTypeFlags(detectDisguiseTypeStates,
                TechnoTypeDetectDisguiseOffset);
            detectDisguisesEnabled = false;
            return;
        }
        detectDisguisesEnabled = true;
        nextOwnedTypeRefreshAt = DateTime.MinValue;
        MaintainOwnedTypeEnhancements();
    }

    private void ToggleMindControlImmunity()
    {
        if (mindControlImmunityEnabled)
        {
            RestoreMindImmunityTypeFlags();
            mindControlImmunityEnabled = false;
            return;
        }
        mindControlImmunityEnabled = true;
        nextOwnedTypeRefreshAt = DateTime.MinValue;
        MaintainOwnedTypeEnhancements();
    }

    private void MaintainOwnedTypeEnhancements()
    {
        var now = DateTime.UtcNow;
        if (now < nextOwnedTypeRefreshAt)
            return;
        nextOwnedTypeRefreshAt = now + TimeSpan.FromMilliseconds(500);
        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;

        WithSuspendedProcess(() =>
        {
            var buildings = ReadVector(BuildingArray, 4096).ToHashSet();
            var foot = ReadVector(FootArray, 10000).ToHashSet();
            var validTypes = ReadVector(TechnoTypeArray, 4096).ToHashSet();
            foreach (var techno in ReadVector(TechnoArray, 10000))
            {
                if (ReadUInt32(techno + TechnoOwnerOffset) != house ||
                    GetTechnoTypePointer(techno, buildings, foot) is not { } type ||
                    type == 0 || !validTypes.Contains(type))
                    continue;
                if (detectDisguisesEnabled && !detectDisguiseTypeStates.ContainsKey(type))
                {
                    detectDisguiseTypeStates[type] =
                        ReadByte(type + TechnoTypeDetectDisguiseOffset);
                    WriteBytes(type + TechnoTypeDetectDisguiseOffset, [1]);
                }
                if (mindControlImmunityEnabled && !mindImmunityTypeStates.ContainsKey(type))
                {
                    mindImmunityTypeStates[type] = (
                        ReadByte(type + TechnoTypeImmuneToPsionicsOffset),
                        ReadByte(type + TechnoTypeImmuneToPsionicWeaponsOffset));
                    WriteBytes(type + TechnoTypeImmuneToPsionicsOffset, [1]);
                    WriteBytes(type + TechnoTypeImmuneToPsionicWeaponsOffset, [1]);
                }
            }
        });
    }

    private uint? GetTechnoTypePointer(
        uint techno,
        IReadOnlySet<uint> buildings,
        IReadOnlySet<uint> foot)
    {
        if (buildings.Contains(techno))
            return ReadUInt32(techno + BuildingTypeOffset);
        if (foot.Contains(techno))
            return ReadUInt32(techno + UnitTypeOffset);
        return null;
    }

    private void RestoreOwnedTypeFlags(Dictionary<uint, byte> states, int offset)
    {
        if (states.Count == 0)
            return;
        WithSuspendedProcess(() =>
        {
            foreach (var (type, original) in states)
            {
                if (!VectorContains(TechnoTypeArray, type) || ReadByte(type + offset) != 1)
                    continue;
                WriteBytes(type + offset, [original]);
            }
        });
        states.Clear();
    }

    private void RestoreMindImmunityTypeFlags()
    {
        if (mindImmunityTypeStates.Count == 0)
            return;
        WithSuspendedProcess(() =>
        {
            foreach (var (type, original) in mindImmunityTypeStates)
            {
                if (!VectorContains(TechnoTypeArray, type))
                    continue;
                if (ReadByte(type + TechnoTypeImmuneToPsionicsOffset) == 1)
                    WriteBytes(type + TechnoTypeImmuneToPsionicsOffset, [original.Psionics]);
                if (ReadByte(type + TechnoTypeImmuneToPsionicWeaponsOffset) == 1)
                    WriteBytes(type + TechnoTypeImmuneToPsionicWeaponsOffset,
                        [original.PsionicWeapons]);
            }
        });
        mindImmunityTypeStates.Clear();
    }

    private void DisablePlayerEnhancements()
    {
        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                          GameProcessExitedException)
            {
                Console.Error.WriteLine($"[玩家功能清理失败] {error.Message}");
            }
        }

        Cleanup(DisableGodMode);
        Cleanup(DisableSellEverything);
        Cleanup(DisableSpySatelliteAndRadar);
        Cleanup(() => RestoreOwnedTypeFlags(detectDisguiseTypeStates,
            TechnoTypeDetectDisguiseOffset));
        Cleanup(RestoreMindImmunityTypeFlags);
        detectDisguisesEnabled = false;
        mindControlImmunityEnabled = false;
    }
}
