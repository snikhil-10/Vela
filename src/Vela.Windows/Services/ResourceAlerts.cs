using Microsoft.UI.Dispatching;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed class ResourceAlerts(Func<AppSettings> settings, DispatcherQueue queue, Action<string,string> notify) : IFeatureService
{
    private DispatcherQueueTimer? timer;
    private long lastIdle,lastKernel,lastUser;
    private bool cpuBaseline;
    private readonly SustainedAlertGate cpu=new(), memory=new(), disk=new(holdMilliseconds:0,below:true);
    public string Id=>"resourceAlerts";
    public void Start()
    {
        cpuBaseline=Native.GetSystemTimes(out lastIdle,out lastKernel,out lastUser);
        timer=queue.CreateTimer(); timer.Interval=TimeSpan.FromSeconds(5); timer.Tick+=Tick; timer.Start();
    }
    private void Tick(DispatcherQueueTimer sender, object args)
    {
        var s=settings(); long now=Environment.TickCount64;
        if(Native.GetSystemTimes(out long idle,out long kernel,out long user))
        {
            long total=kernel-lastKernel+user-lastUser;
            double? load=cpuBaseline && total>0 && idle>=lastIdle && kernel>=lastKernel && user>=lastUser
                ?Math.Clamp(100.0*(total-(idle-lastIdle))/total,0,100):null;
            if(cpu.Observe(load,s.CpuAlertPercent,now)) notify("Sustained CPU usage",$"CPU usage has stayed above {s.CpuAlertPercent}% for 30 seconds.");
            lastIdle=idle; lastKernel=kernel; lastUser=user;
            cpuBaseline=true;
        }
        else { cpuBaseline=false; cpu.ResetObservation(); }
        var mem=new Native.MemoryStatus { Length=(uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MemoryStatus>() };
        if(Native.GlobalMemoryStatusEx(ref mem))
        { if(memory.Observe(mem.Load,s.MemoryAlertPercent,now)) notify("Memory is running high",$"Memory usage has stayed above {s.MemoryAlertPercent}% for 30 seconds."); }
        else memory.ResetObservation();
        try
        {
            var drive=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            double? free=drive.IsReady && drive.TotalSize>0?100.0*drive.AvailableFreeSpace/drive.TotalSize:null;
            if(disk.Observe(free,s.DiskAlertPercent,now))
                notify("Disk space is low",$"Your Windows drive has less than {s.DiskAlertPercent}% free space.");
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { disk.ResetObservation(); }
    }
    internal void ResetObservations() { cpuBaseline=false; cpu.ResetObservation(); memory.ResetObservation(); disk.ResetObservation(); }
    public void Stop() { timer?.Stop(); if(timer is not null) timer.Tick-=Tick; timer=null; ResetObservations(); }
    public void Dispose()=>Stop();
}
