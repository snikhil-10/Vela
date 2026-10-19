using Microsoft.UI.Xaml;

namespace Vela.Windows;

public partial class App : Application
{
    private readonly bool startup;
    private readonly bool showSettings;
    internal AppController? Controller { get; private set; }
    internal RegisteredWaitHandle? ActivationWait { get; set; }
    public App(bool startup, bool showSettings = false)
    {
        this.startup = startup;
        this.showSettings = showSettings;
        UnhandledException+=(_,e)=>StartupDiagnostics.Record("XAML dispatcher: "+e.Message,e.Exception);
        DebugSettings.IsXamlResourceReferenceTracingEnabled=true;
        DebugSettings.XamlResourceReferenceFailed+=(_,e)=>StartupDiagnostics.Record("XAML resource",new InvalidOperationException(e.Message));
        try { InitializeComponent(); }
        catch(Exception error) { StartupDiagnostics.Record("Application resources",error); throw; }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Controller = new AppController();
            Controller.Initialize(startup);
            if (showSettings && Controller.Settings.OnboardingComplete) Controller.ShowSettings();
        }
        catch(Exception error) { StartupDiagnostics.Record("Create application windows",error); throw; }
    }
    private bool quitting;
    internal async void Quit()
    {
        if(quitting) return; quitting=true;
        ActivationWait?.Unregister(null);
        if(Controller is not null) await Controller.FinishOperations();
        Controller?.Dispose();
        Exit();
    }
}
