using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PracticeTree;

// Foreground-only raw keyboard input preserves each Shift key's make/break events.
internal sealed class RawShiftInput(Action<int, bool> onShift) : IDisposable
{
    private HwndSource? source;
    private Key firstKey = Key.LeftShift, secondKey = Key.RightShift;
    internal void ConfigureKeys(Key first, Key second) { firstKey = first; secondKey = second; }
    internal bool IsAttached { get; private set; }

    internal void Attach(HwndSource windowSource)
    {
        var devices = new[] { new Device { UsagePage = 1, Usage = 6, Target = windowSource.Handle } };
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<Device>())) return;
        source = windowSource;
        source.AddHook(WindowMessage);
        IsAttached = true;
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmInput = 0x00FF;
        const uint RidInput = 0x10000003;
        if (message != WmInput) return IntPtr.Zero;
        var headerSize = (uint)Marshal.SizeOf<Header>();
        uint size = 0;
        if (GetRawInputData(lParam, RidInput, IntPtr.Zero, ref size, headerSize) == uint.MaxValue || size < headerSize + 16 || size > 4096) return IntPtr.Zero;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RidInput, buffer, ref size, headerSize) == uint.MaxValue) return IntPtr.Zero;
            if (Marshal.ReadInt32(buffer) != 1) return IntPtr.Zero; // RIM_TYPEKEYBOARD
            var keyboard = IntPtr.Add(buffer, (int)headerSize);
            ProcessKeyboard((ushort)Marshal.ReadInt16(keyboard), (ushort)Marshal.ReadInt16(keyboard, 2));
        }
        finally { Marshal.FreeHGlobal(buffer); }
        // Leave WM_INPUT unhandled so DefWindowProc performs foreground cleanup.
        return IntPtr.Zero;
    }

    internal void ProcessKeyboard(ushort scanCode, ushort flags)
    {
        if ((flags & 4) != 0 || scanCode == 0 || scanCode == 0xFF) return;
        var extended = (flags & 2) != 0;
        var virtualKey = !extended && scanCode == 0x2A ? 0xA0u
            : !extended && scanCode == 0x36 ? 0xA1u
            : MapVirtualKey((uint)scanCode | (extended ? 0xE000u : 0u), 3);
        if (virtualKey == 0) return;
        var key = KeyInterop.KeyFromVirtualKey((int)virtualKey);
        var player = key == firstKey ? 0 : key == secondKey ? 1 : -1;
        if (player >= 0) onShift(player, (flags & 1) != 0);
    }

    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    public void Dispose()
    {
        if (!IsAttached) return;
        source?.RemoveHook(WindowMessage);
        RegisterRawInputDevices([new Device { UsagePage = 1, Usage = 6, Flags = 1 }], 1, (uint)Marshal.SizeOf<Device>());
        source = null;
        IsAttached = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Device
    {
        public ushort UsagePage, Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Header
    {
        public uint Type, Size;
        public IntPtr Device, WParam;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(Device[] devices, uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
}
