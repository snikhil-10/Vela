using Microsoft.UI.Dispatching;
using Vela.Core;

namespace Vela.Windows.Services;

internal sealed class ThermalAlerts(Func<AppSettings> settings,DispatcherQueue queue,Action<string,string> notify):IFeatureService
{
    private DispatcherQueueTimer? timer;
    private readonly Dictionary<string,SustainedAlertGate> gates=[];
    private bool busy,running;
    private int generation;
    public string Id=>"thermalAlerts";
    public void Start() { if(running) return; running=true; timer=queue.CreateTimer(); timer.Interval=TimeSpan.FromSeconds(5); timer.Tick+=Tick; timer.Start(); }
    private async void Tick(DispatcherQueueTimer sender,object args)
    {
        if(busy || !running) return; busy=true; int sampledGeneration=generation;
        try
        {
            var readings=await Task.Run(()=> { using var sensors=new GpuSensors(); return sensors.Read(); });
            if(!running || sampledGeneration!=generation) return;
            long now=Environment.TickCount64; int threshold=settings().GpuAlertCelsius;
            foreach(var reading in readings)
            {
                if(!gates.TryGetValue(reading.Id,out var gate)) gates[reading.Id]=gate=new();
                if(gate.Observe(reading.Temperature,threshold,now))
                    notify("GPU temperature is high",$"{reading.Name} has stayed above {threshold} °C for 30 seconds.");
            }
            foreach(string missing in gates.Keys.Except(readings.Select(r=>r.Id)).ToArray()) gates[missing].ResetObservation();
        }
        catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception) { ResetObservations(); }
        finally { busy=false; }
    }
    internal void ResetObservations() { generation++; foreach(var gate in gates.Values) gate.ResetObservation(); }
    public void Stop() { running=false; timer?.Stop(); if(timer is not null) timer.Tick-=Tick; timer=null; ResetObservations(); }
    public void Dispose()=>Stop();
}
