using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed class AwakeService : IFeatureService
{
    private readonly Func<AppSettings> settings;
    private readonly DispatcherQueue queue;
    private readonly AwakeState state;
    private readonly Func<bool?> pluggedIn;
    private readonly Func<bool> externalDisplay;
    private readonly Func<IReadOnlyCollection<string>> processes;
    private readonly Action<bool,bool> applyRequest;
    private DispatcherQueueTimer? timer;
    private bool applied,displayApplied,failed;
    private AwakeDecision decision;
    internal AwakeService(Func<AppSettings> settings,DispatcherQueue queue)
        :this(settings,queue,TimeProvider.System,ReadPower,()=>WindowWorkspace.Monitors().Count>1,ReadProcesses,ApplyNative) { }
    internal AwakeService(Func<AppSettings> settings,DispatcherQueue queue,TimeProvider clock,Func<bool?> pluggedIn,
        Func<bool> externalDisplay,Func<IReadOnlyCollection<string>> processes,Action<bool,bool> applyRequest)
    {
        this.settings=settings; this.queue=queue; state=new(clock); this.pluggedIn=pluggedIn;
        this.externalDisplay=externalDisplay; this.processes=processes; this.applyRequest=applyRequest;
    }
    public string Id => "keepAwake";
    public bool IsAwake => applied;
    public bool HasTimer=>state.Remaining>TimeSpan.Zero;
    public string Status
    {
        get
        {
            var remaining=state.Remaining;
            return failed?"Request unavailable":decision.Reason switch
            {
                AwakeReason.Timer=>remaining>TimeSpan.Zero?$"{Math.Ceiling(remaining.TotalMinutes)} min remaining":"Timer ending",
                AwakeReason.Application=>"App rule active",AwakeReason.ExternalDisplay=>"Display rule active",
                AwakeReason.Battery=>"Paused on battery",AwakeReason.PowerUnavailable=>"Paused: power status unavailable",_=>"Off"
            };
        }
    }
    public event Action? Changed;
    public event Action<string>? Error;
    public void Hold(TimeSpan duration) { if(state.Hold(duration)) Evaluate(); }
    public void Release() { state.Release(); Evaluate(); }
    public void Start()
    {
        if(state.IsActive) return;
        state.Start();
        timer=queue.CreateTimer(); timer.Interval=TimeSpan.FromSeconds(5); timer.Tick+=Tick; timer.Start(); Evaluate();
    }
    private void Tick(DispatcherQueueTimer sender, object args)
    {
        try { Evaluate(); }
        catch(InvalidOperationException error) { Error?.Invoke(error.Message); }
    }
    public void Evaluate()
    {
        if(!state.IsActive) { decision=new(false,AwakeReason.Off,TimeSpan.Zero); Apply(false,false); Changed?.Invoke(); return; }
        var options=settings();
        bool timed=HasTimer;
        bool display=!timed && options.AwakeWithExternalDisplay && Probe(externalDisplay,false);
        bool app=!timed && !display && options.AwakeApps.Count>0 && Probe(processes,Array.Empty<string>()).Any(process=>options.AwakeApps.Any(
            name=>Path.GetFileNameWithoutExtension(name).Equals(process,StringComparison.OrdinalIgnoreCase)));
        decision=state.Decide(options.AwakeOnlyOnAc,options.AwakeOnlyOnAc?Probe(pluggedIn,null):null,app,display);
        Apply(decision.Requested,decision.Requested && options.KeepDisplayAwake); Changed?.Invoke();
    }
    private void Apply(bool value,bool display)
    {
        if(applied==value && displayApplied==display) { failed=false; return; }
        try { applyRequest(value,display); applied=value; displayApplied=display; failed=false; }
        catch { failed=true; throw; }
    }
    private T Probe<T>(Func<T> read,T unavailable)
    {
        try { return read(); }
        catch(Exception error) when(error is InvalidOperationException or System.ComponentModel.Win32Exception)
        { Error?.Invoke("Keep-awake rule unavailable: "+error.Message); return unavailable; }
    }
    private static void ApplyNative(bool value,bool display)
    {
        // Called exclusively on the UI thread; execution-state requests are thread scoped.
        uint flags=0x80000000 | (value ? 1u | (display ? 2u : 0u) : 0u);
        if (Native.SetThreadExecutionState(flags)==0) throw new InvalidOperationException("Windows declined the keep-awake request.");
    }
    private static bool? ReadPower()=>Native.GetSystemPowerStatus(out var power) && power.ACLineStatus is 0 or 1 ? power.ACLineStatus==1 : null;
    private static IReadOnlyCollection<string> ReadProcesses()
    {
        var names=new List<string>();
        foreach(var process in Process.GetProcesses())
            using(process) { try { names.Add(process.ProcessName); } catch(Exception error) when(error is InvalidOperationException or System.ComponentModel.Win32Exception) { } }
        return names;
    }
    public void Stop()
    {
        timer?.Stop(); if(timer is not null) timer.Tick-=Tick; timer=null; state.Stop();
        decision=new(false,AwakeReason.Off,TimeSpan.Zero); Apply(false,false); Changed?.Invoke();
    }
    public void Dispose()=>Stop();
}
