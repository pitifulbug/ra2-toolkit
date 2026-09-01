using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private PendingLogicUpdatePatch? pendingLogicUpdatePatch;

    private void ToggleRevealMap()
    {
        if (revealMapEnabled)
        {
            DisableRevealMap();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine("[地图全开未开启] 当前玩家阵营指针无效。");
            return;
        }

        try
        {
            InvokeRevealMapLikeCrate(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            Console.WriteLine($"[地图全开未开启] {error.Message}");
            return;
        }
        revealMapEnabled = true;
        Console.WriteLine("[地图全开已开启] 已调用游戏原生的全图视野箱效果。");
    }

    private void InvokeRevealMapLikeCrate(uint house)
    {
        RestorePendingLogicUpdatePatch();
        if (ReadUInt32(CurrentPlayer) != house)
            throw new InvalidOperationException("当前玩家阵营已经变化，已停止揭开地图。");
        if (!ReadBytes(RevealMapLikeCrate, RevealMapLikeCrateFingerprint.Length)
                .AsSpan().SequenceEqual(RevealMapLikeCrateFingerprint))
            throw new InvalidOperationException("游戏原生揭图函数指纹不匹配，未执行。");
        if (!ReadBytes(LogicUpdate, LogicUpdateOriginalBytes.Length)
                .AsSpan().SequenceEqual(LogicUpdateOriginalBytes))
            throw new InvalidOperationException("游戏主循环函数指纹不匹配，未执行。");

        const int codeCaveSize = 128;
        var codeCave = Native.VirtualAllocEx(handle, 0, codeCaveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (codeCave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配原生揭图调用区失败");

        var markerAddress = codeCave.ToInt64() + 112;
        var mayBePublished = false;
        byte[]? installedJump = null;
        try
        {
            var code = new List<byte>(112) { 0x60 }; // pushad
            code.AddRange(Convert.FromHexString("833D")); // already completed
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.Add(0);
            var completedJump = code.Count;
            code.AddRange([0x0F, 0x85, 0, 0, 0, 0]);
            code.Add(0xA1); // mov eax,[CurrentPlayer]
            code.AddRange(BitConverter.GetBytes(checked((uint)CurrentPlayer)));
            code.Add(0x3D); // cmp eax,expected HouseClass*
            code.AddRange(BitConverter.GetBytes(house));
            var houseChangedJump = code.Count;
            code.AddRange([0x0F, 0x85, 0, 0, 0, 0]);
            code.Add(0xB9); // mov ecx, MapClass::Instance
            code.AddRange(BitConverter.GetBytes(checked((uint)Map)));
            code.Add(0x68); // push HouseClass*
            code.AddRange(BitConverter.GetBytes(house));
            code.Add(0xB8); // mov eax, MapClass::Reveal
            code.AddRange(BitConverter.GetBytes(checked((uint)RevealMapLikeCrate)));
            code.AddRange([0xFF, 0xD0]); // call eax
            code.AddRange([0xC7, 0x05]); // completion marker
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.AddRange(BitConverter.GetBytes(1));
            var successJump = code.Count;
            code.AddRange([0xE9, 0, 0, 0, 0]);
            var houseChanged = code.Count;
            code.AddRange([0xC7, 0x05]);
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.AddRange(BitConverter.GetBytes(2));
            var completed = code.Count;
            code.Add(0x61); // popad
            code.AddRange(LogicUpdateOriginalBytes);
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (LogicUpdate + LogicUpdateOriginalBytes.Length -
                 (codeCave.ToInt64() + code.Count + 4)))));
            var completedDisplacement = BitConverter.GetBytes(completed - (completedJump + 6));
            for (var index = 0; index < completedDisplacement.Length; index++)
                code[completedJump + 2 + index] = completedDisplacement[index];
            var houseChangedDisplacement = BitConverter.GetBytes(
                houseChanged - (houseChangedJump + 6));
            var successDisplacement = BitConverter.GetBytes(
                completed - (successJump + 5));
            for (var index = 0; index < sizeof(int); index++)
            {
                code[houseChangedJump + 2 + index] = houseChangedDisplacement[index];
                code[successJump + 1 + index] = successDisplacement[index];
            }
            if (code.Count > 112)
                throw new InvalidOperationException("原生揭图调用区容量不足。");
            WriteCodeCave(codeCave, [.. code]);
            installedJump = CreateRelativePatch(LogicUpdate,
                LogicUpdateOriginalBytes.Length, codeCave.ToInt64());
            PublishCodePatch(LogicUpdate, LogicUpdateOriginalBytes, installedJump,
                "游戏主循环", ref mayBePublished);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline && ReadInt32(markerAddress) == 0)
                Thread.Sleep(10);
            var result = ReadInt32(markerAddress);
            if (result == 2)
                throw new InvalidOperationException("当前玩家阵营在执行揭图前已经变化。");
            if (result != 1)
                throw new InvalidOperationException("等待游戏主线程执行原生揭图逻辑超时。");
        }
        finally
        {
            if (mayBePublished && installedJump is not null)
            {
                var restored = false;
                try { restored = RestoreOwnedCodePatch(LogicUpdate, LogicUpdateOriginalBytes, installedJump); }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                               GameProcessExitedException)
                {
                }
                if (restored)
                    RetireCodeCave(codeCave);
                else
                    pendingLogicUpdatePatch = new PendingLogicUpdatePatch(codeCave, installedJump);
            }
            else
                FreeUnpublishedCodeCaveBestEffort(codeCave);
        }
    }

    private void InvokeHouseReshroudMap(uint house)
    {
        RestorePendingLogicUpdatePatch();
        if (ReadUInt32(CurrentPlayer) != house)
            throw new InvalidOperationException("当前玩家阵营已经变化，已停止恢复地图迷雾。");
        if (!ReadBytes(LogicUpdate, LogicUpdateOriginalBytes.Length)
                .AsSpan().SequenceEqual(LogicUpdateOriginalBytes))
            throw new InvalidOperationException("游戏主循环函数指纹不匹配，未执行。");

        const int codeCaveSize = 128;
        var codeCave = Native.VirtualAllocEx(handle, 0, codeCaveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (codeCave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配原生恢复迷雾调用区失败");

        var markerAddress = codeCave.ToInt64() + 112;
        var mayBePublished = false;
        byte[]? installedJump = null;
        try
        {
            var code = new List<byte>(112) { 0x60 }; // pushad
            code.AddRange(Convert.FromHexString("833D")); // already completed
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.Add(0);
            var completedJump = code.Count;
            code.AddRange([0x0F, 0x85, 0, 0, 0, 0]);
            code.Add(0xA1); // mov eax,[CurrentPlayer]
            code.AddRange(BitConverter.GetBytes(checked((uint)CurrentPlayer)));
            code.Add(0x3D); // cmp eax,expected HouseClass*
            code.AddRange(BitConverter.GetBytes(house));
            var houseChangedJump = code.Count;
            code.AddRange([0x0F, 0x85, 0, 0, 0, 0]);
            code.Add(0xB9); // mov ecx, HouseClass*
            code.AddRange(BitConverter.GetBytes(checked((uint)house)));
            code.Add(0xB8); // mov eax, HouseClass::ReshroudMap
            code.AddRange(BitConverter.GetBytes(checked((uint)HouseReshroudMap)));
            code.AddRange([0xFF, 0xD0]); // call eax
            code.AddRange([0xC7, 0x05]); // completion marker
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.AddRange(BitConverter.GetBytes(1));
            var successJump = code.Count;
            code.AddRange([0xE9, 0, 0, 0, 0]);
            var houseChanged = code.Count;
            code.AddRange([0xC7, 0x05]);
            code.AddRange(BitConverter.GetBytes(checked((uint)markerAddress)));
            code.AddRange(BitConverter.GetBytes(2));
            var completed = code.Count;
            code.Add(0x61); // popad
            code.AddRange(LogicUpdateOriginalBytes);
            code.Add(0xE9);
            code.AddRange(BitConverter.GetBytes(checked((int)
                (LogicUpdate + LogicUpdateOriginalBytes.Length -
                 (codeCave.ToInt64() + code.Count + 4)))));
            var completedDisplacement = BitConverter.GetBytes(completed - (completedJump + 6));
            for (var index = 0; index < completedDisplacement.Length; index++)
                code[completedJump + 2 + index] = completedDisplacement[index];
            var houseChangedDisplacement = BitConverter.GetBytes(
                houseChanged - (houseChangedJump + 6));
            var successDisplacement = BitConverter.GetBytes(
                completed - (successJump + 5));
            for (var index = 0; index < sizeof(int); index++)
            {
                code[houseChangedJump + 2 + index] = houseChangedDisplacement[index];
                code[successJump + 1 + index] = successDisplacement[index];
            }
            if (code.Count > 112)
                throw new InvalidOperationException("原生恢复迷雾调用区容量不足。");
            WriteCodeCave(codeCave, [.. code]);
            installedJump = CreateRelativePatch(LogicUpdate,
                LogicUpdateOriginalBytes.Length, codeCave.ToInt64());
            PublishCodePatch(LogicUpdate, LogicUpdateOriginalBytes, installedJump,
                "游戏主循环", ref mayBePublished);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline && ReadInt32(markerAddress) == 0)
                Thread.Sleep(10);
            var result = ReadInt32(markerAddress);
            if (result == 2)
                throw new InvalidOperationException("当前玩家阵营在执行恢复迷雾前已经变化。");
            if (result != 1)
                throw new InvalidOperationException("等待游戏主线程执行恢复迷雾逻辑超时。");
        }
        finally
        {
            if (mayBePublished && installedJump is not null)
            {
                var restored = false;
                try { restored = RestoreOwnedCodePatch(LogicUpdate, LogicUpdateOriginalBytes, installedJump); }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                               GameProcessExitedException)
                {
                }
                if (restored)
                    RetireCodeCave(codeCave);
                else
                    pendingLogicUpdatePatch = new PendingLogicUpdatePatch(codeCave, installedJump);
            }
            else
                FreeUnpublishedCodeCaveBestEffort(codeCave);
        }
    }

    private void RestorePendingLogicUpdatePatch()
    {
        if (pendingLogicUpdatePatch is not { } pending)
            return;
        if (!RestoreOwnedCodePatch(LogicUpdate, LogicUpdateOriginalBytes, pending.InstalledBytes))
        {
            throw new InvalidOperationException(
                "游戏主循环临时补丁仍未恢复；为避免覆盖外部修改，已停止后续原生调用。");
        }
        RetireCodeCave(pending.CodeCave);
        pendingLogicUpdatePatch = null;
    }

    private sealed record PendingLogicUpdatePatch(nint CodeCave, byte[] InstalledBytes);

    private void DisableRevealMap()
    {
        if (!revealMapEnabled)
            return;

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            throw new InvalidOperationException("当前玩家阵营指针无效，无法恢复地图迷雾。");
        InvokeHouseReshroudMap(house);
        revealMapEnabled = false;
        Console.WriteLine("[地图全开已关闭] 已调用无卫星时的原生逻辑恢复战争迷雾。");
    }

    private void DisableRevealMapBestEffort()
    {
        try
        {
            DisableRevealMap();
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                      GameProcessExitedException)
        {
            revealMapEnabled = false;
            Console.WriteLine($"[地图全开清理失败] {error.Message}");
        }
    }

    private void ToggleInfiniteMoney()
    {
        if (infiniteMoneyEnabled)
        {
            DisableInfiniteMoney();
            return;
        }

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine("[无限资金未开启] 当前玩家阵营指针无效。");
            return;
        }

        try
        {
            EnsureInfiniteMoney(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            Console.WriteLine($"[无限资金未开启] {error.Message}");
            return;
        }

        infiniteMoneyEnabled = true;
        nextInfiniteMoneyRefreshAt = DateTime.MinValue;
        Console.WriteLine($"[无限资金已开启] 我方资金下限为 {InfiniteMoneyFloor:N0}；在控制面板中取消勾选即可关闭。");
    }

    private void MaintainInfiniteMoney()
    {
        var now = DateTime.UtcNow;
        if (now < nextInfiniteMoneyRefreshAt)
            return;
        nextInfiniteMoneyRefreshAt = now + TimeSpan.FromMilliseconds(100);

        try
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            if (ReadInt32(house + HouseBalanceOffset) < InfiniteMoneyFloor)
                EnsureInfiniteMoney(house);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            nextInfiniteMoneyRefreshAt = now + TimeSpan.FromSeconds(1);
        }
    }

    private void EnsureInfiniteMoney(uint house)
    {
        var suspended = false;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;
            if (ReadUInt32(CurrentPlayer) != house)
                throw new InvalidOperationException("当前玩家阵营已经变化，已停止写入资金。");
            if (ReadInt32(house + HouseBalanceOffset) < InfiniteMoneyFloor)
                WriteInt32(house + HouseBalanceOffset, InfiniteMoneyFloor);
        }
        finally
        {
            if (suspended)
                ResumeSuspendedProcessOrThrow();
        }
    }

    private void DisableInfiniteMoney()
    {
        if (!infiniteMoneyEnabled)
            return;
        infiniteMoneyEnabled = false;
        Console.WriteLine("[无限资金已关闭] 当前余额保留，后续消费不再自动补充。");
    }

    private void ToggleOneHitKill()
    {
        if (oneHitKillEnabled)
        {
            DisableOneHitKill();
            return;
        }

        EnableCombatBoost(enableOneHitKill: true);
    }

    private void ToggleHighDefense()
    {
        if (highDefenseEnabled)
        {
            DisableHighDefense();
            return;
        }

        EnableCombatBoost(enableOneHitKill: false);
    }

    private void EnableCombatBoost(bool enableOneHitKill)
    {
        var featureName = enableOneHitKill ? "秒杀" : "高防御";

        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
        {
            Console.WriteLine($"[{featureName}未开启] 当前玩家阵营指针无效。");
            return;
        }

        var combatBoostAlreadyEnabled = oneHitKillEnabled || highDefenseEnabled;
        if (!combatBoostAlreadyEnabled || house != oneHitKillHouse)
            oneHitKillObjects.Clear();

        int affected;
        try
        {
            affected = ApplyCombatBoostToOwnedTechnos(
                house,
                oneHitKillEnabled || enableOneHitKill,
                highDefenseEnabled || !enableOneHitKill);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            if (!combatBoostAlreadyEnabled)
                oneHitKillObjects.Clear();
            Console.WriteLine($"[{featureName}未开启] {error.Message}");
            return;
        }

        oneHitKillHouse = house;
        if (enableOneHitKill)
            oneHitKillEnabled = true;
        else
            highDefenseEnabled = true;
        nextOneHitKillRefreshAt = DateTime.MinValue;
        Console.WriteLine($"[{featureName}已开启] 已修改 {affected} 个现有单位/建筑；新单位会自动加入。取消勾选即可恢复。");
    }

    private void MaintainCombatBoost()
    {
        var now = DateTime.UtcNow;
        if (now < nextOneHitKillRefreshAt)
            return;
        nextOneHitKillRefreshAt = now + TimeSpan.FromMilliseconds(250);

        try
        {
            var house = ReadUInt32(CurrentPlayer);
            if (house == 0)
                return;
            if (house != oneHitKillHouse)
            {
                oneHitKillObjects.Clear();
                oneHitKillHouse = house;
            }
            ApplyCombatBoostToOwnedTechnos(house, oneHitKillEnabled, highDefenseEnabled);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            nextOneHitKillRefreshAt = now + TimeSpan.FromSeconds(1);
        }
    }

    private void DisableOneHitKill()
    {
        if (!oneHitKillEnabled)
            return;

        DisableCombatBoost(disableOneHitKill: true);
    }

    private void DisableHighDefense()
    {
        if (!highDefenseEnabled)
            return;

        DisableCombatBoost(disableOneHitKill: false);
    }

    private void DisableCombatBoost(bool disableOneHitKill)
    {
        var featureName = disableOneHitKill ? "秒杀" : "高防御";
        var restored = 0;
        var restoreFailed = false;
        try
        {
            if (!IsGameProcessUnavailable())
                restored = RestoreCombatBoostObjects(
                    restoreFirepower: disableOneHitKill,
                    restoreArmor: !disableOneHitKill);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or GameProcessExitedException)
        {
            restoreFailed = true;
            Console.WriteLine($"[{featureName}恢复失败] {error.Message}");
        }
        finally
        {
            if (disableOneHitKill)
                oneHitKillEnabled = false;
            else
                highDefenseEnabled = false;

            if (!oneHitKillEnabled && !highDefenseEnabled)
            {
                oneHitKillHouse = 0;
                oneHitKillObjects.Clear();
            }
            Console.WriteLine(restoreFailed
                ? $"[{featureName}已关闭] 未能写回原倍率；请重新开始对局或重启游戏。"
                : $"[{featureName}已关闭] 已恢复 {restored} 个仍然存在的我方单位/建筑。");
        }
    }

    private int ApplyCombatBoostToOwnedTechnos(
        uint house, bool applyOneHitKill, bool applyHighDefense)
    {
        var suspended = false;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;
            if (ReadUInt32(CurrentPlayer) != house)
                throw new InvalidOperationException("当前玩家阵营已经变化，已停止写入单位攻防倍率。");

            var items = ReadUInt32(TechnoArray + 4);
            var count = ReadInt32(TechnoArray + 16);
            if (items == 0 || count is < 0 or > 10000)
                throw new InvalidOperationException($"TechnoClass 列表异常：{count}。");

            var affected = 0;
            for (var index = 0; index < count; index++)
            {
                var pointer = ReadUInt32(items + index * 4L);
                if (pointer == 0 || ReadUInt32(pointer + TechnoOwnerOffset) != house)
                    continue;
                var id = ReadInt32(pointer + 0x10);
                if (id <= 0)
                    continue;

                if (!oneHitKillObjects.TryGetValue(pointer, out var state) || state.Id != id)
                {
                    var originalFirepower = ReadDouble(pointer + TechnoFirepowerMultiplierOffset);
                    if (originalFirepower == LegacyOverflowingFirepowerMultiplier)
                        originalFirepower = 1.0;
                    var originalArmor = ReadDouble(pointer + TechnoArmorMultiplierOffset);
                    if (!IsReasonableFirepowerMultiplier(originalFirepower) ||
                        !IsReasonableFirepowerMultiplier(originalArmor))
                        continue;
                    state = new OneHitKillObjectState(id, originalFirepower, originalArmor);
                    oneHitKillObjects[pointer] = state;
                }

                if (applyOneHitKill &&
                    ReadDouble(pointer + TechnoFirepowerMultiplierOffset) != OneHitKillFirepowerMultiplier)
                    WriteBytes(pointer + TechnoFirepowerMultiplierOffset,
                        BitConverter.GetBytes(OneHitKillFirepowerMultiplier));
                if (applyHighDefense &&
                    ReadDouble(pointer + TechnoArmorMultiplierOffset) != ExtremeDefenseArmorMultiplier)
                    WriteBytes(pointer + TechnoArmorMultiplierOffset,
                        BitConverter.GetBytes(ExtremeDefenseArmorMultiplier));
                affected++;
            }
            return affected;
        }
        finally
        {
            if (suspended)
                ResumeSuspendedProcessOrThrow();
        }
    }

    private int RestoreCombatBoostObjects(bool restoreFirepower, bool restoreArmor)
    {
        var suspended = false;
        try
        {
            SuspendProcessOrThrow();
            suspended = true;
            var items = ReadUInt32(TechnoArray + 4);
            var count = ReadInt32(TechnoArray + 16);
            if (items == 0 || count is < 0 or > 10000)
                throw new InvalidOperationException($"TechnoClass 列表异常：{count}。");

            var restored = 0;
            for (var index = 0; index < count; index++)
            {
                var pointer = ReadUInt32(items + index * 4L);
                if (pointer == 0 || !oneHitKillObjects.TryGetValue(pointer, out var state) ||
                    ReadInt32(pointer + 0x10) != state.Id ||
                    ReadUInt32(pointer + TechnoOwnerOffset) != oneHitKillHouse)
                    continue;
                if (restoreFirepower)
                    WriteBytes(pointer + TechnoFirepowerMultiplierOffset,
                        BitConverter.GetBytes(state.OriginalFirepowerMultiplier));
                if (restoreArmor)
                    WriteBytes(pointer + TechnoArmorMultiplierOffset,
                        BitConverter.GetBytes(state.OriginalArmorMultiplier));
                restored++;
            }
            return restored;
        }
        finally
        {
            if (suspended)
                ResumeSuspendedProcessOrThrow();
        }
    }

    internal static bool IsReasonableFirepowerMultiplier(double value) =>
        double.IsFinite(value) && value is > 0.0 and <= 1000.0;

}
