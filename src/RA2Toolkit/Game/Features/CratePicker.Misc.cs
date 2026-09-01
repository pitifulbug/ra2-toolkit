using System.ComponentModel;
using System.Runtime.InteropServices;

internal sealed partial class CratePicker
{
    private const long GameSpeedValue = 0xA8EDDC;
    private const long ImmediateVictoryFlag = 0xA83D49;
    private const long SpyVsSpyOwnerSelection = 0x51A002;
    private const int TechnoForceShieldedOffset = 0x1C4;
    private const int TechnoIronCurtainTimerTimeLeftOffset = 0x194;
    private static readonly byte[] SpyVsSpyOriginal = Convert.FromHexString("8B861C020000");

    private bool gameSpeedOverridden;
    private byte originalGameSpeed;
    private byte appliedGameSpeed;
    private bool spyVsSpyEnabled;
    private bool spyVsSpyPatchInstalled;
    private nint spyVsSpyCodeCave;
    private bool disableShieldsEnabled;
    private DateTime nextShieldRefreshAt = DateTime.MinValue;

    private int? SetGameSpeed(OverlayCommandRequest request)
    {
        var userSpeed = RequireWholeNumber(request, 0, 6, "游戏速度");
        var internalSpeed = checked((byte)(6 - userSpeed));
        var previousValue = ReadByte(GameSpeedValue);
        var previousOriginal = originalGameSpeed;
        var previousApplied = appliedGameSpeed;
        var previouslyOwned = gameSpeedOverridden && previousValue == appliedGameSpeed;

        originalGameSpeed = previouslyOwned ? originalGameSpeed : previousValue;
        appliedGameSpeed = internalSpeed;
        gameSpeedOverridden = true;
        try
        {
            WriteBytes(GameSpeedValue, [internalSpeed]);
            if (ReadByte(GameSpeedValue) != internalSpeed)
                throw new InvalidOperationException("游戏速度写入后校验失败。");
        }
        catch (Exception writeError) when (writeError is Win32Exception or
                                           InvalidOperationException)
        {
            try
            {
                var current = ReadByte(GameSpeedValue);
                if (current == internalSpeed)
                    WriteBytes(GameSpeedValue, [previousValue]);
                if (ReadByte(GameSpeedValue) != previousValue)
                    throw new InvalidOperationException("游戏速度回滚后校验失败。");

                gameSpeedOverridden = previouslyOwned;
                originalGameSpeed = previousOriginal;
                appliedGameSpeed = previousApplied;
            }
            catch (Exception rollbackError) when (rollbackError is Win32Exception or
                                                  InvalidOperationException)
            {
                // Keep ownership of the attempted value. A later cleanup only restores
                // when the byte still equals appliedGameSpeed, so external changes stay safe.
                throw new InvalidOperationException(
                    "游戏速度写入失败，且未能完整回滚。",
                    new AggregateException(writeError, rollbackError));
            }
            throw new InvalidOperationException("游戏速度写入失败，已恢复原值。", writeError);
        }
        return userSpeed;
    }

    private int WinImmediately()
    {
        WriteBytes(ImmediateVictoryFlag, [1]);
        if (ReadByte(ImmediateVictoryFlag) != 1)
            throw new InvalidOperationException("胜利状态写入后校验失败。");
        return 1;
    }

