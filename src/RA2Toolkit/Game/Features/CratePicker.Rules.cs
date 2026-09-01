using System.ComponentModel;
using System.Text;

internal sealed partial class CratePicker
{
    private const long RulesClassPointerAddress = 0x8871E0;
    private const long InfantryTypeArray = 0xA8E348;

    private const int MaximumQueuedObjectsOffset = 0xF0;
    private const int PsychicDominatorDamageOffset = 0x30C;
    private const int PrismSupportModifierOffset = 0x49C;
    private const int PrismSupportDelayOffset = 0x4A4;
    private const int PrismSupportDurationOffset = 0x4A8;
    private const int PrismSupportHeightOffset = 0x4AC;
    private const int V3RocketDamageOffset = 0x4D0;
    private const int EliteV3RocketDamageOffset = 0x4D4;
    private const int DreadnoughtMissileDamageOffset = 0x504;
    private const int EliteDreadnoughtMissileDamageOffset = 0x508;
    private const int BoomerMissileDamageOffset = 0x538;
    private const int EliteBoomerMissileDamageOffset = 0x53C;
    private const int EliteUnitAttackMultiplierOffset = 0x670;
    private const int EliteUnitSpeedMultiplierOffset = 0x678;
    private const int EliteUnitArmorMultiplierOffset = 0x688;
    private const int EliteUnitRateOfFireMultiplierOffset = 0x690;
    private const int UsParadropInfantryTypeOffset = 0xC04;
    private const int UsParadropCountOffset = 0xC20;
    private const int AlliedParadropInfantryTypeOffset = 0xC3C;
    private const int AlliedParadropCountOffset = 0xC58;
    private const int SovietParadropInfantryTypeOffset = 0xC74;
    private const int SovietParadropCountOffset = 0xC90;
    private const int YuriParadropInfantryTypeOffset = 0xCAC;
    private const int YuriParadropCountOffset = 0xCC8;
    private const int HouseTypeSideIndexOffset = 0xBC;
    private const int OrePurifierBonusOffset = 0xF3C;
    private const int NuclearMissileDamageOffset = 0x1530;
    private const int LightningStormDamageOffset = 0x1798;

    private readonly object rulesOverrideSync = new();
    private readonly Dictionary<OverlayCommand, RulesOverride> rulesOverrides = [];

    private int? SetBuildQueueLimit(OverlayCommandRequest request)
    {
        EnsureCommand(request, OverlayCommand.SetBuildQueueLimit);
        var value = RequireInteger(request, 1, 1_000, "建造队列上限");
        ApplyRulesOverride(request.Command, MaximumQueuedObjectsOffset,
            RulesTargetKind.Direct, BitConverter.GetBytes(value));
        return 1;
    }

    private int? SetRulesValue(OverlayCommandRequest request)
    {
        var definition = GetRulesValueDefinition(request.Command);
        var value = RequireNumber(request, definition.Minimum, definition.Maximum,
            definition.DisplayName);
        var bytes = definition.Storage switch
        {
            RulesValueStorage.Int32 => BitConverter.GetBytes(ToInteger(value,
                definition.DisplayName)),
            RulesValueStorage.Single => BitConverter.GetBytes((float)value),
            RulesValueStorage.Double => BitConverter.GetBytes((double)value),
            _ => throw new InvalidOperationException("未知的规则数值类型。")
        };

        ApplyRulesOverride(request.Command, definition.Offset, RulesTargetKind.Direct, bytes);
        return 1;
    }

    private int? SetParadropConfiguration(OverlayCommandRequest request)
    {
        var house = ReadUInt32(CurrentPlayer);
        var houseType = house == 0 ? 0 : ReadUInt32(house + HouseTypeOffset);
        if (houseType == 0)
            throw new InvalidOperationException("无法识别当前玩家阵营。");
        var definition = GetParadropDefinition(request.Command,
            ReadInt32(houseType + HouseTypeSideIndexOffset), ReadTypeId(houseType));
        if (definition.IsInfantryType)
        {
            var id = ValidateInfantryTypeId(request.Option);
            ApplyRulesOverride(request.Command, definition.Offset,
                RulesTargetKind.VectorFirstElement,
                () => BitConverter.GetBytes(FindInfantryTypePointer(id)));
        }
        else
        {
            var count = RequireInteger(request, 0, 100, definition.DisplayName);
            ApplyRulesOverride(request.Command, definition.Offset,
                RulesTargetKind.VectorFirstElement, BitConverter.GetBytes(count));
        }
        return 1;
    }

