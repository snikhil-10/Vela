using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed class AwakeService(Func<AppSettings> settings, DispatcherQueue queue) : IFeatureService
{
    private DispatcherQueueTimer? timer;
    private DateTimeOffset? until;
    private bool applied;
    public string Id => "keepAwake";
    public bool IsAwake => applied;
    public string Status => applied ? until is { } end ? $"Until {end:t}" : "App rule active" : "Off";
    public event Action? Changed;
    public void Hold(TimeSpan duration) { until = DateTimeOffset.Now + duration; Evaluate(); }
    public void Release() { until=null; Apply(false); Changed?.Invoke(); }
    public void Start()
    {
        timer=queue.CreateTimer(); timer.Interval=TimeSpan.FromSeconds(5); timer.Tick+=Tick; timer.Start(); Evaluate();
    }
    private void Tick(DispatcherQueueTimer sender, object args) => Evaluate();
    public void Evaluate()
    {
        var options=settings();
        bool requested=until > DateTimeOffset.Now;
        if(options.AwakeWithExternalDisplay && WindowWorkspace.Monitors().Count>1) requested=true;
        if (until <= DateTimeOffset.Now) until=null;
        if (!requested && options.AwakeApps.Count > 0)
        {
            foreach (var process in Process.GetProcesses())
            {
                using(process) { try { if (options.AwakeApps.Any(n => Path.GetFileNameWithoutExtension(n).Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase))) requested=true; } catch (Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } }
            }
        }
        if (options.AwakeOnlyOnAc && Native.GetSystemPowerStatus(out var power) && power.ACLineStatus != 1) requested=false;
        Apply(requested); Changed?.Invoke();
    }
    private void Apply(bool value)
    {
        // Called exclusively on the UI thread; execution-state requests are thread scoped.
        uint flags=0x80000000 | (value ? 1u | (settings().KeepDisplayAwake ? 2u : 0u) : 0u);
        if (Native.SetThreadExecutionState(flags)==0) throw new InvalidOperationException("Windows declined the keep-awake request.");
        applied=value;
    }
    public void Stop() { timer?.Stop(); if(timer is not null) timer.Tick-=Tick; timer=null; until=null; Apply(false); }
    public void Dispose() { }
}
