using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed class ShortcutService(nint hwnd, WindowMessages messages, Func<AppSettings> settings, Action panel, Action command, Action radial,Action plainPaste,Action windows) : IDisposable
{
    private readonly HashSet<int> registered=[];
    public event Action<string>? Error;
    public void Update(bool running)
    {
        foreach(int id in registered) Native.UnregisterHotKey(hwnd,id);
        registered.Clear(); messages.Message-=Receive;
        if(!running) return;
        messages.Message+=Receive;
        var s=settings();
        Register(71,"quickLauncher",s.PanelShortcut);
        Register(72,"commandBar",s.CommandShortcut);
        Register(73,"radialMenu",s.RadialShortcut);
        Register(74,"pastePlain",s.PlainPasteShortcut);
        Register(75,"windowSearch",s.WindowSearchShortcut);
    }
    private void Register(int id,string feature,string text)
    {
        if(!settings().EnabledFeatures.Contains(feature)) return;
        if(Shortcut.TryParse(text,out var shortcut) && Native.RegisterHotKey(hwnd,id,shortcut.Modifiers|0x4000,shortcut.Key)) registered.Add(id);
        else Error?.Invoke($"{text} is unavailable. Choose another shortcut in Settings.");
    }
    private void Receive(uint msg,nuint wp,nint lp)
    {
        if(msg!=Native.WM_HOTKEY) return;
        if(wp==71) panel(); if(wp==72) command(); if(wp==73) radial();
        if(wp==74) plainPaste(); if(wp==75) windows();
    }
    public void Dispose()=>Update(false);
}
