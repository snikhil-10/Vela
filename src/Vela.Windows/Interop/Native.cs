using System.Runtime.InteropServices;
using System.Text;

namespace Vela.Windows.Interop;

internal static class Native
{
    internal const uint WM_HOTKEY = 0x0312, WM_CLIPBOARDUPDATE = 0x031D, WM_WTSSESSION_CHANGE = 0x02B1,
        WM_POWERBROADCAST = 0x0218, WM_MOUSEWHEEL = 0x020A, WM_MOUSEHWHEEL = 0x020E;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MonitorInfo
    { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct SystemPowerStatus
    { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }
    [StructLayout(LayoutKind.Sequential)] internal struct MemoryStatus
    { public uint Length, Load; public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirtual, AvailVirtual, AvailExtended; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseData
    { public Point Point; public uint MouseDataValue, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardData
    { public uint Key, ScanCode, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct GuiThreadInfo
    { public uint Size,Flags; public nint Active,Focus,Capture,MenuOwner,MoveSize,Caret; public Rect CaretRect; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
    { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput
    { public int X, Y; public uint Data, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput
    { public ushort Key, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    internal delegate nint SubclassProc(nint hwnd, uint msg, nuint wp, nint lp, nuint id, nuint data);
    internal delegate nint HookProc(int code, nuint wp, nint lp);
    internal delegate bool MonitorEnum(nint monitor, nint hdc, ref Rect rect, nint data);
    [DllImport("comctl32.dll")] internal static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] internal static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] internal static extern nint DefSubclassProc(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint processId);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd,int command);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnum callback, nint data);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern nint GetWindowLongPtrW(nint hwnd, int index);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern nint SetWindowLongPtrW(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint hwnd,uint attribute,ref uint value,uint size);
    [DllImport("gdi32.dll")] internal static extern nint CreateRoundRectRgn(int left,int top,int right,int bottom,int ellipseWidth,int ellipseHeight);
    [DllImport("user32.dll")] internal static extern int SetWindowRgn(nint hwnd,nint region,bool redraw);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint value);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string value);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] internal static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] internal static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);
    [DllImport("user32.dll")] internal static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] internal static extern bool PostMessage(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError=true)] internal static extern nint SetWindowsHookEx(int type, HookProc proc, nint module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nuint wp, nint lp);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int ToUnicode(uint key, uint scan, byte[] state, StringBuilder text, int length, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int ToUnicodeEx(uint key,uint scan,byte[] state,StringBuilder text,int length,uint flags,nint layout);
    [DllImport("user32.dll")] internal static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] internal static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll")] internal static extern bool GetGUIThreadInfo(uint thread,ref GuiThreadInfo info);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(nint hwnd,StringBuilder value,int max);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] internal static extern bool AddClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool RemoveClipboardFormatListener(nint hwnd);
    [DllImport("wtsapi32.dll")] internal static extern bool WTSRegisterSessionNotification(nint hwnd, uint flags);
    [DllImport("wtsapi32.dll")] internal static extern bool WTSUnRegisterSessionNotification(nint hwnd);
    [DllImport("kernel32.dll", SetLastError=true)] internal static extern uint SetThreadExecutionState(uint flags);
    [DllImport("kernel32.dll")] internal static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
    [DllImport("kernel32.dll")] internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")] internal static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
}
