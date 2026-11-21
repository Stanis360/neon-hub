using System.Runtime.InteropServices;

namespace NeonMonitor.Services.Deck;

/// <summary>
/// Simuliert Tastendrücke über die Windows-Funktion SendInput – sie landen beim Fenster im Vordergrund
/// (bzw. bei Programmen mit globalen Tastenkürzeln wie Discord).
/// Tastenkürzel werden als Scancodes geschickt: Das verstehen normale Programme <b>und</b> die meisten Spiele.
/// </summary>
public static class InputSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x2, Unicode = 0x4, ScanCode = 0x8, ExtendedKey = 0x1;
    private const uint MapVkToVscEx = 4;

    /// <summary>Kombination drücken und loslassen: Zusatztasten runter, Taste runter/hoch, Zusatztasten hoch.</summary>
    public static void SendHotkey(IReadOnlyList<ushort> modifiers, ushort key)
    {
        var inputs = new List<Input>();
        foreach (var m in modifiers) inputs.Add(Key(m, up: false));
        inputs.Add(Key(key, up: false));
        inputs.Add(Key(key, up: true));
        foreach (var m in modifiers.Reverse()) inputs.Add(Key(m, up: true));
        Send(inputs);
    }

    /// <summary>Einzelne virtuelle Taste (für Medien-/Lautstärketasten – die gehen über den virtuellen Code).</summary>
    public static void SendVirtualKey(ushort vk) =>
        Send([new Input { Type = InputKeyboard, U = new InputUnion { Ki = new KeybdInput { Vk = vk } } },
              new Input { Type = InputKeyboard, U = new InputUnion { Ki = new KeybdInput { Vk = vk, Flags = KeyUp } } }]);

    /// <summary>Text eintippen (Unicode, unabhängig vom Tastaturlayout).</summary>
    public static void SendText(string text)
    {
        var inputs = new List<Input>();
        foreach (var ch in text.Replace("\r\n", "\n"))
        {
            if (ch == '\n')
            {
                inputs.Add(Key(0x0D, false));
                inputs.Add(Key(0x0D, true));
                continue;
            }
            inputs.Add(new Input { Type = InputKeyboard, U = new InputUnion { Ki = new KeybdInput { Scan = ch, Flags = Unicode } } });
            inputs.Add(new Input { Type = InputKeyboard, U = new InputUnion { Ki = new KeybdInput { Scan = ch, Flags = Unicode | KeyUp } } });
        }
        Send(inputs);
    }

    private static Input Key(ushort vk, bool up)
    {
        var scan = MapVirtualKey(vk, MapVkToVscEx);
        var flags = up ? KeyUp : 0;
        if (scan != 0)
        {
            flags |= ScanCode;
            if ((scan & 0xFF00) is 0xE000 or 0xE100) flags |= ExtendedKey; // z. B. Pfeiltasten, Win, rechte Strg
        }
        return new Input
        {
            Type = InputKeyboard,
            U = new InputUnion { Ki = new KeybdInput { Vk = scan != 0 ? (ushort)0 : vk, Scan = (ushort)(scan & 0xFF), Flags = flags } },
        };
    }

    private static void Send(IReadOnlyCollection<Input> inputs)
    {
        if (inputs.Count == 0) return;
        var arr = inputs.ToArray();
        SendInput((uint)arr.Length, arr, Marshal.SizeOf<Input>());
    }

    // ---------------- P/Invoke ----------------

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion U;
    }

    /// <summary>Union wie in winuser.h – die Maus-Struktur ist die größte und bestimmt die Größe.</summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mi;
        [FieldOffset(0)] public KeybdInput Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx, Dy;
        public uint MouseData, Flags, Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);
}
