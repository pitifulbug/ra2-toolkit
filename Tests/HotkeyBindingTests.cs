public sealed class HotkeyBindingTests
{
    [Fact]
    public void DisplayText_UsesStableModifierOrder()
    {
        var binding = new HotkeyBinding(
            HotkeyBinding.Control | HotkeyBinding.Shift | HotkeyBinding.Alt,
            Keys.F5);

        Assert.Equal("Ctrl+Shift+Alt+F5", binding.DisplayText);
    }

    [Fact]
    public void DisplayText_OmitsUnrecognizedModifierBits()
    {
        var binding = new HotkeyBinding(0x80, Keys.F8);

        Assert.Equal("F8", binding.DisplayText);
    }

    [Fact]
    public void DisplayText_UsesKeyNameWhenNoModifiersArePressed()
    {
        var binding = new HotkeyBinding(0, Keys.Delete);

        Assert.Equal("Delete", binding.DisplayText);
    }
}
