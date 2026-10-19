using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private void Brightness()
    {
        Add("External monitors","Vela uses DDC/CI only for connected monitors that report brightness support. Enable DDC/CI in the monitor's menu if available. Dock, cable, HDR and driver support can affect availability.");
        var cards=Ui.Stack(12); Body.Children.Add(cards);
        async Task Refresh()
        {
            cards.Children.Clear();
            var monitors=await MonitorBrightness.Read();
            foreach(var monitor in monitors)
            {
                var slider=new Slider { Header=monitor.Name,Minimum=monitor.Minimum,Maximum=monitor.Maximum,Value=monitor.Current,StepFrequency=1 };
                cards.Children.Add(slider);
                cards.Children.Add(AsyncButton("Apply brightness",async ()=> { await MonitorBrightness.Set(monitor.Id,(uint)slider.Value); Message("Monitor brightness changed."); }));
            }
            if(monitors.Count==0) cards.Children.Add(Ui.Text("No monitor exposes DDC/CI brightness on this connection. Virtual displays do not support this control."));
        }
        Body.Children.Add(AsyncButton("Refresh displays",Refresh));
        Body.Children.Add(Ui.Text("Brightness changes remain in the monitor after Vela exits. Test a small change on your monitor first.","VelaCaption"));
        Root.Loaded+=async (_,_)=> { try { await Refresh(); } catch(Exception ex) { Message(ex.Message,true); } };
    }
    private void Thermal()
    {
        var readings=Ui.Stack(12); Body.Children.Add(readings);
        Body.Children.Add(AsyncButton("Read available sensors",async ()=>
        {
            var snapshot=await Task.Run(()=> { using var sensors=new GpuSensors(); return (sensors.Availability,Values:sensors.Read()); });
            readings.Children.Clear(); readings.Children.Add(Ui.Text(snapshot.Availability,"VelaCaption"));
            foreach(var gpu in snapshot.Values) readings.Children.Add(Ui.Card(gpu.Name,$"Temperature: {(gpu.Temperature is {} t?$"{t} °C":"unavailable")} · Board power: {(gpu.Watts is {} w?$"{w:F1} W":"unavailable")}"));
            if(snapshot.Values.Count==0) Message("No supported hardware sensor readings are available on this computer.");
        }));
        Add("GPU temperature alert","Notify after 30 seconds above this temperature. Alerts have a 15-minute cooldown. Available NVIDIA sensors are sampled while this feature is enabled.",Ui.Number(controller.Settings.GpuAlertCelsius,60,100,n=> { controller.Settings.GpuAlertCelsius=n; controller.Save(false); }));
        Add("Hardware support","NVIDIA NVML works across board brands when the installed driver exposes the readings. EVGA branding alone does not guarantee every reading. CPU and motherboard fan controls require a separate supported vendor backend; Vela does not install a kernel driver.");
    }
}
