using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace Vela.Windows.UI;

public sealed partial class ShelfDropSurface : UserControl
{
    private readonly UISettings preferences=new();
    private Storyboard? fade;
    private TaskCompletionSource? completion;
    private bool emphasized;

    public ShelfDropSurface()
    {
        InitializeComponent();
        Loaded+=(_,_)=> { if(emphasized) _=Transition(true); };
    }

    internal Task Transition(bool visible)
    {
        emphasized=visible;
        double current=Emphasis.Opacity;
        fade?.Stop(); fade=null; completion?.TrySetResult(); completion=null;
        Emphasis.Opacity=current;
        if(!IsLoaded) { Emphasis.Opacity=0; return Task.CompletedTask; }
        double end=visible?1:0;
        if(!preferences.AnimationsEnabled || Math.Abs(current-end)<0.001)
        { Emphasis.Opacity=end; return Task.CompletedTask; }
        var result=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation=new DoubleAnimation { From=current,To=end,
            Duration=new(TimeSpan.FromMilliseconds(visible?160:140)),
            EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
        Storyboard.SetTarget(animation,Emphasis); Storyboard.SetTargetProperty(animation,"Opacity");
        var transition=new Storyboard(); transition.Children.Add(animation);
        transition.Completed+=(_,_)=>
        {
            if(ReferenceEquals(fade,transition))
            { transition.Stop(); Emphasis.Opacity=end; fade=null; completion=null; }
            result.TrySetResult();
        };
        fade=transition; completion=result; transition.Begin(); return result.Task;
    }
    internal void Reset()
    {
        emphasized=false; fade?.Stop(); fade=null;
        completion?.TrySetResult(); completion=null; Emphasis.Opacity=0;
    }
    internal void Fit(string mode,double width,double height)
    {
        bool compact=Math.Min(width,height)<32;
        double padding=compact?2:6;
        HitArea.Clip=new RectangleGeometry { Rect=new(0,0,width,height) };
        Highlight.Margin=new(padding); Highlight.CornerRadius=new(compact?4:6);
        Badge.Margin=new(padding+2); Badge.Width=Badge.Height=compact?6:10;
        Badge.CornerRadius=new(compact?3:5);
        Badge.Visibility=mode=="taskbar"?Visibility.Collapsed:Visibility.Visible;
        Caption.Visibility=mode=="taskbar" && width>=180 && height>=32 ? Visibility.Visible : Visibility.Collapsed;
    }
}
