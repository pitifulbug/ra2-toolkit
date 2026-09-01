using Xunit;

public sealed class HotkeyStoreTests
{
    [Fact]
    public void Legacy_bindings_migrate_to_current_commands()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ra2-toolkit-hotkeys-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path,
                """
                {
                  "ToggleCratePicker": {
                    "Modifiers": 2,
                    "Key": 75
                  },
                  "PromoteSelectedUnits": {
                    "Modifiers": 1,
                    "Key": 76
                  }
                }
                """);

            var store = new HotkeyStore(path);

            Assert.True(store.TryGet(
                OverlayCommand.ToggleSelectedCratePickers, out var gesture));
            Assert.Equal(2u, gesture.Modifiers);
            Assert.Equal(75, gesture.Key);
            Assert.True(store.TryGet(
                OverlayCommand.ToggleEliteUnits, out var eliteGesture));
            Assert.Equal(1u, eliteGesture.Modifiers);
            Assert.Equal(76, eliteGesture.Key);
            Assert.Equal(2, store.Bindings.Count);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
