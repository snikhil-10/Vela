using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vela.Windows.UI;

namespace Vela.Windows;

public sealed partial class MainWindow : Window
{
    private readonly AppController controller;
    internal MainWindow(AppController controller)
    {
        this.controller=controller; InitializeComponent(); WindowStyle.Configure(this,420,620,true);
        AppWindow.Closing+=(_,e)=> { if(!controller.Quitting) { e.Cancel=true; AppWindow.Hide(); } };
        Activated+=(_,e)=> { if(e.WindowActivationState==WindowActivationState.Deactivated && !controller.Settings.PinFlyout) AppWindow.Hide(); };
    }
    internal void Refresh()
    {
        var features=controller.OrderedFeatures.Where(f=>controller.Settings.EnabledFeatures.Contains(f.Id) && f.Id is not("keepAwake" or "quickLauncher")).ToList();
        ToolGrid.ItemsSource=features; ToolCount.Text=$"{features.Count} enabled";
        EmptyMessage.Visibility=features.Count==0?Visibility.Visible:Visibility.Collapsed;
        AwakeCard.Visibility=controller.Settings.EnabledFeatures.Contains("keepAwake")?Visibility.Visible:Visibility.Collapsed;
        PinButton.IsChecked=controller.Settings.PinFlyout;
        ShortcutHint.Text=controller.Settings.EnabledFeatures.Contains("quickLauncher")?controller.Settings.PanelShortcut:"Lives in your system tray";
        RefreshAwake();
    }
    internal void RefreshAwake()
    { if(controller.Awake is null) return; AwakeStatus.Text=controller.Awake.Status; StopAwake.IsEnabled=controller.Awake.HasTimer; }
    internal void ShowError(string text) { ErrorBar.Message=text; ErrorBar.IsOpen=true; }
    private void ToolClick(object sender,RoutedEventArgs e) { if(((Button)sender).Tag is string id) controller.OpenTool(id); }
    private void SettingsClick(object sender,RoutedEventArgs e)=>controller.ShowSettings();
    private void PinClick(object sender,RoutedEventArgs e) { controller.Settings.PinFlyout=PinButton.IsChecked==true; controller.Save(false); }
    private void StartAwakeClick(object sender,RoutedEventArgs e)
    { if(controller.Quitting) return; try { controller.Awake.Hold(TimeSpan.FromMinutes(int.Parse((string)((Button)sender).Tag))); } catch(Exception error) { controller.Report(error.Message); } }
    private void StopAwakeClick(object sender,RoutedEventArgs e)
    { if(controller.Quitting) return; try { controller.Awake.Release(); } catch(Exception error) { controller.Report(error.Message); } }
}