    private void ToggleSpyVsSpy()
    {
        if (spyVsSpyEnabled || spyVsSpyPatchInstalled)
        {
            DisableSpyVsSpy();
            return;
        }

        const int caveSize = 64;
        var cave = Native.VirtualAllocEx(handle, 0, caveSize,
            Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
        if (cave == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "分配无间道代码区失败");
        var mayBePublished = false;
        try
        {
            var code = new List<byte>(40)
            {
                0x9C,                               // pushfd
                0x8B, 0x87, 0x1C, 0x02, 0x00, 0x00, // mov eax,[edi+21C]
                0x3B, 0x05                          // cmp eax,[CurrentPlayer]
            };
            code.AddRange(BitConverter.GetBytes(checked((uint)CurrentPlayer)));
            code.AddRange([0x75, 0x07, 0xA1]);       // jne normal; mov eax,[CurrentPlayer]
            code.AddRange(BitConverter.GetBytes(checked((uint)CurrentPlayer)));
            code.AddRange([0xEB, 0x06]);             // jmp common
            code.AddRange(SpyVsSpyOriginal);         // normal: mov eax,[esi+21C]
            code.AddRange([0x9D, 0xE9]);             // common: popfd; jmp back
            code.AddRange(BitConverter.GetBytes(checked((int)
                (SpyVsSpyOwnerSelection + SpyVsSpyOriginal.Length -
                 (cave.ToInt64() + code.Count + sizeof(int))))));
            WriteCodeCave(cave, [.. code]);
            var patch = CreateRelativePatch(SpyVsSpyOwnerSelection,
                SpyVsSpyOriginal.Length, cave.ToInt64());
            PublishCodePatch(SpyVsSpyOwnerSelection, SpyVsSpyOriginal, patch,
                "无间道", ref mayBePublished);
            spyVsSpyCodeCave = cave;
            spyVsSpyPatchInstalled = true;
            spyVsSpyEnabled = true;
        }
        catch
        {
            if (mayBePublished)
            {
                var patch = CreateRelativePatch(SpyVsSpyOwnerSelection,
                    SpyVsSpyOriginal.Length, cave.ToInt64());
                if (TryRollbackFailedCodePatch(SpyVsSpyOwnerSelection,
                        SpyVsSpyOriginal, patch))
                    RetireCodeCave(cave);
                else
                {
                    spyVsSpyCodeCave = cave;
                    spyVsSpyPatchInstalled = true;
                    spyVsSpyEnabled = true;
                }
            }
            else
                FreeUnpublishedCodeCaveBestEffort(cave);
            throw;
        }
    }

    private void DisableSpyVsSpy()
    {
        if (!spyVsSpyPatchInstalled)
        {
            spyVsSpyEnabled = false;
            return;
        }
        var patch = CreateRelativePatch(SpyVsSpyOwnerSelection,
            SpyVsSpyOriginal.Length, spyVsSpyCodeCave.ToInt64());
        if (!RestoreOwnedCodePatch(SpyVsSpyOwnerSelection, SpyVsSpyOriginal, patch))
            throw new InvalidOperationException("无间道补丁已被其他程序修改，未覆盖外部更改。");
        spyVsSpyPatchInstalled = false;
        spyVsSpyEnabled = false;
        RetireCodeCave(ref spyVsSpyCodeCave);
    }

    private int GrantNuclearMissile()
    {
        var house = ReadUInt32(CurrentPlayer);
        if (house == 0)
            throw new InvalidOperationException("当前玩家阵营指针无效。");
        var granted = 0;
        WithSuspendedProcess(() =>
        {
            foreach (var super in ReadVector(house + HouseSupersOffset, 256))
            {
                var type = ReadUInt32(super + SuperTypeOffset);
                if (type == 0 || ReadInt32(type + SuperWeaponTypeKindOffset) != 0)
                    continue;
                WriteBytes(super + SuperIsPresentOffset, [1]);
                WriteBytes(super + SuperIsReadyOffset, [1]);
                WriteBytes(super + SuperIsSuspendedOffset, [0]);
                WriteInt32(super + SuperRechargeStartOffset, 0);
                WriteInt32(super + SuperRechargeTimeLeftOffset, 0);
                granted++;
                break;
            }
        });
        if (granted == 0)
            throw new InvalidOperationException("当前阵营没有可授予的核弹超级武器实例。");
        return granted;
    }

    private void ToggleDisableShields()
    {
        disableShieldsEnabled = !disableShieldsEnabled;
        nextShieldRefreshAt = DateTime.MinValue;
        if (disableShieldsEnabled)
            MaintainDisabledShields();
    }

    private void MaintainDisabledShields()
    {
        var now = DateTime.UtcNow;
        if (now < nextShieldRefreshAt)
            return;
        nextShieldRefreshAt = now + TimeSpan.FromMilliseconds(100);
        WithSuspendedProcess(() =>
        {
            foreach (var techno in ReadVector(TechnoArray, 10000))
            {
                if (ReadInt32(techno + TechnoForceShieldedOffset) == 0)
                    continue;
                WriteInt32(techno + TechnoForceShieldedOffset, 0);
                WriteInt32(techno + TechnoIronCurtainTimerTimeLeftOffset, 0);
            }
        });
    }

    private void RestoreMiscOverrides()
    {
        try { DisableSpyVsSpy(); }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                      GameProcessExitedException)
        {
            Console.Error.WriteLine($"[杂项功能清理失败] {error.Message}");
        }
        disableShieldsEnabled = false;
        try
        {
            if (gameSpeedOverridden)
            {
                var current = ReadByte(GameSpeedValue);
                if (current != appliedGameSpeed)
                    gameSpeedOverridden = false;
                else
                {
                    WriteBytes(GameSpeedValue, [originalGameSpeed]);
                    if (ReadByte(GameSpeedValue) != originalGameSpeed)
                        throw new InvalidOperationException("游戏速度恢复后校验失败。");
                    gameSpeedOverridden = false;
                }
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or
                                      GameProcessExitedException)
        {
            Console.Error.WriteLine($"[游戏速度恢复失败] {error.Message}");
        }
    }
}
