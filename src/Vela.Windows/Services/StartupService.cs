using Microsoft.Win32;

namespace Vela.Windows.Services;

internal static class StartupService
{
    private const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    public static bool Enabled
    {
        get { if(AppDataPaths.IsValidationProfile) return false; using var key=Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("Vela.Utilities") is string; }
        set
        {
            if(AppDataPaths.IsValidationProfile) throw new InvalidOperationException("Sign-in startup is unavailable in a validation profile.");
            using var key=Registry.CurrentUser.CreateSubKey(RunKey);
            if(value) key.SetValue("Vela.Utilities",$"\"{Environment.ProcessPath}\" --startup");
            else key.DeleteValue("Vela.Utilities",false);
        }
    }
}
