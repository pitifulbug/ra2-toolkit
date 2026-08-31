using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal sealed partial class CratePicker
{
    private void Tick()
    {
        var now = DateTime.UtcNow;
        for (var index = units.Count - 1; index >= 0; index--)
        {
            var state = units[index];
            if (IsCapturedUnitValid(state.Unit))
            {
                state.InvalidSince = null;
                continue;
            }

            if (state.InvalidSince is null)
            {
                state.InvalidSince = now;
                pendingMissions.Remove(state.Unit.Id);
                state.ActiveCrate = null;
                state.LastCommandAt = DateTime.MinValue;
                ResetTargetProgress(state);
                ResetWaitingState(state);
                continue;
            }

            if (now - state.InvalidSince.Value < TimeSpan.FromSeconds(2))
                continue;

            units.RemoveAt(index);
        }

        if (units.Count == 0)
        {
            DisableCrateActionLines();
            return;
        }

        var usableUnits = units.Where(state => state.InvalidSince is null).ToArray();
        if (usableUnits.Length == 0)
        {
            RefreshCrateActionLines([]);
            return;
        }

        var crates = ReadActiveCrates();
        var crateByKey = crates.ToDictionary(crate => new CrateKey(crate.Index, crate.X, crate.Y));
        foreach (var expired in recentlyCollected
                     .Where(entry => entry.Value <= now)
                     .Select(entry => entry.Key)
                     .ToArray())
            recentlyCollected.Remove(expired);

        var claimed = new HashSet<CrateKey>();
        var collectedThisTick = new HashSet<CrateKey>();
        foreach (var state in usableUnits)
        {
            foreach (var expired in state.UnreachableCrates
                         .Where(entry => entry.Value <= now)
                         .Select(entry => entry.Key)
                         .ToArray())
                state.UnreachableCrates.Remove(expired);

            var location = ReadUnitCell(state.Unit.Pointer);
            if (state.ActiveCrate is { } reached &&
                DistanceSquared(location, (reached.X, reached.Y)) == 0)
            {
                var reachedKey = new CrateKey(reached.Index, reached.X, reached.Y);
                collectedThisTick.Add(reachedKey);
                recentlyCollected[reachedKey] = now + TimeSpan.FromSeconds(3);
                state.ActiveCrate = null;
                state.LastCommandAt = DateTime.MinValue;
                ResetTargetProgress(state);
            }

            if (state.ActiveCrate is { } previous)
            {
                var previousKey = new CrateKey(previous.Index, previous.X, previous.Y);
                if (!crateByKey.ContainsKey(previousKey) || !claimed.Add(previousKey))
                {
                    state.ActiveCrate = null;
                    state.LastCommandAt = DateTime.MinValue;
                    ResetTargetProgress(state);
                    QueueGuard(state.Unit);
                }
            }

            if (state.ActiveCrate is { } target)
            {
                if (location != state.LastTargetObservedCell)
                {
                    state.LastTargetObservedCell = location;
                    state.LastTargetProgressAt = now;
                }
                else if (now - state.LastTargetProgressAt >= TimeSpan.FromSeconds(3))
                {
                    var key = new CrateKey(target.Index, target.X, target.Y);
                    state.UnreachableCrates[key] = now + TimeSpan.FromSeconds(8);
                    state.ActiveCrate = null;
                    state.LastCommandAt = DateTime.MinValue;
                    ResetTargetProgress(state);
                }
            }

        }

        if (collectedThisTick.Count != 0)
        {
            foreach (var state in usableUnits)
            {
                if (state.ActiveCrate is not { } target ||
                    !collectedThisTick.Contains(new CrateKey(target.Index, target.X, target.Y)))
                    continue;
                claimed.Remove(new CrateKey(target.Index, target.X, target.Y));
                state.ActiveCrate = null;
                state.LastCommandAt = DateTime.MinValue;
                ResetTargetProgress(state);
                QueueGuard(state.Unit);
            }
        }

        foreach (var state in usableUnits)
        {
            var location = ReadUnitCell(state.Unit.Pointer);
            if (state.ActiveCrate is null)
            {
                var nearest = crates
                    .Where(crate =>
                    {
                        var key = new CrateKey(crate.Index, crate.X, crate.Y);
                        return !recentlyCollected.ContainsKey(key) &&
                               !claimed.Contains(key) &&
                               !state.UnreachableCrates.ContainsKey(key);
                    })
                    .MinBy(crate => DistanceSquared(location, (crate.X, crate.Y)));

                if (nearest is not null)
                {
                    ResetWaitingState(state);
                    QueueMove(state.Unit, nearest.X, nearest.Y);
                    state.ActiveCrate = nearest;
                    state.LastCommandAt = now;
                    state.LastTargetObservedCell = location;
                    state.LastTargetProgressAt = now;
                    claimed.Add(new CrateKey(nearest.Index, nearest.X, nearest.Y));
                    continue;
                }

                WaitAtSafePlace(state, now);
                continue;
            }

            ResetWaitingState(state);
        }

        if (crateRouteLinesEnabled)
        {
            EnableCrateActionLines();
            RefreshCrateActionLines(usableUnits);
        }
        else
        {
            DisableCrateActionLines();
        }
    }

    private void EnableCrateActionLines()
    {
        if (crateActionLinesActive)
            return;
        var cave = nint.Zero;
        var mayBePublished = false;
        byte[]? installedPatch = null;
        try
        {
            cave = Native.VirtualAllocEx(handle, 0, CrateActionLineCodeCaveSize,
                Native.MemCommit | Native.MemReserve, Native.PageExecuteReadWrite);
            if (cave == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "分配行动路线渲染区失败");

            var caveAddress = cave.ToInt64();
            var countAddress = caveAddress + 64;
            var tableAddress = countAddress + 4;
            WriteBytes(caveAddress, new byte[CrateActionLineCodeCaveSize]);
            var filterCode = CreateCrateActionLineFilter(countAddress, tableAddress);
            WriteCodeCave(cave, filterCode);

            installedPatch = CreateRelativePatch(ActionLineSelectionCheck,
                ActionLineSelectionOriginalBytes.Length, caveAddress, 0xE8);
            PublishCodePatch(ActionLineSelectionCheck, ActionLineSelectionOriginalBytes,
                installedPatch, "行动路线渲染函数", ref mayBePublished);

            WithSuspendedProcess(() =>
            {
                originalActionLinesEnabled = ReadByte(ActionLinesEnabled);
                WriteBytes(ActionLinesEnabled, [1]);
                crateActionLineCodeCave = cave;
                crateActionLineCountAddress = countAddress;
                crateActionLineTableAddress = tableAddress;
                crateActionLinesActive = true;
            });
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or OverflowException)
        {
            var restored = false;
            if (mayBePublished && installedPatch is not null)
            {
                try { restored = RestoreOwnedCodePatch(ActionLineSelectionCheck,
                    ActionLineSelectionOriginalBytes, installedPatch); }
                catch (Exception cleanupError) when (cleanupError is Win32Exception or
                                                     InvalidOperationException or
                                                     GameProcessExitedException)
                {
                }
            }
            if (mayBePublished)
            {
                if (restored)
                {
                    if (crateActionLinesActive)
                    {
                        try
                        {
                            WithSuspendedProcess(() =>
                                WriteBytes(ActionLinesEnabled, [originalActionLinesEnabled]));
                        }
                        catch (Exception cleanupError) when (cleanupError is Win32Exception or
                                                             InvalidOperationException or
                                                             GameProcessExitedException)
                        {
                        }
                    }
                    RetireCodeCave(cave);
                    crateActionLineCodeCave = 0;
                    crateActionLineCountAddress = 0;
                    crateActionLineTableAddress = 0;
                    crateActionLinesActive = false;
                }
                else
                {
                    crateActionLineCodeCave = cave;
                    crateActionLineCountAddress = cave.ToInt64() + 64;
                    crateActionLineTableAddress = crateActionLineCountAddress + 4;
                    crateActionLinesActive = true;
                }
            }
            else
                FreeUnpublishedCodeCaveBestEffort(cave);
            crateRouteLinesEnabled = false;
        }
    }

    private void RefreshCrateActionLines(IEnumerable<UnitState> usableUnits)
    {
        if (!crateActionLinesActive)
            return;
        var usableSet = usableUnits.ToHashSet();
        var pointers = units
            .Where(state => usableSet.Contains(state) && state.ActiveCrate is not null)
            .Select(state => state.Unit.Pointer)
            .Distinct()
            .Take(MaximumCrateActionLineUnits)
            .ToArray();

        WriteInt32(crateActionLineCountAddress, 0);
        if (pointers.Length == 0)
            return;
        var pointerBytes = new byte[pointers.Length * sizeof(uint)];
        Buffer.BlockCopy(pointers, 0, pointerBytes, 0, pointerBytes.Length);
        WriteBytes(crateActionLineTableAddress, pointerBytes);
        WriteInt32(crateActionLineCountAddress, pointers.Length);
        WriteInt32(ActionLineTimerStart, ReadInt32(CurrentFrame));
        WriteInt32(ActionLineTimerTimeLeft, 25);
    }

    private void DisableCrateActionLines()
    {
        if (!crateActionLinesActive)
            return;
        var cave = crateActionLineCodeCave;
        var countAddress = crateActionLineCountAddress;
        var installedPatch = cave == 0
            ? null
            : CreateRelativePatch(ActionLineSelectionCheck,
                ActionLineSelectionOriginalBytes.Length, cave.ToInt64(), 0xE8);
        var codeRestored = false;
        try
        {
            if (countAddress != 0)
                WithSuspendedProcess(() => WriteInt32(countAddress, 0));
            if (installedPatch is not null)
                codeRestored = RestoreOwnedCodePatch(ActionLineSelectionCheck,
                    ActionLineSelectionOriginalBytes, installedPatch);
            if (codeRestored)
                WithSuspendedProcess(() =>
                    WriteBytes(ActionLinesEnabled, [originalActionLinesEnabled]));
        }
        finally
        {
            RetireCodeCave(ref crateActionLineCodeCave);
            crateActionLinesActive = false;
            crateActionLineCountAddress = 0;
            crateActionLineTableAddress = 0;
        }
    }

    private static byte[] CreateCrateActionLineFilter(long countAddress, long tableAddress)
    {
        var code = new List<byte>(48);
        code.AddRange(Convert.FromHexString("8A868300000084C0752351528B0D"));
        code.AddRange(BitConverter.GetBytes(checked((uint)countAddress)));
        code.Add(0xBA);
        code.AddRange(BitConverter.GetBytes(checked((uint)tableAddress)));
        code.AddRange(Convert.FromHexString(
            "85C9740A3932740A83C20449EBF230C0EB02B0015A5984C0C3"));
        return [.. code];
    }

    private List<CapturedUnit> CaptureSelectedUnits()
    {
        var items = ReadUInt32(CurrentObjects + 4);
        var count = ReadInt32(CurrentObjects + 16);
        var result = new List<CapturedUnit>();
        if (items == 0 || count is < 1 or > 100)
            return result;

        var currentPlayer = ReadUInt32(CurrentPlayer);
        var seen = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var pointer = ReadUInt32(items + index * 4L);
            if (pointer == 0 || !seen.Add(pointer) || !VectorContains(FootArray, pointer))
                continue;
            var id = ReadInt32(pointer + 0x10);
            if (id > 0 && ReadUInt32(pointer + TechnoOwnerOffset) == currentPlayer)
                result.Add(new CapturedUnit(pointer, id));
        }
        return result;
    }

    private bool IsCapturedUnitValid(CapturedUnit captured)
    {
        try
        {
            return ReadInt32(captured.Pointer + 0x10) == captured.Id &&
                   VectorContains(FootArray, captured.Pointer) &&
                   ReadUInt32(captured.Pointer + TechnoOwnerOffset) == ReadUInt32(CurrentPlayer) &&
                   ReadByte(captured.Pointer + ObjectIsOnMapOffset) != 0 &&
                   ReadByte(captured.Pointer + ObjectInLimboOffset) == 0 &&
                   ReadByte(captured.Pointer + ObjectIsAliveOffset) != 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private bool IsMultiplayerSession()
    {
        var gameMode = ReadInt32(Session);
        return IsMultiplayerGameMode(gameMode);
    }

    internal static bool IsMultiplayerGameMode(int gameMode) =>
        gameMode is 3 or 4; // GameMode::LAN / GameMode::Internet

    private void WaitAtSafePlace(UnitState state, DateTime now)
    {
        var currentCell = ReadUnitCell(state.Unit.Pointer);
        if (multiplayerSession)
        {
            // Keep the synchronized idle transition to one event. A new crate resets
            // this state before QueueMove is issued, so collection resumes normally.
            WaitForCrateInMultiplayer(state, now, currentCell);
            return;
        }

        if (!state.WaitingForCrate)
        {
            state.WaitingForCrate = true;
            state.ActiveCrate = null;
            state.SafeCellAttempt = 0;
            QueueBaseReturn(state, now, currentCell);
            return;
        }

        if (currentCell != state.LastSafeObservedCell)
        {
            state.LastSafeObservedCell = currentCell;
            state.LastSafeProgressAt = now;
        }

        if (state.AtSafePlace)
        {
            if (now - state.LastCommandAt < TimeSpan.FromSeconds(5))
                return;
            var refreshedTarget = ReadSafeCell(state.Unit, state.SafeCellAttempt);
            if (refreshedTarget != state.SafeCell)
            {
                state.AtSafePlace = false;
                QueueBaseReturn(state, now, currentCell);
                return;
            }
            if (state.SafeCell is { } parkedAt &&
                DistanceSquared(currentCell, parkedAt) <= 1)
            {
                state.LastCommandAt = now;
                return;
            }
            state.AtSafePlace = false;
            QueueBaseReturn(state, now, currentCell);
            return;
        }

        if (now - state.LastCommandAt < TimeSpan.FromSeconds(5))
            return;

        if (state.SafeCell is not { } destination)
        {
            state.SafeCellAttempt++;
            QueueBaseReturn(state, now, currentCell);
            return;
        }

        if (DistanceSquared(currentCell, destination) <= 1)
        {
            QueueGuard(state.Unit);
            state.AtSafePlace = true;
            state.LastCommandAt = now;
            return;
        }

        if (now - state.LastSafeProgressAt >= TimeSpan.FromSeconds(10))
        {
            state.SafeCellAttempt++;
            QueueBaseReturn(state, now, currentCell);
            return;
        }

        QueueMove(state.Unit, destination.X, destination.Y);
        state.LastCommandAt = now;
    }

    private void QueueBaseReturn(
        UnitState state, DateTime now, (int X, int Y) currentCell)
    {
        state.SafeCell = ReadSafeCell(state.Unit, state.SafeCellAttempt);
        state.AtSafePlace = false;
        state.LastSafeObservedCell = currentCell;
        state.LastSafeProgressAt = now;

        if (state.SafeCell is { } target)
        {
            if (DistanceSquared(currentCell, target) > 1)
            {
                QueueMove(state.Unit, target.X, target.Y);
            }
            else
            {
                QueueGuard(state.Unit);
                state.AtSafePlace = true;
            }
        }
        else
        {
            QueueGuard(state.Unit);
        }
        state.LastCommandAt = now;
    }

    private void WaitForCrateInMultiplayer(
        UnitState state, DateTime now, (int X, int Y) currentCell)
    {
        if (!ShouldQueueMultiplayerCrateWait(state.WaitingForCrate))
            return;

        state.WaitingForCrate = true;
        state.ActiveCrate = null;
        state.SafeCell = null;
        state.SafeCellAttempt = 0;
        state.AtSafePlace = false;
        state.LastSafeObservedCell = currentCell;
        state.LastSafeProgressAt = now;
        QueueGuard(state.Unit);
        state.LastCommandAt = now;
    }

    internal static bool ShouldQueueMultiplayerCrateWait(bool waitingForCrate) =>
        !waitingForCrate;

}
