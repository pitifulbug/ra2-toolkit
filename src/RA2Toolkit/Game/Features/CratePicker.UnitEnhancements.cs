using System.ComponentModel;

internal sealed partial class CratePicker
{
    private const int TechnoAmmoOffset = 0x2FC;
    private const int TechnoTargetOffset = 0x2B4;
    private const int TechnoSpawnManagerOffset = 0x2D0;
    private const int SpawnManagerRegenRateOffset = 0x30;
    private const int SpawnManagerReloadRateOffset = 0x34;
    private const int SpawnManagerUpdateTimerTimeLeftOffset = 0x58;
    private const int SpawnManagerSpawnTimerTimeLeftOffset = 0x64;
    private const int FastSpawnReloadRate = 2;
    private const int TechnoTypeGuardRangeOffset = 0x5B8;
    private const int TechnoTypeAmmoOffset = 0x684;
    private const int ExtendedGuardRange = 100 * 256;
    private const int LargeAmmoAmount = 1000;

    private bool autoUnitRepairEnabled;
    private bool rapidAttackEnabled;
    private bool fastReloadEnabled;
    private bool largeAmmoEnabled;
    private bool extendedGuardRangeEnabled;
    private bool fastInfantryEnabled;
    private DateTime nextUnitEnhancementRefreshAt = DateTime.MinValue;
    private readonly Dictionary<CapturedUnit, int> largeAmmoStates = [];
    private readonly Dictionary<uint, int> guardRangeTypeStates = [];
    private readonly Dictionary<CapturedUnit, FastInfantryState> fastInfantryStates = [];
    private readonly Dictionary<uint, SpawnManagerState> spawnManagerStates = [];

    private void ToggleAutoUnitRepair() =>
        ToggleSimpleUnitFeature(ref autoUnitRepairEnabled);

    private void ToggleRapidAttack() =>
        ToggleSimpleUnitFeature(ref rapidAttackEnabled);

    private void ToggleFastReload()
    {
        if (fastReloadEnabled)
        {
            RestoreSpawnManagers();
            fastReloadEnabled = false;
            return;
        }
        fastReloadEnabled = true;
        nextUnitEnhancementRefreshAt = DateTime.MinValue;
        MaintainUnitEnhancements();
    }

    private void ToggleLargeAmmo()
    {
        if (largeAmmoEnabled)
        {
            RestoreLargeAmmo();
            largeAmmoEnabled = false;
            return;
        }
        largeAmmoEnabled = true;
        nextUnitEnhancementRefreshAt = DateTime.MinValue;
        MaintainUnitEnhancements();
    }

    private void ToggleExtendedGuardRange()
    {
        if (extendedGuardRangeEnabled)
        {
            RestoreGuardRanges();
            extendedGuardRangeEnabled = false;
            return;
        }
        extendedGuardRangeEnabled = true;
        nextUnitEnhancementRefreshAt = DateTime.MinValue;
        MaintainUnitEnhancements();
    }

    private void ToggleFastInfantry()
    {
        if (fastInfantryEnabled)
        {
            RestoreFastInfantry();
            fastInfantryEnabled = false;
            return;
        }
        fastInfantryEnabled = true;
        nextUnitEnhancementRefreshAt = DateTime.MinValue;
        MaintainUnitEnhancements();
    }

    private void ToggleSimpleUnitFeature(ref bool enabledFlag)
    {
        enabledFlag = !enabledFlag;
        nextUnitEnhancementRefreshAt = DateTime.MinValue;
        if (enabledFlag)
            MaintainUnitEnhancements();
    }

    private int PromoteSelectedUnits()
    {
        var selected = CaptureSelectedUnits();
        var affected = 0;
        WithSuspendedProcess(() =>
        {
            foreach (var unit in selected)
            {
                if (!IsCapturedUnitValid(unit))
                    continue;
                WriteSingle(unit.Pointer + TechnoVeterancyOffset, 2.0f);
                affected++;
            }
        });
        return affected;
    }

