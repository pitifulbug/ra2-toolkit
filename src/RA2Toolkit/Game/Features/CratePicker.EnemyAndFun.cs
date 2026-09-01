using System.ComponentModel;

internal sealed partial class CratePicker
{
    private const int HouseAlliesOffset = 0x5788;
    private const int HouseTypeOffset = 0x34;
    private const int HouseTypeMultiplayPassiveOffset = 0x1A6;
    private const int TechnoMindControlledByOffset = 0x2C0;
    private const int TechnoMindControlledByHouseOffset = 0x2CC;
    private const int TechnoOriginallyOwnedByHouseOffset = 0x2E0;
    private const int BuildingTypeCanBeOccupiedOffset = 0x157B;
    private const int MissionGuard = 5;
    private const int MissionEnter = 7;
    private const int MissionCapture = 8;

    private bool freezeEnemiesEnabled;
    private bool enemyRepairsCauseDamageEnabled;
    private bool counterYuriControlEnabled;
    private bool captureAttackersEnabled;
    private bool preventEnemyCapturesEnabled;
    private bool preventEnemyGarrisonEnabled;
    private DateTime nextEnemyFeatureRefreshAt = DateTime.MinValue;
    private readonly Dictionary<CapturedUnit, double> frozenEnemyStates = [];
    private readonly Dictionary<CapturedUnit, int> enemyBuildingHealthStates = [];

    private void ToggleFreezeEnemies()
    {
        if (freezeEnemiesEnabled)
        {
            RestoreFrozenEnemies();
            freezeEnemiesEnabled = false;
            return;
        }
        freezeEnemiesEnabled = true;
        nextEnemyFeatureRefreshAt = DateTime.MinValue;
        MaintainEnemyAndFunFeatures();
    }

    private void ToggleEnemyRepairsCauseDamage()
    {
        enemyRepairsCauseDamageEnabled = !enemyRepairsCauseDamageEnabled;
        enemyBuildingHealthStates.Clear();
        nextEnemyFeatureRefreshAt = DateTime.MinValue;
    }

    private void ToggleCounterYuriControl()
    {
        counterYuriControlEnabled = !counterYuriControlEnabled;
        nextEnemyFeatureRefreshAt = DateTime.MinValue;
        if (counterYuriControlEnabled)
            MaintainEnemyAndFunFeatures();
    }

    private void ToggleCaptureAttackers()
    {
        captureAttackersEnabled = !captureAttackersEnabled;
        nextEnemyFeatureRefreshAt = DateTime.MinValue;
    }

    private void TogglePreventEnemyCaptures()
    {
        preventEnemyCapturesEnabled = !preventEnemyCapturesEnabled;
        nextEnemyFeatureRefreshAt = DateTime.MinValue;
    }

    private void TogglePreventEnemyGarrison()
    {
        preventEnemyGarrisonEnabled = !preventEnemyGarrisonEnabled;
        nextEnemyFeatureRefreshAt = DateTime.MinValue;
    }

    private void MaintainEnemyAndFunFeatures()
    {
        var now = DateTime.UtcNow;
        if (now < nextEnemyFeatureRefreshAt)
            return;
        nextEnemyFeatureRefreshAt = now + TimeSpan.FromMilliseconds(50);
        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;

        var ownershipChanges = new Dictionary<uint, CapturedUnit>();
        WithSuspendedProcess(() =>
        {
            var technos = ReadVector(TechnoArray, 10000);
            var enemyHouses = CaptureEnemyHouses(house);
            var liveTechnos = new Dictionary<uint, CapturedUnit>();
            var ownedTechnos = new HashSet<uint>();
            foreach (var pointer in technos)
            {
                var id = ReadInt32(pointer + 0x10);
                if (id <= 0)
                    continue;
                liveTechnos[pointer] = new CapturedUnit(pointer, id);
                if (ReadUInt32(pointer + TechnoOwnerOffset) == house)
                    ownedTechnos.Add(pointer);
            }

            if (counterYuriControlEnabled)
                CollectOwnershipReclaims(house, liveTechnos, ownershipChanges);
            if (captureAttackersEnabled)
                CollectAttackers(enemyHouses, liveTechnos, ownedTechnos, ownershipChanges);
            if (freezeEnemiesEnabled)
                ApplyEnemyFreeze(enemyHouses);
            if (enemyRepairsCauseDamageEnabled)
                ReverseEnemyRepairs(enemyHouses);
            if (preventEnemyCapturesEnabled || preventEnemyGarrisonEnabled)
                BlockEnemyEntryMissions(enemyHouses, ownedTechnos);
        });

        if (ownershipChanges.Count != 0)
            InvokeObjectAction(ownershipChanges.Values.Take(32).ToArray(),
                takeOwnership: true, TechnoArray, 10000);
    }

