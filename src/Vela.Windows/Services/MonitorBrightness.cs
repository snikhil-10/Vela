using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Vela.Windows.Services;

internal sealed record MonitorBrightnessInfo(string Id,string Name,uint Minimum,uint Current,uint Maximum);

// Physical handles are scoped to a single operation. Hot-plugged handles are never cached.
internal static class MonitorBrightness
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    public static async Task<IReadOnlyList<MonitorBrightnessInfo>> Read()
    {
        await Gate.WaitAsync();
        try { return await Task.Run(()=>Visit(null,null)); }
        finally { Gate.Release(); }
    }
    public static async Task Set(string id,uint value)
    {
        await Gate.WaitAsync();
        try { await Task.Run(()=>Visit(id,value)); }
        finally { Gate.Release(); }
    }
    private static IReadOnlyList<MonitorBrightnessInfo> Visit(string? target,uint? requested)
    {
        var result=new List<MonitorBrightnessInfo>(); bool found=false;
        foreach(var display in WindowWorkspace.Monitors())
        {
            if(!GetNumberOfPhysicalMonitorsFromHMONITOR(display.Handle,out uint count) || count is 0 or >32) continue;
            var physical=new PhysicalMonitor[count];
            if(!GetPhysicalMonitorsFromHMONITOR(display.Handle,count,physical)) continue;
            try
            {
                for(int i=0;i<physical.Length;i++)
                {
                    string id=$"{display.Id}/{i}";
                    if(target is not null && target!=id) continue;
                    var monitor=physical[i];
                    if(!GetMonitorCapabilities(monitor.Handle,out uint capabilities,out _) || (capabilities&2)==0 ||
                        !GetMonitorBrightness(monitor.Handle,out uint min,out uint current,out uint max) || min>=max) continue;
                    if(requested is {} value)
                    {
                        if(!SetMonitorBrightness(monitor.Handle,Math.Clamp(value,min,max))) throw new Win32Exception(Marshal.GetLastWin32Error(),"The monitor rejected the brightness change.");
                        found=true;
                    }
                    result.Add(new(id,string.IsNullOrWhiteSpace(monitor.Description)?display.Name:monitor.Description,min,current,max));
                }
            }
            finally { foreach(var monitor in physical) if(monitor.Handle!=0) DestroyPhysicalMonitor(monitor.Handle); }
        }
        if(target is not null && !found) throw new InvalidOperationException("This monitor is no longer available or does not expose DDC/CI brightness. Refresh displays and check its on-screen menu.");
        return result;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct PhysicalMonitor
    { public nint Handle; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Description; }
    [DllImport("dxva2.dll",SetLastError=true)] private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor,out uint count);
    [DllImport("dxva2.dll",SetLastError=true,CharSet=CharSet.Unicode)] private static extern bool GetPhysicalMonitorsFromHMONITOR(nint monitor,uint count,[Out] PhysicalMonitor[] physical);
    [DllImport("dxva2.dll")] private static extern bool DestroyPhysicalMonitor(nint monitor);
    [DllImport("dxva2.dll")] private static extern bool GetMonitorCapabilities(nint monitor,out uint capabilities,out uint temperatures);
    [DllImport("dxva2.dll")] private static extern bool GetMonitorBrightness(nint monitor,out uint min,out uint current,out uint max);
    [DllImport("dxva2.dll",SetLastError=true)] private static extern bool SetMonitorBrightness(nint monitor,uint brightness);
}
