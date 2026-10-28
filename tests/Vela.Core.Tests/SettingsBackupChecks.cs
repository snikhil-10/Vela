using System.Text;
using Vela.Core;

internal static class SettingsBackupChecks
{
    internal static void Run(Action<string,Action> test)
    {
        static void Require(bool value,string message) { if(!value) throw new Exception(message); }
        static void Rejected<T>(Func<Task> action) where T:Exception
        { try { action().GetAwaiter().GetResult(); } catch(T) { return; } throw new Exception("Expected "+typeof(T).Name); }
        static void InDirectory(Func<string,Task> check)
        {
            string parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string directory=Path.Combine(parent,"Vela-Backup-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { check(directory).GetAwaiter().GetResult(); }
            finally
            {
                if(Path.GetDirectoryName(Path.GetFullPath(directory))!=parent || !Path.GetFileName(directory).StartsWith("Vela-Backup-",StringComparison.Ordinal))
                    throw new Exception("Refusing fixture cleanup outside its owned temporary directory.");
                Directory.Delete(directory,true);
            }
        }
        test("Settings backups round-trip Unicode, disabled preferences and the accepted shelf target",()=>InDirectory(async directory=>
        {
            var source=new AppSettings { EnabledFeatures=[],ShelfDropTarget="velaIcon",ShelfRemoveAfterDrop=true,Theme="Dark",Snippets=[new("署名",";署名","Hello 東京\n🌙")],InputExcludedApps=["editor.exe"] };
            string path=Path.Combine(directory,"backup.json"); await SettingsBackup.Write(path,source);
            var restored=await SettingsBackup.Read(path);
            Require(restored.EnabledFeatures.Count==0 && restored.ShelfDropTarget=="velaIcon" && restored.ShelfRemoveAfterDrop && restored.Theme=="Dark","Feature or shelf preferences changed.");
            Require(restored.Snippets.Single().Text==source.Snippets.Single().Text && restored.InputExcludedApps.Single()=="editor.exe","Unicode or disabled-feature preferences changed.");
            Require(Directory.GetFiles(directory).Length==1,"Backup staging leaked.");
        }));
        test("Settings backup import accepts UTF-8, UTF-16 and UTF-32 BOM files",()=>InDirectory(async directory=>
        {
            foreach(Encoding encoding in new Encoding[]{new UTF8Encoding(true),Encoding.Unicode,Encoding.BigEndianUnicode,new UTF32Encoding(false,true),new UTF32Encoding(true,true)})
            {
                string path=Path.Combine(directory,"encoded.json");
                await File.WriteAllTextAsync(path,SettingsStore.Serialize(new AppSettings { Theme="Light" }),encoding);
                Require((await SettingsBackup.Read(path)).Theme=="Light","A BOM-encoded backup failed to import.");
            }
        }));
        test("Malformed, empty, future and invalid-encoding backups fail without altering their input",()=>InDirectory(async directory=>
        {
            string path=Path.Combine(directory,"bad.json");
            foreach(byte[] bytes in new[]{Encoding.UTF8.GetBytes("{"),Encoding.UTF8.GetBytes("null"),Encoding.UTF8.GetBytes("{\"Version\":99}"),new byte[]{0x7B,0x22,0x78,0x22,0x3A,0x22,0xFF,0x22,0x7D},new byte[]{0xFF,0xFE,0x7B,0,0x22,0,0x78,0,0x22,0,0x3A,0,0x22,0,0,0xD8,0x22,0,0x7D,0}})
            {
                await File.WriteAllBytesAsync(path,bytes); bool failed=false;
                try { await SettingsBackup.Read(path); } catch(Exception error) when(error is System.Text.Json.JsonException or NotSupportedException or DecoderFallbackException) { failed=true; }
                Require(failed && File.ReadAllBytes(path).SequenceEqual(bytes),"Invalid backup was accepted or modified.");
            }
        }));
        test("The 4 MB settings import limit includes file bytes and accepts its exact boundary",()=>InDirectory(async directory=>
        {
            string path=Path.Combine(directory,"sized.json");
            await File.WriteAllTextAsync(path,"{}"+new string(' ',SettingsBackup.MaximumBytes-2),new UTF8Encoding(false));
            Require(new FileInfo(path).Length==SettingsBackup.MaximumBytes && (await SettingsBackup.Read(path)).Version==1,"An exact-boundary backup failed.");
            await File.AppendAllTextAsync(path," ");
            Rejected<InvalidDataException>(()=>SettingsBackup.Read(path));
            Require(new FileInfo(path).Length==SettingsBackup.MaximumBytes+1,"Rejected input changed.");
        }));
        test("An oversized settings export preserves the previous backup and source settings",()=>InDirectory(async directory=>
        {
            string path=Path.Combine(directory,"kept.json"); await File.WriteAllTextAsync(path,"old backup");
            var settings=new AppSettings { Snippets=[new("Large",";large",new string('x',SettingsBackup.MaximumBytes))] };
            Rejected<InvalidDataException>(()=>SettingsBackup.Write(path,settings));
            Require(File.ReadAllText(path)=="old backup" && settings.Snippets.Single().Text.Length==SettingsBackup.MaximumBytes && Directory.GetFiles(directory).Length==1,"Rejected export mutated data or left staging.");
        }));
        test("Canceled settings backup reads and writes preserve every existing byte",()=>InDirectory(async directory=>
        {
            string path=Path.Combine(directory,"kept.json"); const string original="{\"Version\":1}"; await File.WriteAllTextAsync(path,original);
            using var canceled=new CancellationTokenSource(); canceled.Cancel();
            Rejected<OperationCanceledException>(()=>SettingsBackup.Read(path,canceled.Token));
            Rejected<OperationCanceledException>(()=>SettingsBackup.Write(path,new(),canceled.Token));
            Require(File.ReadAllText(path)==original && Directory.GetFiles(directory).Length==1,"Canceled backup touched input/output.");
        }));
        test("A settings export snapshots its input before asynchronous writing",()=>InDirectory(async directory=>
        {
            string path=Path.Combine(directory,"snapshot.json"); var settings=new AppSettings { Theme="Dark",Snippets=[new("Name",";name","Before")] };
            Task writing=SettingsBackup.Write(path,settings); settings.Theme="Light"; settings.Snippets.Clear(); await writing;
            var saved=await SettingsBackup.Read(path);
            Require(saved.Theme=="Dark" && saved.Snippets.Single().Text=="Before","An in-flight backup followed later settings mutations.");
        }));
        test("A future-version settings export cannot replace a readable backup",()=>InDirectory(async directory=>
        {
            string path=Path.Combine(directory,"kept.json"); await File.WriteAllTextAsync(path,"old backup");
            Rejected<NotSupportedException>(()=>SettingsBackup.Write(path,new AppSettings { Version=99 }));
            Require(File.ReadAllText(path)=="old backup" && Directory.GetFiles(directory).Length==1,"A future export changed the previous backup.");
        }));
    }
}
