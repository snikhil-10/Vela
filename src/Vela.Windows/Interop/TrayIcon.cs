using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Vela.Windows.Interop;

internal sealed class TrayIcon : IDisposable
{
    private const uint Callback = 0x8001;
    private readonly uint taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
    private readonly WindowMessages messages;
    private readonly nint hwnd;
    private nint icon;
    private Color iconColor;
    private int iconSize;
    private bool disposed;
    private readonly UISettings colors=new();
    private readonly DispatcherQueue queue;
    private NotifyData data;
    public event Action? Click;
    public event Action? Settings;
    public event Action? Quit;
    public event Action? ThemeChanged;

    public TrayIcon(nint hwnd, WindowMessages messages)
    {
        this.hwnd=hwnd; this.messages=messages;
        queue=DispatcherQueue.GetForCurrentThread();
        int size = Math.Max(16, (int)(16 * Native.GetDpiForWindow(hwnd) / 96));
        iconColor=ThemedIcon.Foreground(ThemedIcon.ShellIsLight); iconSize=size;
        icon=ThemedIcon.Create(size,iconColor);
        data = new NotifyData { Size=(uint)Marshal.SizeOf<NotifyData>(), Window=hwnd, Id=1, Flags=2 | 4 | 1 | 0x20 | 0x80,
            Message=Callback, Icon=icon, Tip="Vela · your Windows extras", Guid=new("7ca3a540-5a0e-4486-86ef-c455e3ba6ff7"), Info="", InfoTitle="" };
        messages.Message += Receive;
        Add();
        colors.ColorValuesChanged+=ColorsChanged;
    }
    private void ColorsChanged(UISettings sender,object args)=>QueueAppearance();
    private void QueueAppearance()=>queue.TryEnqueue(()=>
    {
        if(disposed) return;
        try { UpdateAppearance(); }
        catch(Win32Exception) { } // Keep the current valid icon if Windows cannot allocate its replacement.
    });
    private void UpdateAppearance()
    {
        Color color=ThemedIcon.Foreground(ThemedIcon.ShellIsLight);
        int size=Math.Max(16,(int)(16*Native.GetDpiForWindow(hwnd)/96));
        if(!color.Equals(iconColor) || size!=iconSize)
        {
            nint replacement=ThemedIcon.Create(size,color);
            var changed=data; changed.Icon=replacement;
            if(Shell_NotifyIcon(1,ref changed))
            { Native.DestroyIcon(icon); icon=replacement; data=changed; iconColor=color; iconSize=size; }
            else Native.DestroyIcon(replacement);
        }
        ThemeChanged?.Invoke();
    }
    private void Add()
    {
        if (!Shell_NotifyIcon(0, ref data)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not add the system tray icon.");
        data.Version = 4; Shell_NotifyIcon(4, ref data);
    }
    private void Receive(uint msg, nuint wp, nint lp)
    {
        if (msg == taskbarCreated) { Add(); QueueAppearance(); return; }
        if(msg is 0x001A or 0x0015 or 0x031A or 0x02E0) { QueueAppearance(); return; }
        if (msg != Callback) return;
        uint action = (uint)(lp.ToInt64() & 0xFFFF);
        if (action is 0x400 or 0x401) Click?.Invoke(); // NIN_SELECT / NIN_KEYSELECT (keyboard accessible).
        if (action == 0x7B) ShowContextMenu();
    }
    private void ShowContextMenu()
    {
        Native.GetCursorPos(out var point);
        nint menu=Native.CreatePopupMenu();
        try
        {
            Native.AppendMenu(menu, 0, 1, "Open Vela"); Native.AppendMenu(menu, 0, 2, "Settings");
            Native.AppendMenu(menu, 0x800, 0, null); Native.AppendMenu(menu, 0, 3, "Exit Vela");
            Native.SetForegroundWindow(hwnd);
            uint choice=Native.TrackPopupMenuEx(menu, 0x100 | 0x2, point.X, point.Y, hwnd, 0);
            Native.PostMessage(hwnd, 0, 0, 0);
            if(choice==1) Click?.Invoke(); if(choice==2) Settings?.Invoke(); if(choice==3) Quit?.Invoke();
        }
        finally { Native.DestroyMenu(menu); }
    }
    public Native.Rect? Bounds
    {
        get
        {
            var id = new NotifyIdentifier { Size=(uint)Marshal.SizeOf<NotifyIdentifier>(), Window=hwnd, Id=1, Guid=data.Guid };
            return Shell_NotifyIconGetRect(ref id, out var rect) == 0 ? rect : null;
        }
    }
    public void Notify(string title, string message)
    {
        var notification=data; notification.Flags=0x10 | 0x20; notification.InfoTitle=title;
        notification.Info=message.Length > 255 ? message[..255] : message; notification.InfoFlags=1 | 0x80;
        Shell_NotifyIcon(1, ref notification);
    }
    public void Dispose()
    {
        if(disposed) return;
        disposed=true; colors.ColorValuesChanged-=ColorsChanged;
        messages.Message-=Receive; Shell_NotifyIcon(2, ref data); Native.DestroyIcon(icon);
    }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct NotifyData
    {
        public uint Size; public nint Window; public uint Id, Flags, Message; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NotifyIdentifier
    { public uint Size; public nint Window; public uint Id; public Guid Guid; }
    [DllImport("shell32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool Shell_NotifyIcon(uint message, ref NotifyData data);
    [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref NotifyIdentifier id, out Native.Rect rect);
}
