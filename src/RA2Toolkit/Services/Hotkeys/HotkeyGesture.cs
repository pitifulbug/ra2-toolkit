using System.Windows.Input;

internal readonly record struct HotkeyGesture(uint Modifiers, int Key)
{
    internal const uint Alt = 0x0001;
    internal const uint Control = 0x0002;
    internal const uint Shift = 0x0004;
    internal const uint SupportedModifiers = Alt | Control | Shift;

    internal bool IsValid => Key > 0;

    public string DisplayText
    {
        get
        {
            var key = KeyInterop.KeyFromVirtualKey(Key);
            var keyName = key == System.Windows.Input.Key.None
                ? $"VK_{Key:X2}"
                : key.ToString();
            return string.Join("+", new[]
            {
                (Modifiers & Control) != 0 ? "Ctrl" : null,
                (Modifiers & Shift) != 0 ? "Shift" : null,
                (Modifiers & Alt) != 0 ? "Alt" : null,
                keyName
            }.Where(part => part is not null));
        }
    }
}