    private void ApplyRulesOverride(
        OverlayCommand command,
        int offset,
        RulesTargetKind targetKind,
        byte[] desiredValue) =>
        ApplyRulesOverride(command, offset, targetKind, () => desiredValue);

    private void ApplyRulesOverride(
        OverlayCommand command,
        int offset,
        RulesTargetKind targetKind,
        Func<byte[]> desiredValueFactory)
    {
        lock (rulesOverrideSync)
        {
            WithSuspendedProcess(() =>
            {
                var rulesPointer = ReadUInt32(RulesClassPointerAddress);
                if (rulesPointer == 0)
                    throw new InvalidOperationException("规则对象尚未初始化，请进入对局后再试。");

                if (rulesOverrides.Values.Any(item => item.RulesPointer != rulesPointer))
                    rulesOverrides.Clear();

                var desiredValue = desiredValueFactory();
                var targetAddress = ResolveRulesTarget(rulesPointer, offset, targetKind);
                var previousValue = ReadBytes(targetAddress, desiredValue.Length);
                var valueChanged = !previousValue.AsSpan().SequenceEqual(desiredValue);
                rulesOverrides.TryGetValue(command, out var existing);
                if (existing is not null &&
                    (existing.RulesPointer != rulesPointer || existing.Offset != offset ||
                     existing.TargetKind != targetKind ||
                     existing.TargetAddress != targetAddress ||
                     existing.OriginalValue.Length != desiredValue.Length))
                {
                    throw new InvalidOperationException(
                        "规则字段的内存位置已经变化；为避免误写，本次设置已取消。");
                }
                if (existing is not null &&
                    !previousValue.AsSpan().SequenceEqual(existing.LastWrittenValue) &&
                    (existing.AlternateWrittenValue is not { } alternate ||
                     !previousValue.AsSpan().SequenceEqual(alternate)))
                {
                    // Another writer took ownership after our previous setting. Treat the
                    // current value as the new baseline for this explicit user request.
                    rulesOverrides.Remove(command);
                    existing = null;
                }

                try
                {
                    WriteVerifiedWithRollback(targetAddress, previousValue, desiredValue);
                }
                catch (Exception writeError) when (writeError is Win32Exception or
                                                   InvalidOperationException)
                {
                    // A failed rollback can leave either the attempted bytes or the value
                    // present before this attempt. Retain both as owned candidates so a
                    // later cleanup can restore the original baseline without guessing.
                    var originalValue = existing?.OriginalValue ?? previousValue;
                    rulesOverrides[command] = new RulesOverride(
                        rulesPointer,
                        offset,
                        targetKind,
                        targetAddress,
                        (byte[])originalValue.Clone(),
                        (byte[])desiredValue.Clone(),
                        (byte[])previousValue.Clone());
                    throw;
                }

                if (existing is null)
                {
                    if (valueChanged)
                    {
                        rulesOverrides.Add(command, new RulesOverride(
                            rulesPointer,
                            offset,
                            targetKind,
                            targetAddress,
                            (byte[])previousValue.Clone(),
                            (byte[])desiredValue.Clone()));
                    }
                }
                else if (existing.OriginalValue.AsSpan().SequenceEqual(desiredValue))
                {
                    rulesOverrides.Remove(command);
                }
                else if (valueChanged)
                {
                    existing.LastWrittenValue = (byte[])desiredValue.Clone();
                    existing.AlternateWrittenValue = null;
                }
            });
        }
    }

