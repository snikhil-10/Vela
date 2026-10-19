using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Vela.Windows.UI;

internal static class Ui
{
    public static Style Style(string name)=>(Style)Application.Current.Resources[name];
    public static TextBlock Text(string text,string style="VelaBody")=>new() { Text=text,Style=Style(style),FontFamily=new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable") };
    public static StackPanel Stack(double spacing=12)=>new() { Spacing=spacing };
    public static Border Card(string title,string description,UIElement? control=null)
    {
        var grid=new Grid { ColumnSpacing=20 }; grid.ColumnDefinitions.Add(new() { Width=new(1,GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
        var labels=Stack(4); labels.Children.Add(Text(title,"BodyStrongTextBlockStyle")); labels.Children.Add(Text(description,"VelaCaption")); grid.Children.Add(labels);
        if(control is not null)
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control,title);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(control,description);
            if(control is FrameworkElement element) { Grid.SetColumn(element,1); element.VerticalAlignment=VerticalAlignment.Center; }
            grid.Children.Add(control);
            grid.RowDefinitions.Add(new() { Height=GridLength.Auto }); grid.RowDefinitions.Add(new() { Height=GridLength.Auto });
            if(control is FrameworkElement responsive) grid.SizeChanged+=(_,_)=>
            {
                bool narrow=grid.ActualWidth<460;
                Grid.SetColumnSpan(labels,narrow?2:1); Grid.SetRow(responsive,narrow?1:0); Grid.SetColumn(responsive,narrow?0:1);
                Grid.SetColumnSpan(responsive,narrow?2:1); responsive.HorizontalAlignment=HorizontalAlignment.Left;
                responsive.Margin=narrow?new Thickness(0,12,0,0):new Thickness(0);
            };
        }
        return new() { Style=Style("VelaCard"),Child=grid };
    }
    public static Button Button(string title,Action action,bool accent=false)
    {
        var button=new Button { Content=title }; if(accent) button.Style=Style("AccentButtonStyle");
        button.Click+=(_,_)=> { try { action(); } catch(Exception ex) { ((App)Application.Current).Controller?.Report(ex.Message); } }; return button;
    }
    public static ToggleSwitch Toggle(bool value,Action<bool> changed)
    { var toggle=new ToggleSwitch { IsOn=value,OnContent="On",OffContent="Off" }; toggle.Toggled+=(_,_)=>changed(toggle.IsOn); return toggle; }
    public static NumberBox Number(double value,double min,double max,Action<int> change)
    {
        var box=new NumberBox { Value=value,Minimum=min,Maximum=max,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact,Width=120 };
        box.ValueChanged+=(_,e)=> { if(double.IsFinite(e.NewValue)) change((int)e.NewValue); }; return box;
    }
}
