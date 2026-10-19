using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Dispatching;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed partial class InputService(Func<AppSettings> settings, DispatcherQueue queue, Func<Func<Task>,Task> runOperation,nint clipboardOwner) : IDisposable
{
    private readonly HashSet<string> active=[];
    private Native.HookProc? keyboardCallback, mouseCallback;
    private nint keyboardHook, mouseHook, lastWindow;
    private readonly SnippetMatcher snippets = new();
    private readonly CleaningSession cleaning = new();
    private readonly SnippetKeyGuard snippetKeys = new();
    private readonly HashSet<uint> cleaningKeys = [];
    private nint lastFocus;
    private bool capsHeld, superHeld, suspended;
    private readonly Dictionary<uint,long> lastClicks=[];
    private readonly HashSet<uint> suppressedUps=[];
    private readonly HashSet<uint> mappedButtons=[];
    private readonly CloseShortcutGuard closeGuard=new();
    private readonly HashSet<uint> closeKeys=[];
    
    private DispatcherQueueTimer? cleaningTimer, scrollTimer;
    private double remainingScroll;
    private int lastCleaningSeconds;
    private Native.Point scrollPoint;
    private nint scrollTarget;
    public bool Cleaning => cleaning.Active;
    public bool CleaningMouse => cleaning.Active && cleaning.LockMouse;
    public int CleaningSeconds => cleaning.SecondsRemaining(Environment.TickCount64);
    public event Action<string>? Notice;
    public event Action? CleaningChanged;

    public IFeatureService Feature(string id) => new DelegateFeature(id, () => { active.Add(id); Reconcile(); }, () => { active.Remove(id); Reconcile(); });
    private void Reconcile()
    {
        bool keyboard=active.Any(id=>id is "superKey" or "textSnippets" or "quitWindowProtection" or "cleaningMode");
        bool mouse=CleaningMouse || active.Any(id=>id is "textSnippets" or "scrollHorizontal" or "scrollInverter" or "smoothScroll" or "mouseClickDebounce" or "mouseButtonShortcuts");
        if(keyboard && keyboardHook==0)
        {
            keyboardCallback=SafeKeyboard; keyboardHook=Native.SetWindowsHookEx(13, keyboardCallback, Native.GetModuleHandle(null), 0);
            if(keyboardHook==0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        if(!keyboard && keyboardHook!=0) { ReleaseSuper(); Native.UnhookWindowsHookEx(keyboardHook); keyboardHook=0; snippets.Clear(); }
        if(mouse && mouseHook==0)
        {
            mouseCallback=SafeMouse; mouseHook=Native.SetWindowsHookEx(14, mouseCallback, Native.GetModuleHandle(null), 0);
            if(mouseHook==0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        if(!mouse && mouseHook!=0) { Native.UnhookWindowsHookEx(mouseHook); mouseHook=0; StopScroll(); suppressedUps.Clear(); mappedButtons.Clear(); }
        if(!active.Contains("superKey")) ReleaseSuper();
        if(!active.Contains("smoothScroll")) StopScroll();
        if(!active.Contains("cleaningMode") && Cleaning) EndCleaning();
    }
    public void OptionsChanged() { snippets.Clear(); snippetGeneration++; snippetPreparation?.Cancel(); Reconcile(); }
    public void BeginCleaning()
    {
        if(!active.Contains("cleaningMode")) return;
        snippets.Clear(); snippetGeneration++; snippetPreparation?.Cancel(); ReleaseSuper(); StopScroll();
        DropPendingSnippetInput();
        cleaning.Start(Environment.TickCount64,settings().CleaningLockMouse);
        try { Reconcile(); }
        catch { cleaning.Stop(); Reconcile(); throw; }
        cleaningTimer ??= queue.CreateTimer(); cleaningTimer.Interval=TimeSpan.FromMilliseconds(100);
        lastCleaningSeconds=CleaningSeconds;
        cleaningTimer.Tick-=CleaningTick; cleaningTimer.Tick+=CleaningTick; cleaningTimer.Start(); CleaningChanged?.Invoke();
    }
    private void CleaningTick(DispatcherQueueTimer sender, object args)
    {
        if(cleaning.ShouldEnd(Environment.TickCount64)) EndCleaning();
        else if(CleaningSeconds!=lastCleaningSeconds) { lastCleaningSeconds=CleaningSeconds; CleaningChanged?.Invoke(); }
    }
    public void EndCleaning()
    {
        bool wasActive=Cleaning; cleaning.Stop(); cleaningTimer?.Stop();
        if(wasActive) { Reconcile(); CleaningChanged?.Invoke(); }
    }
    public void SuspendInput()
    { suspended=true; snippets.Clear(); snippetGeneration++; snippetPreparation?.Cancel(); EndCleaning(); ReleaseSuper(); StopScroll(); DropPendingSnippetInput(); }
    public void ResumeInput() { suspended=false; snippets.Clear(); }
    private void DropPendingSnippetInput()
    {
        AbortPendingSnippetInput();
        snippetKeys.Clear();
        SendKeys([0x10,0x11,0x12,0x5B,0x5C],true);
    }
    private bool Excluded(nint hwnd) => settings().InputExcludedApps.Any(n=>Path.GetFileNameWithoutExtension(n).Equals(ProcessName(hwnd),StringComparison.OrdinalIgnoreCase));
    private static string ProcessName(nint hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd,out uint pid);
        try { using var process=Process.GetProcessById((int)pid); return process.ProcessName; } catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or Win32Exception) { return ""; }
    }
    private nint SafeKeyboard(int code,nuint wp,nint lp)
    {
        try { return Keyboard(code,wp,lp); }
        catch { snippets.Clear(); return Native.CallNextHookEx(keyboardHook,code,wp,lp); }
    }
    private nint SafeMouse(int code,nuint wp,nint lp)
    {
        try { return Mouse(code,wp,lp); }
        catch { return Native.CallNextHookEx(mouseHook,code,wp,lp); }
    }
    private static bool StandardPasswordField()
    {
        var info=new Native.GuiThreadInfo { Size=(uint)Marshal.SizeOf<Native.GuiThreadInfo>() };
        if(!Native.GetGUIThreadInfo(0,ref info) || info.Focus==0) return false;
        var name=new StringBuilder(128); Native.GetClassName(info.Focus,name,name.Capacity);
        return name.ToString().Contains("Edit",StringComparison.OrdinalIgnoreCase) && ((long)Native.GetWindowLongPtrW(info.Focus,-16)&0x20)!=0;
    }
    private static bool OwnWindow(nint window)
    {
        Native.GetWindowThreadProcessId(window,out uint pid);
        return pid==(uint)Environment.ProcessId;
    }
    private nint Keyboard(int code, nuint wp, nint lp)
    {
        if(code<0) return Native.CallNextHookEx(keyboardHook,code,wp,lp);
        var data=Marshal.PtrToStructure<Native.KeyboardData>(lp);
        bool down=wp is 0x100 or 0x104;
        if(Cleaning)
        {
            if(data.Key==0x1B) cleaning.Escape(down,Environment.TickCount64);
            if(down) { cleaningKeys.Add(data.Key); return 1; }
            // Release keys that were already held when cleaning began, including
            // an in-flight paste modifier, so the destination cannot retain them.
            cleaningKeys.Remove(data.Key);
            return Native.CallNextHookEx(keyboardHook,code,wp,lp);
        }
        if((data.Flags&0x10)!=0 && data.ExtraInfo!=SnippetReplay) return Native.CallNextHookEx(keyboardHook,code,wp,lp);
        if(cleaningKeys.Contains(data.Key)) { if(!down) cleaningKeys.Remove(data.Key); return 1; }
        if(snippetKeys.Consume(data.Key,down,data.ExtraInfo==SnippetReplay)) return 1;
        if(preparingSnippet && data.ExtraInfo!=SnippetReplay)
        {
            if(data.Key==0x1B) { snippetGeneration++; snippetPreparation?.Cancel(); AbortPendingSnippetInput(); return Native.CallNextHookEx(keyboardHook,code,wp,lp); }
            var buffered=deferredSnippetKeys.Capture(data.Key,down,ReplayKey(data,down));
            if(buffered==BufferedInputResult.Forward) return Native.CallNextHookEx(keyboardHook,code,wp,lp);
            if(buffered==BufferedInputResult.Full) { snippetGeneration++; snippetPreparation?.Cancel(); }
            return 1;
        }
        var window=Native.GetForegroundWindow();
        var gui=new Native.GuiThreadInfo { Size=(uint)Marshal.SizeOf<Native.GuiThreadInfo>() };
        Native.GetGUIThreadInfo(0,ref gui);
        if(window!=lastWindow || gui.Focus!=lastFocus)
        { snippets.Clear(); closeGuard.Clear(); closeKeys.Clear(); lastWindow=window; lastFocus=gui.Focus; }
        if(Excluded(window)) { ReleaseSuper(); snippets.Clear(); return Native.CallNextHookEx(keyboardHook,code,wp,lp); }
        if(active.Contains("superKey"))
        {
            if(data.Key==0x14)
            {
                if(down) capsHeld=true;
                else { bool tap=!superHeld; ReleaseSuper(); if(tap) SendKey(0x1B); }
                return 1;
            }
            if(capsHeld && down && !superHeld) { superHeld=true; SendKeys([0x11,0x12,0x10,0x5B],false); }
        }
        if(!down) { closeKeys.Remove(data.Key); return Native.CallNextHookEx(keyboardHook,code,wp,lp); }
        bool ctrl=Down(0x11),alt=Down(0x12),win=Down(0x5B)||Down(0x5C);
        if(active.Contains("quitWindowProtection") && (data.Key==0x73 && alt || data.Key==0x57 && ctrl)
            && settings().ProtectedApps.Any(n=>Path.GetFileNameWithoutExtension(n).Equals(ProcessName(window),StringComparison.OrdinalIgnoreCase)))
        {
            string key=$"{window}:{data.Key}";
            bool repeat=!closeKeys.Add(data.Key);
            if(!closeGuard.Allow(key,Environment.TickCount64,repeat))
            { if(!repeat) queue.TryEnqueue(()=>Notice?.Invoke("Press the close shortcut again within one second to confirm.")); return 1; }
        }
        if(active.Contains("textSnippets") && !suspended && !ctrl && !alt && !win && !superHeld && !OwnWindow(window) && !StandardPasswordField())
        {
            if(data.Key==8) snippets.Backspace();
            else if(data.Key is 0x1B or >=0x21 and <=0x28 or 0xE5 or 0x2E) snippets.Clear();
            else if(data.Key is not (0x10 or 0xA0 or 0xA1))
            {
                string text=data.Key switch { 0x09=>"\t",0x0D=>"\r",_=>TranslateKey(data) };
                if(text.Length==0) snippets.Clear();
                var match=snippets.Push(text,settings().Snippets);
                if(match is not null)
                {
                    snippetKeys.Complete(data.Key,data.ExtraInfo==SnippetReplay);
                    QueueSnippet(match,window,gui.Focus,data);
                    return 1;
                }
            }
        }
        else snippets.Clear();
        return Native.CallNextHookEx(keyboardHook,code,wp,lp);
    }
    private static string TranslateKey(Native.KeyboardData data)
    {
        byte[] state=new byte[256]; Native.GetKeyboardState(state);
        state[0x10]=Down(0x10)?(byte)0x80:(byte)0;
        var text=new StringBuilder(8);
        uint thread=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out _);
        int count=Native.ToUnicodeEx(data.Key,data.ScanCode,state,text,8,4,Native.GetKeyboardLayout(thread));
        return count>0 ? text.ToString() : count<0 ? "\0" : "";
    }
    private nint Mouse(int code, nuint wp, nint lp)
    {
        if(code<0) return Native.CallNextHookEx(mouseHook,code,wp,lp);
        var data=Marshal.PtrToStructure<Native.MouseData>(lp);
        if(CleaningMouse) return 1;
        if((data.Flags&1)!=0) return Native.CallNextHookEx(mouseHook,code,wp,lp);
        // Complete an already swallowed down/up pair even if focus moved to an excluded app.
        uint message=(uint)wp;
        if(message is 0x201 or 0x204 or 0x207 or 0x20B)
        {
            snippets.Clear(); snippetGeneration++;
            if(preparingSnippet) { snippetPreparation?.Cancel(); AbortPendingSnippetInput(); }
        }
        if(message is not(0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208 or 0x20B or 0x20C or Native.WM_MOUSEWHEEL or Native.WM_MOUSEHWHEEL))
            return Native.CallNextHookEx(mouseHook,code,wp,lp);
        if(suppressedUps.Remove(message)) return 1;
        if(message==0x20C && mappedButtons.Remove(data.MouseDataValue>>16)) return 1;
        nint target=Native.WindowFromPoint(data.Point);
        if(Excluded(target)) return Native.CallNextHookEx(mouseHook,code,wp,lp);
        if(active.Contains("mouseClickDebounce") && message is 0x201 or 0x204 or 0x207)
        {
            long now=Environment.TickCount64;
            if(lastClicks.TryGetValue(message,out long previous) && now-previous<settings().ClickDebounceMs)
            { suppressedUps.Add(message+1); return 1; }
            lastClicks[message]=now;
        }
        if(active.Contains("mouseButtonShortcuts") && message==0x20B)
        {
            uint button=data.MouseDataValue>>16;
            string assignment=button==1?settings().BackButtonShortcut:settings().ForwardButtonShortcut;
            if(Shortcut.TryParse(assignment,out var shortcut)) { mappedButtons.Add(button); queue.TryEnqueue(()=>SendShortcut(shortcut)); return 1; }
        }
        if(message==Native.WM_MOUSEWHEEL)
        {
            int delta=(short)(data.MouseDataValue>>16);
            if(active.Contains("scrollHorizontal") && Down(0x12))
            { PostWheel(target,Native.WM_MOUSEHWHEEL,-delta,data.Point); return 1; }
            if(active.Contains("smoothScroll") && delta%120==0)
            {
                scrollTarget=target; scrollPoint=data.Point; remainingScroll+=delta*settings().ScrollMultiplier;
                if(scrollTimer is null) { scrollTimer=queue.CreateTimer(); scrollTimer.Interval=TimeSpan.FromMilliseconds(10); scrollTimer.Tick+=ScrollTick; }
                scrollTimer.Start(); return 1;
            }
        }
        if(message==Native.WM_MOUSEHWHEEL && active.Contains("scrollInverter"))
        { PostWheel(target,Native.WM_MOUSEHWHEEL,-(short)(data.MouseDataValue>>16),data.Point); return 1; }
        return Native.CallNextHookEx(mouseHook,code,wp,lp);
    }
    private void ScrollTick(DispatcherQueueTimer sender, object args)
    {
        int step=(int)Math.Round(remainingScroll*.32);
        if(step==0) step=Math.Sign(remainingScroll);
        PostWheel(scrollTarget,Native.WM_MOUSEWHEEL,step,scrollPoint); remainingScroll-=step;
        if(Math.Abs(remainingScroll)<1) StopScroll();
    }
    private static void PostWheel(nint hwnd,uint msg,int delta,Native.Point point)
    {
        uint modifiers=(Down(0x11)?8u:0)|(Down(0x10)?4u:0);
        Native.PostMessage(hwnd,msg,(nuint)(((uint)(ushort)(short)delta<<16)|modifiers),(nint)((point.X&0xFFFF)|((point.Y&0xFFFF)<<16)));
    }
    private void StopScroll() { scrollTimer?.Stop(); remainingScroll=0; }
    private static bool Down(int key)=>(Native.GetAsyncKeyState(key)&0x8000)!=0;
    private void ReleaseSuper()
    {
        if(superHeld) SendKeys([0x5B,0x10,0x12,0x11],true);
        capsHeld=false; superHeld=false;
    }
    public static bool SendShortcut(Shortcut shortcut)
    {
        var keys=new List<ushort>(); if((shortcut.Modifiers&2)!=0) keys.Add(0x11); if((shortcut.Modifiers&1)!=0) keys.Add(0x12);
        if((shortcut.Modifiers&4)!=0) keys.Add(0x10); if((shortcut.Modifiers&8)!=0) keys.Add(0x5B);
        var inputs=keys.Select(k=>KeyInput(k,0)).ToList();
        inputs.Add(KeyInput((ushort)shortcut.Key,0)); inputs.Add(KeyInput((ushort)shortcut.Key,2));
        keys.Reverse(); inputs.AddRange(keys.Select(k=>KeyInput(k,2)));
        var data=inputs.ToArray(); uint sent=Native.SendInput((uint)data.Length,data,Marshal.SizeOf<Native.Input>());
        if(sent==data.Length) return true;
        if(sent>0) { var release=keys.Prepend((ushort)shortcut.Key).Select(k=>KeyInput(k,2)).ToArray(); Native.SendInput((uint)release.Length,release,Marshal.SizeOf<Native.Input>()); }
        return false;
    }
    private static void SendKeys(IEnumerable<ushort> keys,bool up) => Send(keys.Select(k=>KeyInput(k,up?2u:0)).ToArray());
    private static Native.Input KeyInput(ushort key,uint flags)=>new() { Type=1, Data=new() { Keyboard=new() { Key=key,Flags=flags } } };
    private static void SendKey(ushort key)=>Send([KeyInput(key,0),KeyInput(key,2)]);
    public static void SendText(string text)
    { var inputs=new List<Native.Input>(); AddText(inputs,text); Send(inputs.ToArray()); }
    private static void AddKey(List<Native.Input> inputs,ushort key)
    { inputs.Add(KeyInput(key,0)); inputs.Add(KeyInput(key,2)); }
    private static void AddText(List<Native.Input> inputs,string text)
    {
        foreach(char c in text)
        { var input=KeyInput(0,4); input.Data.Keyboard.Scan=c; inputs.Add(input); input.Data.Keyboard.Flags=6; inputs.Add(input); }
    }
    private static void Send(Native.Input[] inputs) { if(inputs.Length>0) Native.SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Native.Input>()); }
    public void Dispose()
    {
        snippetGeneration++; snippetPreparation?.Cancel(); active.Clear(); Reconcile(); EndCleaning(); StopScroll(); keyboardCallback=null; mouseCallback=null;
    }
}
