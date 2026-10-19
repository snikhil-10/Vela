using System.Runtime.InteropServices;
using System.Text;

namespace Vela.Windows.Services;

internal sealed record GpuReading(string Id,string Name,uint? Temperature,double? Watts);

// Uses the installed NVIDIA driver's documented read-only NVML APIs. No bundled driver or elevation.
internal sealed class GpuSensors : IDisposable
{
    private nint library;
    private bool initialized;
    private delegate int Initialize();
    private delegate int Count(out uint count);
    private delegate int Handle(uint index,out nint handle);
    private delegate int Name(nint device,StringBuilder name,uint length);
    private delegate int Temperature(nint device,uint sensor,out uint value);
    private delegate int Value(nint device,out uint value);
    private Initialize? shutdown;
    private Count? count;
    private Handle? handle;
    private Name? name;
    private Name? uuid;
    private Temperature? temperature;
    private Value? power;
    public string Availability { get; private set; }="No supported NVIDIA driver sensors were found. CPU, AMD and Intel temperature sensors are not available through this backend.";
    public GpuSensors()
    {
        try
        {
            // Absolute System32 path prevents loading an unrelated nvml.dll from the working directory.
            if(!NativeLibrary.TryLoad(Path.Combine(Environment.SystemDirectory,"nvml.dll"),out library)) return;
            var initialize=Export<Initialize>("nvmlInit_v2"); shutdown=Export<Initialize>("nvmlShutdown");
            count=Export<Count>("nvmlDeviceGetCount_v2"); handle=Export<Handle>("nvmlDeviceGetHandleByIndex_v2");
            name=Export<Name>("nvmlDeviceGetName"); uuid=Export<Name>("nvmlDeviceGetUUID");
            temperature=Export<Temperature>("nvmlDeviceGetTemperature"); power=Export<Value>("nvmlDeviceGetPowerUsage");
            if(initialize is null || shutdown is null || count is null || handle is null || initialize()!=0) return;
            initialized=true; Availability="Read-only NVIDIA driver sensors. Individual readings depend on the GPU and driver. Fan control stays with the firmware.";
        }
        catch(Exception ex) when(ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException) { }
    }
    private T? Export<T>(string symbol) where T:Delegate => NativeLibrary.TryGetExport(library,symbol,out nint address)?Marshal.GetDelegateForFunctionPointer<T>(address):null;
    public IReadOnlyList<GpuReading> Read()
    {
        var readings=new List<GpuReading>();
        if(!initialized || count!(out uint devices)!=0) return readings;
        for(uint i=0;i<Math.Min(devices,16);i++)
        {
            if(handle!(i,out nint device)!=0) continue;
            var label=new StringBuilder(96); name?.Invoke(device,label,(uint)label.Capacity);
            var identifier=new StringBuilder(96);
            string id=uuid is not null && uuid(device,identifier,(uint)identifier.Capacity)==0 && identifier.Length>0
                ?identifier.ToString():$"nvml-index-{i}";
            string displayName=label.Length==0?$"NVIDIA GPU {i+1}":devices>1?$"GPU {i+1} · {label}":label.ToString();
            uint? degrees=temperature is not null && temperature(device,0,out uint temp)==0 && temp<=150?temp:null;
            double? watts=power is not null && power(device,out uint milliwatts)==0?milliwatts/1000.0:null;
            if(degrees is not null || watts is not null) readings.Add(new(id,displayName,degrees,watts));
        }
        return readings;
    }
    public void Dispose()
    { if(initialized) { shutdown?.Invoke(); initialized=false; } if(library!=0) { NativeLibrary.Free(library); library=0; } }
}
