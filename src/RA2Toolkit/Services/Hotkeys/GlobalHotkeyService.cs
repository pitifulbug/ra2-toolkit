using System.ComponentModel;
using System.Runtime.InteropServices;

internal sealed class GlobalHotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly HookProcedure procedure;
    private readonly HashSet<int> pressedKeys = [];
    private nint hook;

    internal GlobalHotkeyService()
    {
        procedure = HookCallback;
        hook = SetWindowsHookEx(WhKeyboardLl, procedure, GetModuleHandle(null), 0);
        if (hook == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装全局快捷键监听。");
    }

    internal event Action<HotkeyGesture>? Pressed;

    private nint HookCallback(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var messageId = message.ToInt32();
            var keyData = Marshal.PtrToStructure<LowLevelKeyboardInput>(data);
            var key = checked((int)keyData.VirtualKey);
            if (messageId is WmKeyUp or WmSysKeyUp)
            {
                pressedKeys.Remove(key);
            }
            else if (messageId is WmKeyDown or WmSysKeyDown && pressedKeys.Add(key) &&
                     !IsModifierKey(key))
            {
                try
                {
                    Pressed?.Invoke(new HotkeyGesture(GetCurrentModifiers(), key));
                }
                catch
                {
                    // A low-level keyboard callback must always continue the hook chain.
                }
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }

    private static bool IsModifierKey(int key) => key is
        0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or
        0x5B or 0x5C;

    private static uint GetCurrentModifiers()
    {
        var modifiers = 0u;
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0)
            modifiers |= HotkeyGesture.Control;
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0)
            modifiers |= HotkeyGesture.Shift;
        if ((GetAsyncKeyState(0x12) & 0x8000) != 0)
            modifiers |= HotkeyGesture.Alt;
        return modifiers;
    }

    public void Dispose()
    {
        if (hook == 0)
            return;
        _ = UnhookWindowsHookEx(hook);
        hook = 0;
        GC.SuppressFinalize(this);
    }

    private delegate nint HookProcedure(int code, nint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct LowLevelKeyboardInput
    {
        internal readonly uint VirtualKey;
        internal readonly uint ScanCode;
        internal readonly uint Flags;
        internal readonly uint Time;
        internal readonly nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookId, HookProcedure callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
