using System.Runtime.InteropServices;
using System.Text;
using SinhalaFontBridge.Core;

namespace SinhalaFontBridge;

// A window-scoped typing session. It keeps only the current word in memory;
// typed text is never saved or sent to a network service.
internal sealed class LiveTypingService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100, WmKeyUp = 0x0101, WmSysKeyDown = 0x0104, WmSysKeyUp = 0x0105;
    private const uint Injected = 0x10, InputKeyboard = 1, KeyUp = 0x0002, UnicodeKey = 0x0004;
    private readonly TypingComposer _composer;
    private readonly Action<string> _onStop;
    private readonly HookProc _callback;
    private readonly HashSet<uint> _suppressed = [];
    private IntPtr _hook;
    private IntPtr _window;
    private LegacyProfile? _profile;
    private TypingStyle _style;
    private string _keys = "";
    private string _rendered = "";

    public bool Active => _hook != IntPtr.Zero;

    public LiveTypingService(TypingComposer composer, Action<string> onStop)
    {
        _composer = composer;
        _onStop = onStop;
        _callback = HandleKey;
    }

    public static IntPtr ForegroundWindow() => GetForegroundWindow();

    public bool Start(IntPtr window, LegacyProfile profile, TypingStyle style, out string error)
    {
        Stop();
        if (window == IntPtr.Zero || !profile.CanEncode)
        {
            error = "Choose an external text window and an encoding that supports typing.";
            return false;
        }
        GetWindowThreadProcessId(window, out uint processId);
        if (processId == (uint)Environment.ProcessId)
        {
            error = "Focus a text field in the target app, then press Ctrl+Alt+Shift+T.";
            return false;
        }
        _window = window;
        _profile = profile;
        _style = style;
        _keys = _rendered = "";
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(null), 0);
        error = _hook == IntPtr.Zero ? "Windows could not start the typing hook." : "";
        return _hook != IntPtr.Zero;
    }

    public void Stop(string? reason = null)
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _window = IntPtr.Zero;
        _profile = null;
        _keys = _rendered = "";
        _suppressed.Clear();
        if (reason is not null) _onStop(reason);
    }

    public void Dispose() => Stop();

    private IntPtr HandleKey(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || !Active) return CallNextHookEx(_hook, code, message, data);
        var key = Marshal.PtrToStructure<KeyboardData>(data);
        if ((key.Flags & Injected) != 0) return CallNextHookEx(_hook, code, message, data);
        int kind = message.ToInt32();
        if (kind is WmKeyUp or WmSysKeyUp)
            return _suppressed.Remove(key.VirtualKey) ? new IntPtr(1) : CallNextHookEx(_hook, code, message, data);
        if (kind is not (WmKeyDown or WmSysKeyDown)) return CallNextHookEx(_hook, code, message, data);
        if (GetForegroundWindow() != _window)
        {
            Stop("Typing stopped because the active window changed.");
            return CallNextHookEx(IntPtr.Zero, code, message, data);
        }
        uint vk = key.VirtualKey;
        if (vk == 0x1B) // Escape
        {
            Stop("Typing stopped.");
            return CallNextHookEx(IntPtr.Zero, code, message, data);
        }
        if (IsDown(0x11) || IsDown(0x12) || IsDown(0x5B) || IsDown(0x5C))
        {
            ClearWord();
            return CallNextHookEx(_hook, code, message, data);
        }
        if (vk == 0x08) // Backspace edits the in-progress word.
        {
            if (_keys.Length == 0) return CallNextHookEx(_hook, code, message, data);
            if (!ReplaceWord(_keys[..^1])) return CallNextHookEx(_hook, code, message, data);
            _suppressed.Add(vk);
            return new IntPtr(1);
        }
        if (vk is 0x20 or 0x09 or 0x0D or 0x25 or 0x26 or 0x27 or 0x28 or 0x24 or 0x23 or 0x2E)
        {
            ClearWord();
            return CallNextHookEx(_hook, code, message, data);
        }
        char? character = Printable(vk);
        if (character is null)
        {
            ClearWord();
            return CallNextHookEx(_hook, code, message, data);
        }
        if (_style == TypingStyle.Singlish && !char.IsLetter(character.Value))
        {
            ClearWord();
            return CallNextHookEx(_hook, code, message, data);
        }
        if (_keys.Length >= 48)
        {
            Stop("Typing stopped at the 48-key word limit. Press the shortcut to restart.");
            return CallNextHookEx(IntPtr.Zero, code, message, data);
        }
        if (!ReplaceWord(_keys + character.Value))
        {
            Stop("This key sequence cannot be encoded by the selected font. Typing stopped.");
            return CallNextHookEx(IntPtr.Zero, code, message, data);
        }
        _suppressed.Add(vk);
        return new IntPtr(1);
    }

    private void ClearWord() => _keys = _rendered = "";

    private bool ReplaceWord(string keys)
    {
        if (_profile is null) return false;
        string unicode = _composer.Compose(keys, _style);
        var encoded = _profile.Encode(unicode);
        if (encoded.Unmapped > 0 || _profile.Decode(encoded.Text) != unicode) return false;
        int common = 0;
        while (common < _rendered.Length && common < encoded.Text.Length && _rendered[common] == encoded.Text[common]) common++;
        var events = new List<Input>();
        for (int i = common; i < _rendered.Length; i++) AddKey(events, 0x08, '\0');
        foreach (char c in encoded.Text.AsSpan(common)) AddKey(events, 0, c);
        if (events.Count > 0 && SendInput((uint)events.Count, events.ToArray(), Marshal.SizeOf<Input>()) != events.Count)
            return false;
        _keys = keys;
        _rendered = encoded.Text;
        return true;
    }

    private static void AddKey(List<Input> events, ushort vk, char character)
    {
        uint flags = character == 0 ? 0 : UnicodeKey;
        events.Add(new Input { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeybdInput { VirtualKey = vk, Scan = character, Flags = flags } } });
        events.Add(new Input { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeybdInput { VirtualKey = vk, Scan = character, Flags = flags | KeyUp } } });
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static char? Printable(uint vk)
    {
        bool shifted = IsDown(0x10);
        bool upper = shifted ^ ((GetKeyState(0x14) & 1) != 0);
        if (vk is >= 0x41 and <= 0x5A) return (char)(upper ? vk : vk + 32);
        if (vk is >= 0x30 and <= 0x39)
            return shifted ? ")!@#$%^&*("[(int)(vk - 0x30)] : (char)vk;
        return vk switch
        {
            0xBA => shifted ? ':' : ';', 0xBB => shifted ? '+' : '=',
            0xBC => shifted ? '<' : ',', 0xBD => shifted ? '_' : '-',
            0xBE => shifted ? '>' : '.', 0xBF => shifted ? '?' : '/',
            0xC0 => shifted ? '~' : '`', 0xDB => shifted ? '{' : '[',
            0xDC => shifted ? '|' : '\\', 0xDD => shifted ? '}' : ']',
            0xDE => shifted ? '"' : '\'', _ => null
        };
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData
    {
        public uint VirtualKey, ScanCode, Flags, Time;
        public UIntPtr ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeybdInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeybdInput
    {
        public ushort VirtualKey, Scan;
        public uint Flags, Time;
        public UIntPtr ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    {
        public int X, Y;
        public uint MouseData, Flags, Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? module);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] events, int size);
}
