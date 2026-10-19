using System.ComponentModel;

namespace Vela.Windows.Interop;

internal sealed class WindowMessages : IDisposable
{
    private readonly nint hwnd;
    private readonly Native.SubclassProc procedure;
    private readonly bool sessionNotifications;
    public event Action<uint, nuint, nint>? Message;
    public event Action<string>? Error;
    public WindowMessages(nint hwnd)
    {
        this.hwnd = hwnd; procedure = Dispatch;
        if (!Native.SetWindowSubclass(hwnd, procedure, 1, 0)) throw new Win32Exception();
        sessionNotifications=Native.WTSRegisterSessionNotification(hwnd,0);
    }
    private nint Dispatch(nint window, uint message, nuint wp, nint lp, nuint id, nuint data)
    {
        try { Message?.Invoke(message, wp, lp); }
        catch(Exception ex) { Error?.Invoke(ex.Message); }
        return Native.DefSubclassProc(window, message, wp, lp);
    }
    public void Dispose()
    {
        if(sessionNotifications) Native.WTSUnRegisterSessionNotification(hwnd);
        Native.RemoveWindowSubclass(hwnd,procedure,1);
    }
}