    private void CollectOwnershipReclaims(uint house,
        IReadOnlyDictionary<uint, CapturedUnit> liveTechnos,
        IDictionary<uint, CapturedUnit> changes)
    {
        foreach (var (pointer, unit) in liveTechnos)
        {
            var owner = ReadUInt32(pointer + TechnoOwnerOffset);
            if (owner == house)
                continue;
            var wasOriginallyOwned = ReadUInt32(pointer + TechnoOriginallyOwnedByHouseOffset) == house;
            var isMindControlled = ReadUInt32(pointer + TechnoMindControlledByOffset) != 0 ||
                ReadUInt32(pointer + TechnoMindControlledByHouseOffset) != 0;
            if (wasOriginallyOwned && isMindControlled)
                changes[pointer] = unit;
        }
    }

    private void CollectAttackers(IReadOnlySet<uint> enemyHouses,
        IReadOnlyDictionary<uint, CapturedUnit> liveTechnos,
        IReadOnlySet<uint> ownedTechnos,
        IDictionary<uint, CapturedUnit> changes)
    {
        foreach (var (pointer, unit) in liveTechnos)
        {
            var owner = ReadUInt32(pointer + TechnoOwnerOffset);
            if (!enemyHouses.Contains(owner))
                continue;
            var target = ReadUInt32(pointer + TechnoTargetOffset);
            if (target != 0 && ownedTechnos.Contains(target))
                changes[pointer] = unit;
        }
    }

    private void ApplyEnemyFreeze(IReadOnlySet<uint> enemyHouses)
    {
        foreach (var (unit, original) in frozenEnemyStates.ToArray())
        {
            if (!IsCapturedFootIdentityValid(unit))
            {
                frozenEnemyStates.Remove(unit);
                continue;
            }
            if (enemyHouses.Contains(ReadUInt32(unit.Pointer + TechnoOwnerOffset)))
                continue;
            if (ReadDouble(unit.Pointer + FootSpeedMultiplierOffset) == 0.0)
                WriteBytes(unit.Pointer + FootSpeedMultiplierOffset,
                    BitConverter.GetBytes(original));
            frozenEnemyStates.Remove(unit);
        }

        foreach (var pointer in ReadVector(FootArray, 10000))
        {
            var owner = ReadUInt32(pointer + TechnoOwnerOffset);
            if (!enemyHouses.Contains(owner))
                continue;
            var id = ReadInt32(pointer + 0x10);
            if (id <= 0)
                continue;
            var unit = new CapturedUnit(pointer, id);
            if (!frozenEnemyStates.ContainsKey(unit))
            {
                var original = ReadDouble(pointer + FootSpeedMultiplierOffset);
                if (!IsReasonableSpeedMultiplier(original))
                    continue;
                frozenEnemyStates[unit] = original;
            }
            if (ReadDouble(pointer + FootSpeedMultiplierOffset) != 0.0)
                WriteBytes(pointer + FootSpeedMultiplierOffset, BitConverter.GetBytes(0.0));
        }
    }