    private void MaintainUnitEnhancements()
    {
        var now = DateTime.UtcNow;
        if (now < nextUnitEnhancementRefreshAt)
            return;
        nextUnitEnhancementRefreshAt = now + TimeSpan.FromMilliseconds(100);
        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;

        WithSuspendedProcess(() =>
        {
            var footPointers = ReadVector(FootArray, 10000).ToHashSet();
            var buildingPointers = ReadVector(BuildingArray, 10000).ToHashSet();
            if (autoUnitRepairEnabled)
                RepairOwnedUnits(house, footPointers);
            foreach (var techno in ReadVector(TechnoArray, 10000))
            {
                if (ReadUInt32(techno + TechnoOwnerOffset) != house ||
                    ReadByte(techno + ObjectIsAliveOffset) == 0 ||
                    ReadByte(techno + ObjectInLimboOffset) != 0)
                    continue;
                var id = ReadInt32(techno + 0x10);
                if (id <= 0)
                    continue;
                var captured = new CapturedUnit(techno, id);
                if (rapidAttackEnabled)
                {
                    if (ReadInt32(techno + TechnoReloadTimerTimeLeftOffset) != 0)
                        WriteInt32(techno + TechnoReloadTimerTimeLeftOffset, 0);
                    if (ReadInt32(techno + TechnoRearmTimerTimeLeftOffset) != 0)
                        WriteInt32(techno + TechnoRearmTimerTimeLeftOffset, 0);
                }
                if (fastReloadEnabled &&
                    ReadInt32(techno + TechnoReloadTimerTimeLeftOffset) != 0)
                    WriteInt32(techno + TechnoReloadTimerTimeLeftOffset, 0);
                var isFoot = footPointers.Contains(techno);
                var type = GetTechnoTypePointer(techno, buildingPointers, footPointers);
                if (largeAmmoEnabled && isFoot && type is { } ammoType &&
                    ReadInt32(ammoType + TechnoTypeAmmoOffset) > 0)
                    ApplyLargeAmmo(captured);
                if (extendedGuardRangeEnabled && type is { } guardType)
                    ApplyExtendedGuardRange(guardType);
                if (fastReloadEnabled)
                    ApplyFastReload(captured);
            }
            if (fastInfantryEnabled)
                ApplyFastInfantry(house);
        });
    }

    private void RepairOwnedUnits(uint house, IReadOnlySet<uint> footPointers)
    {
        foreach (var unit in footPointers)
        {
            var type = ReadUInt32(unit + UnitTypeOffset);
            if (ReadUInt32(unit + TechnoOwnerOffset) != house ||
                ReadByte(unit + ObjectIsAliveOffset) == 0 ||
                ReadByte(unit + ObjectInLimboOffset) != 0 ||
                type == 0)
                continue;
            var strength = ReadInt32(type + ObjectTypeStrengthOffset);
            var health = ReadInt32(unit + ObjectHealthOffset);
            if (strength <= 0 || health <= 0 || health >= strength)
                continue;
            WriteInt32(unit + ObjectHealthOffset,
                Math.Min(strength, health + Math.Max(1, strength / 25)));
        }
    }

    private void ApplyLargeAmmo(CapturedUnit unit)
    {
        if (!largeAmmoStates.ContainsKey(unit))
            largeAmmoStates[unit] = ReadInt32(unit.Pointer + TechnoAmmoOffset);
        if (ReadInt32(unit.Pointer + TechnoAmmoOffset) != LargeAmmoAmount)
            WriteInt32(unit.Pointer + TechnoAmmoOffset, LargeAmmoAmount);
    }

    private void ApplyExtendedGuardRange(uint type)
    {
        if (!guardRangeTypeStates.ContainsKey(type))
            guardRangeTypeStates[type] = ReadInt32(type + TechnoTypeGuardRangeOffset);
        if (ReadInt32(type + TechnoTypeGuardRangeOffset) != ExtendedGuardRange)
            WriteInt32(type + TechnoTypeGuardRangeOffset, ExtendedGuardRange);
    }

    private void ApplyFastReload(CapturedUnit owner)
    {
        var manager = ReadUInt32(owner.Pointer + TechnoSpawnManagerOffset);
        if (manager == 0)
            return;
        var id = ReadInt32(manager + 0x10);
        if (id <= 0)
            return;
        if (!spawnManagerStates.TryGetValue(manager, out var state) ||
            state.Id != id || state.Owner != owner)
        {
            spawnManagerStates[manager] = new SpawnManagerState(owner, id,
                ReadInt32(manager + SpawnManagerRegenRateOffset),
                ReadInt32(manager + SpawnManagerReloadRateOffset));
        }
        WriteInt32(manager + SpawnManagerRegenRateOffset, FastSpawnReloadRate);
        WriteInt32(manager + SpawnManagerReloadRateOffset, FastSpawnReloadRate);
        WriteInt32(manager + SpawnManagerUpdateTimerTimeLeftOffset, 0);
        WriteInt32(manager + SpawnManagerSpawnTimerTimeLeftOffset, 0);
    }