    private void WriteVerifiedWithRollback(
        long targetAddress,
        byte[] previousValue,
        byte[] desiredValue)
    {
        if (previousValue.AsSpan().SequenceEqual(desiredValue))
            return;

        try
        {
            WriteBytes(targetAddress, desiredValue);
            if (!ReadBytes(targetAddress, desiredValue.Length).AsSpan().SequenceEqual(desiredValue))
                throw new InvalidOperationException(
                    $"地址 0x{targetAddress:X} 的规则数值写入后校验失败。");
        }
        catch (Exception writeError) when (writeError is Win32Exception or InvalidOperationException)
        {
            try
            {
                WriteBytes(targetAddress, previousValue);
                if (!ReadBytes(targetAddress, previousValue.Length).AsSpan()
                        .SequenceEqual(previousValue))
                {
                    throw new InvalidOperationException(
                        $"地址 0x{targetAddress:X} 的规则数值回滚后校验失败。");
                }
            }
            catch (Exception rollbackError) when (
                rollbackError is Win32Exception or InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "规则数值写入失败，且未能完整回滚。",
                    new AggregateException(writeError, rollbackError));
            }

            throw new InvalidOperationException("规则数值写入失败，已恢复写入前的值。", writeError);
        }
    }

    private void RestoreRulesOverrides()
    {
        lock (rulesOverrideSync)
        {
            if (rulesOverrides.Count == 0)
                return;

            WithSuspendedProcess(() =>
            {
                var rulesPointer = ReadUInt32(RulesClassPointerAddress);
                if (rulesPointer == 0 ||
                    rulesOverrides.Values.Any(item => item.RulesPointer != rulesPointer))
                {
                    rulesOverrides.Clear();
                    return;
                }

                var restorable = new List<(RulesOverride Item, byte[] OwnedValue)>();
                foreach (var item in rulesOverrides.Values)
                {
                    if (!TryResolveRulesTarget(rulesPointer, item, out var targetAddress) ||
                        targetAddress != item.TargetAddress)
                    {
                        continue;
                    }

                    var currentValue = ReadBytes(targetAddress, item.LastWrittenValue.Length);
                    if (currentValue.AsSpan().SequenceEqual(item.LastWrittenValue) ||
                        item.AlternateWrittenValue is { } alternate &&
                        currentValue.AsSpan().SequenceEqual(alternate))
                        restorable.Add((item, currentValue));
                }

                var restored = new List<(RulesOverride Item, byte[] OwnedValue)>();
                try
                {
                    foreach (var entry in restorable)
                    {
                        WriteVerifiedWithRollback(entry.Item.TargetAddress,
                            entry.OwnedValue, entry.Item.OriginalValue);
                        restored.Add(entry);
                    }
                }
                catch (Exception restoreError) when (
                    restoreError is Win32Exception or InvalidOperationException)
                {
                    var rollbackErrors = new List<Exception>();
                    foreach (var entry in restored.AsEnumerable().Reverse())
                    {
                        try
                        {
                            var currentValue = ReadBytes(entry.Item.TargetAddress,
                                entry.Item.OriginalValue.Length);
                            WriteVerifiedWithRollback(entry.Item.TargetAddress, currentValue,
                                entry.OwnedValue);
                        }
                        catch (Exception rollbackError) when (
                            rollbackError is Win32Exception or InvalidOperationException)
                        {
                            rollbackErrors.Add(rollbackError);
                        }
                    }

                    if (rollbackErrors.Count != 0)
                    {
                        rollbackErrors.Insert(0, restoreError);
                        throw new InvalidOperationException(
                            "规则数值恢复失败，且未能完整回滚恢复操作。",
                            new AggregateException(rollbackErrors));
                    }

                    throw;
                }

                // Values changed by another tool were intentionally skipped. Clearing the
                // snapshots relinquishes ownership instead of overwriting those changes later.
                rulesOverrides.Clear();
            });
        }
    }

    private void ResetRulesOverridesForMatch()
    {
        lock (rulesOverrideSync)
            rulesOverrides.Clear();
    }

    private long ResolveRulesTarget(
        uint rulesPointer,
        int offset,
        RulesTargetKind targetKind)
    {
        var fieldAddress = checked((long)rulesPointer + offset);
        if (targetKind == RulesTargetKind.Direct)
            return fieldAddress;

        var header = ReadBytes(fieldAddress, 20);
        var items = BitConverter.ToUInt32(header, 4);
        var count = BitConverter.ToInt32(header, 16);
        if (items == 0 || count != 1)
        {
            throw new InvalidOperationException(
                $"规则向量 0x{fieldAddress:X} 的元素数量不是 1，未进行写入。");
        }
        return items;
    }

    private bool TryResolveRulesTarget(
        uint rulesPointer,
        RulesOverride item,
        out long targetAddress)
    {
        try
        {
            targetAddress = ResolveRulesTarget(rulesPointer, item.Offset, item.TargetKind);
            return true;
        }
        catch (InvalidOperationException)
        {
            targetAddress = 0;
            return false;
        }
    }

    private uint FindInfantryTypePointer(string id)
    {
        foreach (var pointer in ReadVector(InfantryTypeArray, 4_096))
        {
            var idBytes = ReadBytes(pointer + AbstractTypeIdOffset, 0x18);
            var terminator = Array.IndexOf(idBytes, (byte)0);
            var actualId = Encoding.ASCII.GetString(idBytes, 0,
                terminator < 0 ? idBytes.Length : terminator);
            if (string.Equals(actualId, id, StringComparison.OrdinalIgnoreCase))
                return pointer;
        }

        throw new InvalidOperationException($"步兵类型 {id} 不存在于当前规则中。");
    }

    private static string ValidateInfantryTypeId(string? option)
    {
        var id = option?.Trim();
        if (string.IsNullOrEmpty(id) || id.Length > 23 ||
            id.Any(character => !(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                                  >= '0' and <= '9' or '_' or '-')))
        {
            throw new InvalidOperationException(
                "空降兵类型必须是 1 到 23 位的英文、数字、下划线或连字符 ID。");
        }
        return id;
    }

    private static decimal RequireNumber(
        OverlayCommandRequest request,
        decimal minimum,
        decimal maximum,
        string displayName)
    {
        if (request.Number is not { } value)
            throw new InvalidOperationException($"{displayName}缺少数值。");
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                $"{displayName}必须在 {minimum} 到 {maximum} 之间。");
        }
        return value;
    }

    private static int RequireInteger(
        OverlayCommandRequest request,
        int minimum,
        int maximum,
        string displayName) =>
        ToInteger(RequireNumber(request, minimum, maximum, displayName), displayName);

    private static int RequireWholeNumber(
        OverlayCommandRequest request,
        int minimum,
        int maximum,
        string displayName) =>
        RequireInteger(request, minimum, maximum, displayName);

    private static int ToInteger(decimal value, string displayName)
    {
        if (decimal.Truncate(value) != value || value is < int.MinValue or > int.MaxValue)
            throw new InvalidOperationException($"{displayName}必须是有效整数。");
        return decimal.ToInt32(value);
    }

    private static void EnsureCommand(
        OverlayCommandRequest request,
        OverlayCommand expected)
    {
        if (request.Command != expected)
            throw new InvalidOperationException("命令与规则字段不匹配。");
    }

    private static RulesValueDefinition GetRulesValueDefinition(OverlayCommand command) =>
        command switch
        {
            OverlayCommand.SetNuclearMissileDamage => new(
                NuclearMissileDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "核弹伤害"),
            OverlayCommand.SetEliteUnitAttackMultiplier => new(
                EliteUnitAttackMultiplierOffset, RulesValueStorage.Double,
                0, 100, "三星单位攻击力"),
            OverlayCommand.SetEliteUnitSpeedMultiplier => new(
                EliteUnitSpeedMultiplierOffset, RulesValueStorage.Double,
                0, 100, "三星单位移动速度"),
            OverlayCommand.SetEliteUnitArmorMultiplier => new(
                EliteUnitArmorMultiplierOffset, RulesValueStorage.Double,
                0, 100, "三星单位装甲"),
            OverlayCommand.SetEliteUnitRateOfFireMultiplier => new(
                EliteUnitRateOfFireMultiplierOffset, RulesValueStorage.Double,
                0, 100, "三星单位射速"),
            OverlayCommand.SetPsychicDominatorDamage => new(
                PsychicDominatorDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "心灵控制器伤害"),
            OverlayCommand.SetPrismSupportModifier => new(
                PrismSupportModifierOffset, RulesValueStorage.Int32,
                0, 10_000, "光棱塔连携系数"),
            OverlayCommand.SetPrismSupportDelay => new(
                PrismSupportDelayOffset, RulesValueStorage.Int32,
                0, 100_000, "光棱塔连携延迟"),
            OverlayCommand.SetPrismSupportDuration => new(
                PrismSupportDurationOffset, RulesValueStorage.Int32,
                0, 100_000, "光棱塔连携持续时间"),
            OverlayCommand.SetPrismSupportHeight => new(
                PrismSupportHeightOffset, RulesValueStorage.Int32,
                0, 100_000, "光棱塔连携高度"),
            OverlayCommand.SetV3RocketDamage => new(
                V3RocketDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "V3 火箭伤害"),
            OverlayCommand.SetEliteV3RocketDamage => new(
                EliteV3RocketDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "V3 火箭三星伤害"),
            OverlayCommand.SetDreadnoughtMissileDamage => new(
                DreadnoughtMissileDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "无畏舰导弹伤害"),
            OverlayCommand.SetEliteDreadnoughtMissileDamage => new(
                EliteDreadnoughtMissileDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "无畏舰导弹三星伤害"),
            OverlayCommand.SetBoomerMissileDamage => new(
                BoomerMissileDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "雷鸣潜艇导弹伤害"),
            OverlayCommand.SetEliteBoomerMissileDamage => new(
                EliteBoomerMissileDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "雷鸣潜艇三星伤害"),
            OverlayCommand.SetOrePurifierBonus => new(
                OrePurifierBonusOffset, RulesValueStorage.Single,
                0, 100, "矿石精炼器加成"),
            OverlayCommand.SetLightningStormDamage => new(
                LightningStormDamageOffset, RulesValueStorage.Int32,
                0, 2_000_000_000, "闪电风暴伤害"),
            _ => throw new InvalidOperationException("该命令不是受支持的规则数值设置。")
        };

    internal static ParadropDefinition GetParadropDefinition(
        OverlayCommand command, int sideIndex, string countryId)
    {
        var isInfantryType = command switch
        {
            OverlayCommand.SetParadropInfantryType => true,
            OverlayCommand.SetParadropCount => false,
            _ => throw new InvalidOperationException("该命令不是受支持的空降配置。")
        };
        var (typeOffset, countOffset, sideName) = sideIndex switch
        {
            0 when countryId.Equals("Americans", StringComparison.OrdinalIgnoreCase) =>
                (UsParadropInfantryTypeOffset, UsParadropCountOffset, "美国"),
            0 => (AlliedParadropInfantryTypeOffset, AlliedParadropCountOffset, "盟军"),
            1 => (SovietParadropInfantryTypeOffset, SovietParadropCountOffset, "苏联"),
            2 => (YuriParadropInfantryTypeOffset, YuriParadropCountOffset, "尤里"),
            _ => throw new InvalidOperationException("当前玩家阵营不支持空降兵设置。")
        };
        return new ParadropDefinition(
            isInfantryType ? typeOffset : countOffset,
            isInfantryType,
            $"{sideName}空降兵{(isInfantryType ? "类型" : "数量")}");
    }

    private enum RulesTargetKind
    {
        Direct,
        VectorFirstElement
    }

    private enum RulesValueStorage
    {
        Int32,
        Single,
        Double
    }

    private sealed class RulesOverride(
        uint rulesPointer,
        int offset,
        RulesTargetKind targetKind,
        long targetAddress,
        byte[] originalValue,
        byte[] lastWrittenValue,
        byte[]? alternateWrittenValue = null)
    {
        public uint RulesPointer { get; } = rulesPointer;
        public int Offset { get; } = offset;
        public RulesTargetKind TargetKind { get; } = targetKind;
        public long TargetAddress { get; } = targetAddress;
        public byte[] OriginalValue { get; } = originalValue;
        public byte[] LastWrittenValue { get; set; } = lastWrittenValue;
        public byte[]? AlternateWrittenValue { get; set; } = alternateWrittenValue;
    }

    private readonly record struct RulesValueDefinition(
        int Offset,
        RulesValueStorage Storage,
        decimal Minimum,
        decimal Maximum,
        string DisplayName);

    internal readonly record struct ParadropDefinition(
        int Offset,
        bool IsInfantryType,
        string DisplayName);
}
