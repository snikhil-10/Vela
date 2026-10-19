using Microsoft.Win32;
using Vela.Windows;
using Vela.Windows.Services;

internal static class AppDataPathsChecks
{
    internal static void Run(string root)
    {
        static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        string original=AppDataPaths.DirectoryPath;
        using var run=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
        object? startup=run?.GetValue("Vela.Utilities");
        Require(!AppDataPaths.IsValidationProfile,"Ordinary startup unexpectedly uses a validation profile.");
        foreach(var args in new[]{
            new[]{"--data-directory"},new[]{"--data-directory","relative"},
            new[]{"--data-directory",original},new[]{"--data-directory",original+"\\"},
            new[]{"--data-directory",@"\\server\share\fixture"},
            new[]{"--data-directory",root,"--data-directory",root}})
        {
            bool rejected=false;
            try { AppDataPaths.Configure(args); } catch(ArgumentException) { rejected=true; }
            Require(rejected && !AppDataPaths.IsValidationProfile && AppDataPaths.DirectoryPath==original,"Invalid profile changed the normal data path.");
        }
        string isolated=Path.Combine(root,"profile fixture");
        AppDataPaths.Configure(["--data-directory",isolated]);
        Require(AppDataPaths.IsValidationProfile && AppDataPaths.DirectoryPath==isolated && !Directory.Exists(isolated),"Profile parsing wrote data or chose a different directory.");
        Require(!StartupService.Enabled,"A validation profile inherited sign-in startup.");
        foreach(bool enabled in new[]{false,true})
        {
            bool rejected=false;
            try { StartupService.Enabled=enabled; } catch(InvalidOperationException) { rejected=true; }
            Require(rejected,"A validation profile was allowed to change sign-in startup.");
        }
        Require(Equals(startup,run?.GetValue("Vela.Utilities")),"Validation changed the normal sign-in entry.");
        StartupDiagnostics.Record("Known validation fixture",new InvalidOperationException("Known fixture failure"));
        Require(File.ReadAllText(Path.Combine(isolated,"startup-error.log")).Contains("Known fixture failure"),"Diagnostics did not use the isolated profile.");
    }
}
