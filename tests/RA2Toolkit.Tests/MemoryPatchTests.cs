using Xunit;

public sealed class MemoryPatchTests
{
    [Fact]
    public void Create_relative_patch_emits_jump_and_nop_padding()
    {
        var patch = CratePicker.CreateRelativePatch(0x1000, 7, 0x1100);

        Assert.Equal(new byte[] { 0xE9, 0xFB, 0x00, 0x00, 0x00, 0x90, 0x90 }, patch);
    }

    [Fact]
    public void Create_relative_patch_rejects_too_short_patch()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CratePicker.CreateRelativePatch(0x1000, 4, 0x1100));
    }

    [Fact]
    public void Create_relative_patch_rejects_unrepresentable_displacement()
    {
        Assert.Throws<OverflowException>(
            () => CratePicker.CreateRelativePatch(0, 5, (long)int.MaxValue + 6));
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 }, true)]
    [InlineData(new byte[] { 4, 5, 6 }, true)]
    [InlineData(new byte[] { 1, 5, 3 }, true)]
    [InlineData(new byte[] { 1, 9, 3 }, false)]
    [InlineData(new byte[] { 1, 2 }, false)]
    public void Rollback_compatibility_accepts_only_original_or_installed_bytes(
        byte[] actual,
        bool expected)
    {
        var original = new byte[] { 1, 2, 3 };
        var installed = new byte[] { 4, 5, 6 };

        Assert.Equal(expected,
            CratePicker.IsRollbackCompatible(actual, original, installed));
    }

    [Fact]
    public void Restore_failure_keeps_infinite_range_patch_owned()
    {
        var installed = true;

        var restored = CratePicker.UpdateInstalledAfterRestore(
            ref installed, () => false);

        Assert.False(restored);
        Assert.True(installed);
        Assert.Throws<InvalidOperationException>(() =>
            CratePicker.UpdateInstalledAfterRestore(
                ref installed, () => throw new InvalidOperationException("restore failed")));
        Assert.True(installed);

        restored = CratePicker.UpdateInstalledAfterRestore(ref installed, () => true);

        Assert.True(restored);
        Assert.False(installed);
    }

    [Fact]
    public void Release_keeps_published_infinite_range_cave()
    {
        var cave = (nint)0x1234;
        var retired = new List<nint>();

        var released = CratePicker.RetireCaveIfUninstalled(
            true, ref cave, retired.Add);

        Assert.False(released);
        Assert.Equal((nint)0x1234, cave);
        Assert.Empty(retired);

        released = CratePicker.RetireCaveIfUninstalled(
            false, ref cave, retired.Add);

        Assert.True(released);
        Assert.Equal(0, cave);
        Assert.Equal([(nint)0x1234], retired);
    }

    [Fact]
    public void Cleanup_retry_is_bounded_when_restore_never_succeeds()
    {
        var attempts = 0;

        var completedAttempts = CratePicker.RunCleanupWithRetry(
            () => true, () => attempts++, 3);

        Assert.Equal(3, completedAttempts);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void Cleanup_retry_stops_after_ownership_is_released()
    {
        var pending = true;
        var attempts = 0;

        var completedAttempts = CratePicker.RunCleanupWithRetry(
            () => pending,
            () =>
            {
                attempts++;
                if (attempts == 2)
                    pending = false;
            },
            3);

        Assert.Equal(2, completedAttempts);
        Assert.Equal(2, attempts);
    }
}