    private void ReverseEnemyRepairs(IReadOnlySet<uint> enemyHouses)
    {
        var live = new HashSet<CapturedUnit>();
        foreach (var pointer in ReadVector(BuildingArray, 4096))
        {
            var owner = ReadUInt32(pointer + TechnoOwnerOffset);
            if (!enemyHouses.Contains(owner))
                continue;
            var id = ReadInt32(pointer + 0x10);
            var health = ReadInt32(pointer + ObjectHealthOffset);
            if (id <= 0 || health <= 0)
                continue;
            var building = new CapturedUnit(pointer, id);
            live.Add(building);
            if (enemyBuildingHealthStates.TryGetValue(building, out var previous) &&
                health > previous)
            {
                var reversed = Math.Max(1, previous - (health - previous));
                WriteInt32(pointer + ObjectHealthOffset, reversed);
                health = reversed;
            }
            enemyBuildingHealthStates[building] = health;
        }
        foreach (var building in enemyBuildingHealthStates.Keys
                     .Where(building => !live.Contains(building)).ToArray())
            enemyBuildingHealthStates.Remove(building);
    }

    private void BlockEnemyEntryMissions(
        IReadOnlySet<uint> enemyHouses,
        IReadOnlySet<uint> ownedTechnos)
    {
        var buildings = ReadVector(BuildingArray, 4096).ToHashSet();
        var garrisonableBuildings = new HashSet<uint>();
        if (preventEnemyGarrisonEnabled)
        {
            foreach (var building in buildings)
            {
                var type = ReadUInt32(building + BuildingTypeOffset);
                if (type != 0 && ReadByte(type + BuildingTypeCanBeOccupiedOffset) != 0)
                    garrisonableBuildings.Add(building);
            }
        }
        foreach (var pointer in ReadVector(FootArray, 10000))
        {
            if (!enemyHouses.Contains(ReadUInt32(pointer + TechnoOwnerOffset)))
                continue;
            var mission = ReadInt32(pointer + MissionCurrentOffset);
            var target = ReadUInt32(pointer + TechnoTargetOffset);
            var blockCapture = preventEnemyCapturesEnabled && mission == MissionCapture &&
                ownedTechnos.Contains(target) && buildings.Contains(target);
            var blockGarrison = preventEnemyGarrisonEnabled && mission == MissionEnter &&
                garrisonableBuildings.Contains(target);
            if (!blockCapture && !blockGarrison)
                continue;
            WriteInt32(pointer + MissionCurrentOffset, MissionGuard);
            WriteBytes(pointer + TechnoTargetOffset, BitConverter.GetBytes(0u));
        }
    }

    private HashSet<uint> CaptureEnemyHouses(uint currentHouse)
    {
        var result = new HashSet<uint>();
        var items = ReadUInt32(HouseArray + 4);
        var count = Math.Min(ReadInt32(HouseArray + 16), 32);
        if (items == 0 || count is < 1 or > 32)
            return result;
        var allies = ReadUInt32(currentHouse + HouseAlliesOffset);
        for (var index = 0; index < count; index++)
        {
            var house = ReadUInt32(items + index * sizeof(uint));
            if (house == 0 || house == currentHouse || (allies & (1u << index)) != 0)
                continue;
            var houseType = ReadUInt32(house + HouseTypeOffset);
            if (houseType != 0 && ReadByte(houseType + HouseTypeMultiplayPassiveOffset) != 0)
                continue;
            result.Add(house);
        }
        return result;
    }

    private void RestoreFrozenEnemies()
    {
        WithSuspendedProcess(() =>
        {
            foreach (var (unit, original) in frozenEnemyStates)
                if (IsCapturedFootIdentityValid(unit) &&
                    ReadDouble(unit.Pointer + FootSpeedMultiplierOffset) == 0.0)
                    WriteBytes(unit.Pointer + FootSpeedMultiplierOffset,
                        BitConverter.GetBytes(original));
        });
        frozenEnemyStates.Clear();
    }

    private void DisableEnemyAndFunFeatures()
    {
        if (freezeEnemiesEnabled || frozenEnemyStates.Count != 0)
        {
            try { RestoreFrozenEnemies(); }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                          GameProcessExitedException)
            {
                Console.Error.WriteLine($"[敌人功能清理失败] {error.Message}");
            }
        }
        freezeEnemiesEnabled = false;
        enemyRepairsCauseDamageEnabled = false;
        counterYuriControlEnabled = false;
        captureAttackersEnabled = false;
        preventEnemyCapturesEnabled = false;
        preventEnemyGarrisonEnabled = false;
        enemyBuildingHealthStates.Clear();
    }
}
