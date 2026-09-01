using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

public sealed class CratePickerCommandSafetyTests
{
    [Fact]
    public void EnqueueCommand_rejects_a_disposed_session()
    {
        var picker = (CratePicker)RuntimeHelpers.GetUninitializedObject(typeof(CratePicker));
        typeof(CratePicker)
            .GetField("disposeState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(picker, 1);

        Assert.Throws<ObjectDisposedException>(() =>
            picker.EnqueueCommand(OverlayCommandRequest.For(OverlayCommand.ToggleCrateRouteLines)));
    }

    [Fact]
    public void Multiplayer_cleanup_disables_fast_turn_and_resets_its_schedule()
    {
        var method = ReadMethod(
            "private void EnforceMultiplayerSafety()",
            "private void RequestExit()");

        Assert.Contains("fastTurnEnabled = false;", method, StringComparison.Ordinal);
        Assert.Contains("nextFastTurnAt = DateTime.MinValue;", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_command_failures_are_reported_in_multiplayer()
    {
        var method = ReadMethod(
            "private void ProcessOverlayCommands()",
            "private bool? GetToggleState");

        Assert.Contains(
            "ShowOperationStatus($\"操作未能执行：{error.Message}\", true);",
            method,
            StringComparison.Ordinal);
        Assert.DoesNotContain("if (!multiplayerSession)", method, StringComparison.Ordinal);
    }

    private static string ReadMethod(
        string signature,
        string nextSignature,
        [CallerFilePath] string testFile = "")
    {
        var root = new DirectoryInfo(Path.GetDirectoryName(testFile)!);
        while (!File.Exists(Path.Combine(root.FullName, "ra2-toolkit.slnx")))
            root = root.Parent ?? throw new DirectoryNotFoundException("Repository root not found.");

        var source = File.ReadAllText(Path.Combine(root.FullName,
            "src", "RA2Toolkit", "Game", "Features", "CratePicker.Overlay.cs"));
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not locate method {signature}.");
        var end = source.IndexOf(nextSignature, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not locate method following {signature}.");
        return source[start..end];
    }
}
