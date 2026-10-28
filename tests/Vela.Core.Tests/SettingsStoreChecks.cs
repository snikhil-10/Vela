using Vela.Core;

internal static class SettingsStoreChecks
{
    internal static void Run(Action<string,Action> test)
    {
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        static void Rejected(Action action)
        {
            try { action(); } catch(Exception error) when(error is IOException or UnauthorizedAccessException) { return; }
            throw new Exception("A settings write was accepted without preserving the old file.");
        }
        static void InDirectory(Action<string> check)
        {
            string parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string directory=Path.Combine(parent,"Vela-Settings-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { check(directory); }
            finally
            {
                if(Path.GetDirectoryName(Path.GetFullPath(directory))!=parent || !Path.GetFileName(directory).StartsWith("Vela-Settings-",StringComparison.Ordinal))
                    throw new Exception("Refusing fixture cleanup outside its owned temporary directory.");
                Directory.Delete(directory,true);
            }
        }
        test("Successful settings saves preserve an unrelated legacy temporary file",()=>InDirectory(directory=>
        {
            string path=Path.Combine(directory,"settings.json"),legacy=path+".tmp";
            File.WriteAllText(legacy,"unrelated existing file");
            var store=new SettingsStore(path); store.Save(new AppSettings { Theme="Dark" });
            Require(store.Load().Theme=="Dark","The saved settings did not reload.");
            Require(File.Exists(legacy) && File.ReadAllText(legacy)=="unrelated existing file","Save overwrote or removed a staging file it did not own.");
        }));
        test("A successful reload clears an earlier settings recovery warning",()=>InDirectory(directory=>
        {
            string path=Path.Combine(directory,"settings.json"); File.WriteAllText(path,"{");
            var store=new SettingsStore(path); store.Load(); Require(store.LoadWarning is not null,"Missing recovery warning.");
            File.WriteAllText(path,SettingsStore.Serialize(new AppSettings { Theme="Dark" }));
            Require(store.Load().Theme=="Dark" && store.LoadWarning is null,"A repaired settings file retained an obsolete warning.");
        }));
        test("Every changed unreadable settings file gets a complete distinct recovery copy",()=>InDirectory(directory=>
        {
            string path=Path.Combine(directory,"settings.json"); var store=new SettingsStore(path);
            var versions=Enumerable.Range(0,8).Select(index=>"{broken-"+index).ToArray();
            foreach(string version in versions) { File.WriteAllText(path,version); store.Load(); }
            var backups=Directory.GetFiles(directory,"settings.json.unreadable-*").Select(File.ReadAllText).ToArray();
            Require(versions.All(backups.Contains),"A later unreadable original was left without its own recovery copy.");
            Require(File.ReadAllText(path)==versions[^1],"Recovery modified the original file.");
        }));
        if(!OperatingSystem.IsWindows()) return;
        test("A refused Windows settings commit preserves old bytes and removes only owned staging",()=>InDirectory(directory=>
        {
            string path=Path.Combine(directory,"settings.json"); File.WriteAllText(path,"original bytes");
            var store=new SettingsStore(path);
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) Rejected(()=>store.Save(new()));
            Require(File.ReadAllText(path)=="original bytes" && Directory.GetFiles(directory).Length==1,"A failed commit changed the destination or leaked staged settings.");
        }));
        test("A readable-delete-share lock cannot let defaults replace unbacked unreadable settings",()=>InDirectory(directory=>
        {
            string path=Path.Combine(directory,"settings.json"); const string original="{recover this original";
            File.WriteAllText(path,original); var store=new SettingsStore(path);
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Delete))
            {
                var fallback=store.Load(); Require(store.LoadWarning is not null,"The failed read had no warning.");
                Rejected(()=>store.Save(fallback));
            }
            Require(File.ReadAllText(path)==original && Directory.GetFiles(directory).Length==1,"Settings replaced the only original while backup access was denied.");
        }));
        test("Settings recovery retries preservation after a temporary sharing failure",()=>InDirectory(directory=>
        {
            string path=Path.Combine(directory,"settings.json"); const string original="{recover after unlock";
            File.WriteAllText(path,original); var store=new SettingsStore(path); AppSettings fallback;
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)) fallback=store.Load();
            fallback.Theme="Dark"; store.Save(fallback);
            Require(store.Load().Theme=="Dark","Save did not recover after the file was unlocked.");
            Require(Directory.GetFiles(directory,"settings.json.unreadable-*").Any(file=>File.ReadAllText(file)==original),"Retry replaced the original without making a recovery copy.");
        }));
    }
}
