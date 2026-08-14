using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private void ToggleInstantBuild()
    {
        if (instantBuildEnabled)
        {
            DisableInstantBuild();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine("[瞬间建造未开启] 当前玩家阵营无效。");
            return;
        }
        instantBuildHouse = house;
        originalBuildSpeeds = Enumerable.Range(0, 5)
            .Select(index => ReadInt32(house + HouseBuildSpeedOffset + index * 4L)).ToArray();
        instantBuildEnabled = true;
        nextInstantBuildRefreshAt = DateTime.MinValue;
        MaintainInstantBuild();
        Console.WriteLine("[瞬间建造已开启] 生产倍率已锁定，并会立即推进当前生产项目。");
    }

    private void MaintainInstantBuild()
    {
        var now = DateTime.UtcNow;
        if (now < nextInstantBuildRefreshAt)
            return;
        nextInstantBuildRefreshAt = now + TimeSpan.FromMilliseconds(100);

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            return;
        if (house != instantBuildHouse)
        {
            RestoreBuildSpeeds();
            instantBuildHouse = house;
            originalBuildSpeeds = Enumerable.Range(0, 5)
                .Select(index => ReadInt32(house + HouseBuildSpeedOffset + index * 4L)).ToArray();
        }
        for (var index = 0; index < 5; index++)
            WriteInt32(house + HouseBuildSpeedOffset + index * 4L, 15);

        foreach (var factory in ReadVector(FactoryArray, 256))
        {
            if (ReadUInt32(factory + FactoryOwnerOffset) != house ||
                ReadUInt32(factory + FactoryObjectOffset) == 0)
                continue;
            if (ReadInt32(factory + FactoryProductionValueOffset) >= 54)
                continue;
            WriteInt32(factory + FactoryProductionValueOffset, 53);
            WriteBytes(factory + FactoryProductionChangedOffset, [0]);
            WriteInt32(factory + FactoryProductionTimerStartOffset, ReadInt32(CurrentFrame) - 1);
            WriteInt32(factory + FactoryProductionTimerTimeLeftOffset, 0);
            WriteInt32(factory + FactoryProductionRateOffset, 1);
            WriteInt32(factory + FactoryProductionStepOffset, 1);
        }
    }

    private void DisableInstantBuild()
    {
        if (!instantBuildEnabled)
            return;
        RestoreBuildSpeeds();
        instantBuildEnabled = false;
        instantBuildHouse = 0;
        originalBuildSpeeds = null;
        Console.WriteLine("[瞬间建造已关闭] 已恢复原生产倍率。");
    }

    private void RestoreBuildSpeeds()
    {
        if (instantBuildHouse == 0 || originalBuildSpeeds is null)
            return;
        for (var index = 0; index < originalBuildSpeeds.Length; index++)
            WriteInt32(instantBuildHouse + HouseBuildSpeedOffset + index * 4L, originalBuildSpeeds[index]);
    }

    private void ToggleAutoBuild(string typeId, string displayName)
    {
        if (autoBuildState is not null)
        {
            StopAutoBuild("自动建造已停止；已完成的建筑会保留。");
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            ShowOperationStatus("当前玩家阵营无效，无法开始自动建造。", true);
            return;
        }

        var selectedBuilding = CaptureSelectedBuilding(house);
        if (selectedBuilding is null)
        {
            ShowOperationStatus("请先在游戏中选择一座己方建筑。", true);
            return;
        }

        var buildingType = FindBuildingType(typeId);
        if (buildingType is null)
        {
            ShowOperationStatus($"当前规则中没有找到{displayName}（{typeId}）。", true);
            return;
        }

        try
        {
            var center = ReadUnitCell(selectedBuilding.Pointer);
            var candidates = CreateAutoBuildCandidates(center);
            autoBuildState = new AutoBuildState(
                buildingType.Value.Pointer,
                buildingType.Value.Index,
                displayName,
                house,
                candidates)
            {
                Phase = AutoBuildPhase.FindingPlacement,
                NextActionAt = DateTime.UtcNow
            };
        }
        catch
        {
            StopAutoBuild(null);
            throw;
        }
        ShowOperationStatus($"正在围绕选中建筑自动建造{displayName}；再次按任一自动建造快捷键可停止。");
    }

    private void MaintainAutoBuild()
    {
        var state = autoBuildState;
        if (state is null)
            return;

        try
        {
            var now = DateTime.UtcNow;
            if (now < state.NextActionAt)
                return;
            if (ReadUInt32(CurrentPlayer) != state.House)
            {
                StopAutoBuild("玩家阵营已经变化，自动建造已停止。", true);
                return;
            }

            switch (state.Phase)
            {
                case AutoBuildPhase.WaitingForProduction:
                    MaintainAutoBuildProduction(state, now);
                    break;
                case AutoBuildPhase.FindingPlacement:
                    FindAutoBuildPlacement(state, now);
                    break;
                case AutoBuildPhase.WaitingForPlacement:
                    MaintainAutoBuildPlacement(state, now);
                    break;
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            StopAutoBuild($"自动建造已停止：{error.Message}", true);
        }
    }

    private void BeginAutoBuildProduction(AutoBuildState state, DateTime now)
    {
        EnqueueEvent(CreateProductionEvent(0x0E, state.TypeIndex)); // EventType::Produce
        state.Factory = 0;
        state.Phase = AutoBuildPhase.WaitingForProduction;
        state.NextActionAt = now + TimeSpan.FromMilliseconds(100);
        state.Deadline = now + TimeSpan.FromSeconds(12);
    }

    private void MaintainAutoBuildProduction(AutoBuildState state, DateTime now)
    {
        var factory = FindFactoryProducing(state.House, state.TypePointer);
        if (factory != 0)
        {
            state.Factory = factory;
            if (ReadByte(factory + FactoryIsSuspendedOffset) != 0 ||
                ReadInt32(factory + FactoryProductionValueOffset) >= 54)
            {
                state.HasCompletedObject = true;
                SendAutoBuildPlacement(state, now);
                return;
            }

            if (ReadByte(factory + FactoryOnHoldOffset) == 0)
                AdvanceFactoryProduction(factory);
        }

        if (now >= state.Deadline)
        {
            StopAutoBuild($"{state.DisplayName}未能完成生产，请检查资金、科技条件和建筑生产队列。", true);
            return;
        }
        state.NextActionAt = now + TimeSpan.FromMilliseconds(100);
    }

    private void FindAutoBuildPlacement(AutoBuildState state, DateTime now)
    {
        if (state.Candidates.Count == 0)
        {
            StopAutoBuild(
                state.BuiltCount == 0
                    ? $"没有找到可放置{state.DisplayName}的位置。"
                    : $"自动建造完成：共建造 {state.BuiltCount} 座{state.DisplayName}，已没有可放置位置。");
            return;
        }

        var candidate = state.Candidates.Dequeue();
        if (!CanPlaceBuilding(state.TypePointer, state.House, candidate))
        {
            state.NextActionAt = now;
            return;
        }

        state.CurrentCandidate = candidate;
        if (!state.HasCompletedObject)
        {
            BeginAutoBuildProduction(state, now);
            return;
        }
        SendAutoBuildPlacement(state, now);
    }

    private void SendAutoBuildPlacement(AutoBuildState state, DateTime now)
    {
        state.BuildingCountBeforePlacement = CountPlacedBuildings(state.House, state.TypePointer);
        EnqueueEvent(CreatePlaceEvent(state.TypeIndex, state.CurrentCandidate));
        state.Phase = AutoBuildPhase.WaitingForPlacement;
        state.NextActionAt = now + TimeSpan.FromMilliseconds(100);
        state.Deadline = now + TimeSpan.FromSeconds(1);
    }

    private void MaintainAutoBuildPlacement(AutoBuildState state, DateTime now)
    {
        var count = CountPlacedBuildings(state.House, state.TypePointer);
        if (count > state.BuildingCountBeforePlacement)
        {
            state.BuiltCount++;
            state.HasCompletedObject = false;
            state.Factory = 0;
            state.Phase = AutoBuildPhase.FindingPlacement;
            state.NextActionAt = now;
            return;
        }

        if (now < state.Deadline)
        {
            state.NextActionAt = now + TimeSpan.FromMilliseconds(50);
            return;
        }

        var factoryObject = state.Factory == 0
            ? 0
            : ReadUInt32(state.Factory + FactoryObjectOffset);
        if (factoryObject != 0 &&
            ReadUInt32(factoryObject + BuildingTypeOffset) == state.TypePointer)
        {
            state.HasCompletedObject = true;
            state.Phase = AutoBuildPhase.FindingPlacement;
            state.NextActionAt = now;
            return;
        }

        StopAutoBuild($"{state.DisplayName}的放置状态无法确认，自动建造已停止。", true);
    }

    private void StopAutoBuild(string? message, bool isError = false)
    {
        autoBuildState = null;
        if (message is not null)
            ShowOperationStatus(message, isError);
    }

    private CapturedUnit? CaptureSelectedBuilding(uint house) =>
        CaptureSelectedObjects(technoOnly: true)
            .FirstOrDefault(selected =>
                VectorContains(BuildingArray, selected.Pointer) &&
                ReadUInt32(selected.Pointer + TechnoOwnerOffset) == house &&
                ReadByte(selected.Pointer + ObjectIsOnMapOffset) != 0 &&
                ReadByte(selected.Pointer + ObjectInLimboOffset) == 0 &&
                ReadByte(selected.Pointer + ObjectIsAliveOffset) != 0);

    private (uint Pointer, int Index)? FindBuildingType(string typeId)
    {
        var items = ReadUInt32(BuildingTypeArray + 4);
        var count = ReadInt32(BuildingTypeArray + 16);
        if (items == 0 || count is < 0 or > 4096)
            return null;
        for (var index = 0; index < count; index++)
        {
            var pointer = ReadUInt32(items + index * 4L);
            if (pointer != 0 && ReadTypeId(pointer).Equals(typeId, StringComparison.OrdinalIgnoreCase))
                return (pointer, index);
        }
        return null;
    }

    private Queue<(short X, short Y)> CreateAutoBuildCandidates((int X, int Y) center)
    {
        var left = ReadInt32(Map + MapBoundsOffset);
        var top = ReadInt32(Map + MapBoundsOffset + 4);
        var width = ReadInt32(Map + MapBoundsOffset + 8);
        var height = ReadInt32(Map + MapBoundsOffset + 12);
        if (width <= 0 || height <= 0)
            return new Queue<(short X, short Y)>();

        // MapRect is RectangleStruct { X, Y, Width, Height }, not an LTRB rectangle.
        var right = (long)left + width - 1;
        var bottom = (long)top + height - 1;
        var candidates = new List<(short X, short Y)>();
        for (var y = Math.Max((long)top, short.MinValue); y <= Math.Min(bottom, short.MaxValue); y++)
        for (var x = Math.Max((long)left, short.MinValue); x <= Math.Min(right, short.MaxValue); x++)
            if (x > 0 && y > 0)
                candidates.Add(((short)x, (short)y));
        candidates.Sort((a, b) =>
        {
            var distanceComparison = DistanceSquared((a.X, a.Y), center)
                .CompareTo(DistanceSquared((b.X, b.Y), center));
            return distanceComparison != 0
                ? distanceComparison
                : Math.Atan2(a.Y - center.Y, a.X - center.X)
                    .CompareTo(Math.Atan2(b.Y - center.Y, b.X - center.X));
        });
        return new Queue<(short X, short Y)>(candidates);
    }

    private uint FindFactoryProducing(uint house, uint typePointer)
    {
        foreach (var factory in ReadVector(FactoryArray, 256))
        {
            if (ReadUInt32(factory + FactoryOwnerOffset) != house)
                continue;
            var product = ReadUInt32(factory + FactoryObjectOffset);
            if (product != 0 && ReadUInt32(product + BuildingTypeOffset) == typePointer)
                return factory;
        }
        return 0;
    }

    private bool CanPlaceBuilding(
        uint typePointer, uint house, (short X, short Y) cell)
    {
        if (!ReadBytes(LogicUpdate, LogicUpdateOriginalBytes.Length)
                .AsSpan().SequenceEqual(LogicUpdateOriginalBytes))
            throw new InvalidOperationException("游戏主循环函数指纹不匹配，无法检查建筑位置。");

        const int codeCaveSize = 128;
        var codeCave = Native.VirtualAllocEx(handle, 0, codeCaveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (codeCave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配建筑位置检查区失败");

        var markerAddress = codeCave.ToInt64() + 112;
        var resultAddress = markerAddress + 4;
        var cellAddress = resultAddress + 4;
        var protectionChanged = false;
        uint previousProtection = 0;
        var canFreeCodeCave = true;
        try
        {
            WriteUInt16(cellAddress, unchecked((ushort)cell.X));
            WriteUInt16(cellAddress + 2, unchecked((ushort)cell.Y));

            var code = new List<byte>(112) { 0x60 }; // pushad
            code.AddRange([0xC7, 0x05]); // restore LogicClass::Update before the call
            code.AddRange(BitConverter.GetBytes(checked((uint)LogicUpdate)));
            code.AddRange(LogicUpdateOriginalBytes.AsSpan(0, 4).ToArray());
            code.AddRange([0xC7, 0x05]);
            code.AddRange(BitConverter.GetBytes(checked((uint)(LogicUpdate + 4))));
            code.AddRange(LogicUpdateOriginalBytes.AsSpan(4, 4).ToArray());
            code.AddRange([0xC6, 0x05]);
            code.AddRange(BitConverter.GetBytes(checked((uint)(LogicUpdate + 8))));
            code.Add(LogicUpdateOriginalBytes[8]);
            code.Add(0xB9); // mov ecx, BuildingTypeClass*
            code.AddRange(BitConverter.GetBytes(typePointer));
            code.Add(0x68); // push HouseClass*
            code.AddRange(BitConverter.GetBytes(house));
            code.Add(0x68); // push CellStruct*
            code.AddRange(BitConverter.GetBytes(checked((uint)cellAddress)));
            code.Add(0xB8); // mov eax, BuildingTypeClass::CanPlaceHere
            code.AddRange(BitConverter.GetBytes(checked((uint)BuildingTypeCanPlaceHere)));
            code.AddRange([0xFF, 0xD0]); // call eax
            code.AddRange([0x0F, 0xB6, 0xC0]); // movzx eax, al
            code.Add(0xA3); // mov [result], eax
            code.AddRange(BitConverter.GetBytes(checked((uint)resultAddress)));
            code.AddRange([0xC7, 0x05]); // completion marker
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.AddRange(BitConverter.GetBytes(1));
            code.Add(0x61); // popad
            code.AddRange(LogicUpdateOriginalBytes);
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (LogicUpdate + LogicUpdateOriginalBytes.Length -
                 (codeCave.ToInt64() + code.Count + 4)))));
            WriteBytes(codeCave.ToInt64(), [.. code]);

            if (!Native.VirtualProtectEx(handle, (nint)LogicUpdate,
                    (nuint)LogicUpdateOriginalBytes.Length,
                    Native.PageExecuteReadWrite, out previousProtection))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "修改主循环入口页面保护失败");
            protectionChanged = true;

            var jump = Enumerable.Repeat((byte)0x90, LogicUpdateOriginalBytes.Length).ToArray();
            jump[0] = 0xE9;
            BitConverter.GetBytes(checked((int)
                    (codeCave.ToInt64() - (LogicUpdate + 5))))
                .CopyTo(jump, 1);
            WriteBytes(LogicUpdate, jump);
            if (!Native.FlushInstructionCache(handle, (nint)LogicUpdate, (nuint)jump.Length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "刷新建筑位置检查跳转失败");

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
            while (DateTime.UtcNow < deadline && ReadInt32(markerAddress) != 1)
                Thread.Sleep(1);
            if (ReadInt32(markerAddress) != 1)
            {
                canFreeCodeCave = false;
                throw new InvalidOperationException("等待游戏主线程检查建筑位置超时。");
            }
            return ReadInt32(resultAddress) != 0;
        }
        finally
        {
            if (protectionChanged)
            {
                WriteBytes(LogicUpdate, LogicUpdateOriginalBytes);
                Native.FlushInstructionCache(handle, (nint)LogicUpdate,
                    (nuint)LogicUpdateOriginalBytes.Length);
                Native.VirtualProtectEx(handle, (nint)LogicUpdate,
                    (nuint)LogicUpdateOriginalBytes.Length,
                    previousProtection, out _);
            }
            if (canFreeCodeCave)
                Native.VirtualFreeEx(handle, codeCave, 0, Native.MemRelease);
        }
    }

    private void AdvanceFactoryProduction(uint factory)
    {
        if (ReadInt32(factory + FactoryProductionValueOffset) >= 54)
            return;
        WriteInt32(factory + FactoryProductionValueOffset, 53);
        WriteBytes(factory + FactoryProductionChangedOffset, [0]);
        WriteInt32(factory + FactoryProductionTimerStartOffset, ReadInt32(CurrentFrame) - 1);
        WriteInt32(factory + FactoryProductionTimerTimeLeftOffset, 0);
        WriteInt32(factory + FactoryProductionRateOffset, 1);
        WriteInt32(factory + FactoryProductionStepOffset, 1);
    }

    private int CountPlacedBuildings(uint house, uint typePointer)
    {
        var count = 0;
        foreach (var building in ReadVector(house + HouseBuildingsOffset, 4096))
        {
            if (ReadUInt32(building + BuildingTypeOffset) == typePointer &&
                ReadByte(building + ObjectIsOnMapOffset) != 0 &&
                ReadByte(building + ObjectInLimboOffset) == 0 &&
                ReadByte(building + ObjectIsAliveOffset) != 0)
                count++;
        }
        return count;
    }

    private byte[] CreateProductionEvent(byte eventType, int typeIndex)
    {
        var eventData = CreateEvent(eventType);
        BitConverter.GetBytes(7).CopyTo(eventData, 7); // AbstractType::BuildingType
        BitConverter.GetBytes(typeIndex).CopyTo(eventData, 11);
        BitConverter.GetBytes(0).CopyTo(eventData, 15); // IsNaval
        return eventData;
    }

    private byte[] CreatePlaceEvent(int typeIndex, (short X, short Y) cell)
    {
        var eventData = CreateProductionEvent(0x0B, typeIndex); // EventType::Place
        BitConverter.GetBytes(cell.X).CopyTo(eventData, 19);
        BitConverter.GetBytes(cell.Y).CopyTo(eventData, 21);
        return eventData;
    }

}
