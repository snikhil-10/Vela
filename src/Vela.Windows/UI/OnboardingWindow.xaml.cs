using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vela.Core;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

public sealed partial class OnboardingWindow : Window
{
    private readonly AppController controller;
    private int step;
    private bool rendering;
    private string selectedPreset="Custom";
    internal OnboardingWindow(AppController controller)
    {
        this.controller=controller; InitializeComponent(); WindowStyle.Configure(this,680,760);
        step=controller.Settings.OnboardingStep; StartupToggle.IsOn=StartupService.Enabled;
        StartupToggle.IsEnabled=!AppDataPaths.IsValidationProfile; Render();
    }
    private void Render()
    {
        rendering=true;
        string[] titles=["Make yourself at home","Choose your tools","You're in control","You're all set"];
        string[] captions=["A few useful extras, right where you need them.","Start small. Add or remove a tool whenever you like.","Only selected features run in the background.","Your workspace, with a few thoughtful additions."];
        Heading.Text=titles[step]; Description.Text=captions[step]; StepLabel.Text=$"{step+1} of 4"; Progress.Value=step+1;
        WelcomePage.Visibility=step==0?Visibility.Visible:Visibility.Collapsed;
        FeaturesPage.Visibility=step==1?Visibility.Visible:Visibility.Collapsed;
        AccessPage.Visibility=step==2?Visibility.Visible:Visibility.Collapsed;
        DonePage.Visibility=step==3?Visibility.Visible:Visibility.Collapsed;
        BackButton.IsEnabled=step>0; NextButton.Content=step==3?"Let's go":"Next";
        if(step==1)
        {
            PresetBox.SelectedItem=selectedPreset; FeatureChoices.Children.Clear();
            foreach(var group in FeatureCatalog.All.GroupBy(f=>f.Group))
            {
                FeatureChoices.Children.Add(new TextBlock { Text=group.Key,Style=Ui.Style("BodyStrongTextBlockStyle"),Margin=new(0,12,0,4) });
                foreach(var feature in group)
                {
                    var check=new CheckBox { Content=feature.Name,IsChecked=controller.Settings.EnabledFeatures.Contains(feature.Id) };
                    ToolTipService.SetToolTip(check,feature.Description);
                    check.Checked+=(_,_)=> { if(!rendering) { MarkCustom(); controller.Settings.EnabledFeatures.Add(feature.Id); controller.Save(); } };
                    check.Unchecked+=(_,_)=> { if(!rendering) { MarkCustom(); controller.Settings.EnabledFeatures.Remove(feature.Id); controller.Save(); } };
                    FeatureChoices.Children.Add(check);
                }
            }
        }
        if(step==2)
        {
            AccessChoices.Children.Clear();
            foreach(var feature in FeatureCatalog.All.Where(f=>controller.Settings.EnabledFeatures.Contains(f.Id)))
                AccessChoices.Children.Add(Ui.Card(feature.Name,feature.Access));
        }
        rendering=false;
    }
    private void PresetChanged(object sender,SelectionChangedEventArgs e)
    {
        if(rendering || PresetBox.SelectedItem is not string preset || preset=="Custom") return;
        selectedPreset=preset;
        controller.Settings.EnabledFeatures=FeatureCatalog.Preset(preset); controller.Save(); Render();
    }
    private void MarkCustom() { selectedPreset="Custom"; PresetBox.SelectedItem=selectedPreset; }
    private void BackClick(object sender,RoutedEventArgs e) { step=Math.Max(0,step-1); Persist(); Render(); }
    private void NextClick(object sender,RoutedEventArgs e)
    {
        try
        {
            if(step==2 && !AppDataPaths.IsValidationProfile) StartupService.Enabled=StartupToggle.IsOn;
            if(step==3) { controller.CompleteOnboarding(); Close(); return; }
            step++; Persist(); Render();
        }
        catch(Exception ex) { ErrorBar.Message=ex.Message; ErrorBar.IsOpen=true; }
    }
    private void Persist() { controller.Settings.OnboardingStep=step; controller.Save(false); }
}
