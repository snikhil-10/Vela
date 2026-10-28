using Vela.Core;

internal static class AtomicOutputChecks
{
    internal static void Run(Action<string,Action> test)
    {
        static void Require(bool value) { if(!value) throw new Exception("Atomic output changed a destination before successful commit."); }
        static void WithFile(Action<string,string> check)
        {
            string root=Path.Combine(Path.GetTempPath(),"Vela-Atomic-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string target=Path.Combine(root,"kept.txt"); File.WriteAllText(target,"old bytes");
            try { check(root,target); }
            finally { foreach(string path in Directory.EnumerateFiles(root)) File.SetAttributes(path,FileAttributes.Normal); Directory.Delete(root,true); }
        }
        test("A completed atomic output replaces the selected file and removes staging",()=>WithFile((root,target)=>
        {
            AtomicFileOutput.Write(target,(temporary,token)=>File.WriteAllTextAsync(temporary,"complete 東京",token)).GetAwaiter().GetResult();
            Require(File.ReadAllText(target)=="complete 東京" && Directory.GetFiles(root).Length==1);
        }));
        test("A writer failure after partial output leaves existing destination bytes intact",()=>WithFile((root,target)=>
        {
            bool failed=false;
            try { AtomicFileOutput.Write(target,async (temporary,token)=> { await File.WriteAllTextAsync(temporary,"partial",token); throw new IOException("Generated failure"); }).GetAwaiter().GetResult(); }
            catch(IOException) { failed=true; }
            Require(failed && File.ReadAllText(target)=="old bytes" && Directory.GetFiles(root).Length==1);
        }));
        test("Cancellation after writing but before commit preserves the old destination",()=>WithFile((root,target)=>
        {
            using var cancel=new CancellationTokenSource(); bool canceled=false;
            try { AtomicFileOutput.Write(target,async (temporary,token)=> { await File.WriteAllTextAsync(temporary,"uncommitted",token); cancel.Cancel(); },cancel.Token).GetAwaiter().GetResult(); }
            catch(OperationCanceledException) { canceled=true; }
            Require(canceled && File.ReadAllText(target)=="old bytes" && Directory.GetFiles(root).Length==1);
        }));
        test("Canceled atomic output never invokes its writer",()=>WithFile((root,target)=>
        {
            bool called=false,canceled=false;
            try { AtomicFileOutput.Write(target,(_,_)=> { called=true; return Task.CompletedTask; },new CancellationToken(true)).GetAwaiter().GetResult(); }
            catch(OperationCanceledException) { canceled=true; }
            Require(canceled && !called && File.ReadAllText(target)=="old bytes" && Directory.GetFiles(root).Length==1);
        }));
        test("A failed new-file writer leaves no selected or staging file",()=>WithFile((root,_)=>
        {
            string target=Path.Combine(root,"new.txt"); bool failed=false;
            try { AtomicFileOutput.Write(target,(_,_)=>throw new IOException("Generated failure")).GetAwaiter().GetResult(); }
            catch(IOException) { failed=true; }
            Require(failed && !File.Exists(target) && Directory.GetFiles(root).Length==1);
        }));
        if(OperatingSystem.IsWindows()) test("A busy Windows destination survives a failed commit",()=>WithFile((root,target)=>
        {
            bool failed=false;
            using(var locked=File.Open(target,FileMode.Open,FileAccess.Read,FileShare.Read))
                try { AtomicFileOutput.Write(target,(temporary,token)=>File.WriteAllTextAsync(temporary,"new",token)).GetAwaiter().GetResult(); }
                catch(Exception error) when(error is IOException or UnauthorizedAccessException) { failed=true; }
            Require(failed && File.ReadAllText(target)=="old bytes" && Directory.GetFiles(root).Length==1);
        }));
        if(OperatingSystem.IsWindows()) test("A read-only Windows destination is preserved after commit refusal",()=>WithFile((root,target)=>
        {
            File.SetAttributes(target,File.GetAttributes(target)|FileAttributes.ReadOnly); bool failed=false;
            try { AtomicFileOutput.Write(target,(temporary,token)=>File.WriteAllTextAsync(temporary,"new",token)).GetAwaiter().GetResult(); }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException) { failed=true; }
            Require(failed && File.ReadAllText(target)=="old bytes" && Directory.GetFiles(root).Length==1 && (File.GetAttributes(target)&FileAttributes.ReadOnly)!=0);
        }));
    }
}