    private void ApplyFastInfantry(uint house)
    {
        foreach (var pointer in ReadVector(InfantryArray, 4096))
        {
            if (ReadUInt32(pointer + TechnoOwnerOffset) != house)
                continue;
            var id = ReadInt32(pointer + 0x10);
            if (id <= 0)
                continue;
            var unit = new CapturedUnit(pointer, id);
            if (!fastInfantryStates.TryGetValue(unit, out var state))
            {
                var original = ReadDouble(pointer + FootSpeedMultiplierOffset);
                if (!IsReasonableSpeedMultiplier(original))
                    continue;
                state = new FastInfantryState(original, Math.Min(10.0, original * 3.0));
                fastInfantryStates[unit] = state;
            }
            if (ReadDouble(pointer + FootSpeedMultiplierOffset) != state.Applied)
                WriteBytes(pointer + FootSpeedMultiplierOffset,
                    BitConverter.GetBytes(state.Applied));
        }
    }

    private void RestoreLargeAmmo()
    {
        WithSuspendedProcess(() =>
        {
            foreach (var (unit, original) in largeAmmoStates)
                if (IsCapturedFootIdentityValid(unit) &&
                    ReadInt32(unit.Pointer + TechnoAmmoOffset) == LargeAmmoAmount)
                    WriteInt32(unit.Pointer + TechnoAmmoOffset, original);
        });
        largeAmmoStates.Clear();
    }

    private void RestoreGuardRanges()
    {
        WithSuspendedProcess(() =>
        {
            foreach (var (type, original) in guardRangeTypeStates)
                if (VectorContains(TechnoTypeArray, type) &&
                    ReadInt32(type + TechnoTypeGuardRangeOffset) == ExtendedGuardRange)
                    WriteInt32(type + TechnoTypeGuardRangeOffset, original);
        });
        guardRangeTypeStates.Clear();
    }

    private void RestoreFastInfantry()
    {
        WithSuspendedProcess(() =>
        {
            foreach (var (unit, state) in fastInfantryStates)
                if (IsCapturedFootIdentityValid(unit) &&
                    ReadDouble(unit.Pointer + FootSpeedMultiplierOffset) == state.Applied)
                    WriteBytes(unit.Pointer + FootSpeedMultiplierOffset,
                        BitConverter.GetBytes(state.Original));
        });
        fastInfantryStates.Clear();
    }

    private void RestoreSpawnManagers()
    {
        WithSuspendedProcess(() =>
        {
            var liveTechnos = ReadVector(TechnoArray, 10000).ToHashSet();
            foreach (var (manager, state) in spawnManagerStates)
            {
                if (!liveTechnos.Contains(state.Owner.Pointer) ||
                    ReadInt32(state.Owner.Pointer + 0x10) != state.Owner.Id ||
                    ReadUInt32(state.Owner.Pointer + TechnoSpawnManagerOffset) != manager ||
                    ReadInt32(manager + 0x10) != state.Id)
                    continue;
                if (ReadInt32(manager + SpawnManagerRegenRateOffset) == FastSpawnReloadRate)
                    WriteInt32(manager + SpawnManagerRegenRateOffset, state.RegenRate);
                if (ReadInt32(manager + SpawnManagerReloadRateOffset) == FastSpawnReloadRate)
                    WriteInt32(manager + SpawnManagerReloadRateOffset, state.ReloadRate);
            }
        });
        spawnManagerStates.Clear();
    }

    private void DisableUnitEnhancements()
    {
        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                          GameProcessExitedException)
            {
                Console.Error.WriteLine($"[单位功能清理失败] {error.Message}");
            }
        }

        autoUnitRepairEnabled = false;
        rapidAttackEnabled = false;
        if (fastReloadEnabled || spawnManagerStates.Count != 0)
            Cleanup(RestoreSpawnManagers);
        if (largeAmmoEnabled || largeAmmoStates.Count != 0)
            Cleanup(RestoreLargeAmmo);
        if (extendedGuardRangeEnabled || guardRangeTypeStates.Count != 0)
            Cleanup(RestoreGuardRanges);
        if (fastInfantryEnabled || fastInfantryStates.Count != 0)
            Cleanup(RestoreFastInfantry);
        fastReloadEnabled = false;
        largeAmmoEnabled = false;
        extendedGuardRangeEnabled = false;
        fastInfantryEnabled = false;
    }

    private sealed record SpawnManagerState(
        CapturedUnit Owner,
        int Id,
        int RegenRate,
        int ReloadRate);
    private readonly record struct FastInfantryState(double Original, double Applied);
}
