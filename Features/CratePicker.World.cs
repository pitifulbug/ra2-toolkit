using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private void ToggleRevealMap()
    {
        if (revealMapEnabled)
        {
            revealMapEnabled = false;
            Console.WriteLine("[地图全开已关闭] 已停止该功能；原生视野箱已经揭开的地图会保留。");
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
        if (ReadUInt32(CurrentPlayer) != house)
            throw new InvalidOperationException("当前玩家阵营已经变化，已停止揭开地图。");
        if (!ReadBytes(RevealMapLikeCrate, RevealMapLikeCrateFingerprint.Length)
                .AsSpan().SequenceEqual(RevealMapLikeCrateFingerprint))
            throw new InvalidOperationException("游戏原生揭图函数指纹不匹配，未执行。");
        if (!ReadBytes(LogicUpdate, LogicUpdateOriginalBytes.Length)
                .AsSpan().SequenceEqual(LogicUpdateOriginalBytes))
            throw new InvalidOperationException("游戏主循环函数指纹不匹配，未执行。");

        const int codeCaveSize = 96;
        var codeCave = Native.VirtualAllocEx(handle, 0, codeCaveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (codeCave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配原生揭图调用区失败");

        var markerAddress = codeCave.ToInt64() + 80;
        var protectionChanged = false;
        uint previousProtection = 0;
        var canFreeCodeCave = true;
        try
        {
            var code = new List<byte>(80) { 0x60 }; // pushad
            code.AddRange([0xC7, 0x05]); // restore LogicClass::Update before calling Reveal
            code.AddRange(BitConverter.GetBytes(checked((uint)LogicUpdate)));
            code.AddRange(LogicUpdateOriginalBytes.AsSpan(0, 4).ToArray());
            code.AddRange([0xC7, 0x05]);
            code.AddRange(BitConverter.GetBytes(checked((uint)(LogicUpdate + 4))));
            code.AddRange(LogicUpdateOriginalBytes.AsSpan(4, 4).ToArray());
            code.AddRange([0xC6, 0x05]);
            code.AddRange(BitConverter.GetBytes(checked((uint)(LogicUpdate + 8))));
            code.Add(LogicUpdateOriginalBytes[8]);
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
                throw new Win32Exception(Marshal.GetLastWin32Error(), "刷新主循环入口跳转失败");

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < deadline && ReadInt32(markerAddress) != 1)
                Thread.Sleep(10);
            if (ReadInt32(markerAddress) != 1)
            {
                canFreeCodeCave = false;
                throw new InvalidOperationException("等待游戏主线程执行原生揭图逻辑超时。");
            }
            Thread.Sleep(50);
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

    private void DisableRevealMap()
    {
        if (!revealMapEnabled)
            return;
        revealMapEnabled = false;
        Console.WriteLine("[地图全开已关闭] 原生视野箱已经揭开的地图会保留。");
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
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
            suspended = true;
            if (ReadUInt32(CurrentPlayer) != house)
                throw new InvalidOperationException("当前玩家阵营已经变化，已停止写入资金。");
            if (ReadInt32(house + HouseBalanceOffset) < InfiniteMoneyFloor)
                WriteInt32(house + HouseBalanceOffset, InfiniteMoneyFloor);
        }
        finally
        {
            if (suspended)
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
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
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
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
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private int RestoreCombatBoostObjects(bool restoreFirepower, bool restoreArmor)
    {
        var suspended = false;
        try
        {
            CheckNtStatus(Native.NtSuspendProcess(handle), "暂停游戏进程失败");
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
                CheckNtStatus(Native.NtResumeProcess(handle), "恢复游戏进程失败");
        }
    }

    private static bool IsReasonableFirepowerMultiplier(double value) =>
        double.IsFinite(value) && value is > 0.0 and <= 1000.0;

}
