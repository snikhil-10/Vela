using Vela.Core;

internal static class ClipboardChecks
{
    public static void Run(Action<string,Action> test)
    {
        static void Require(bool condition) { if(!condition) throw new Exception("Clipboard restoration violated ownership or retry limits."); }
        test("A newer clipboard copy prevents restoration without attempting a write",()=>
        {
            int writes=0;
            var result=ClipboardRestoration.Restore(10,()=>11,()=> { writes++; return true; }).GetAwaiter().GetResult();
            Require(result==ClipboardRestoreResult.Superseded && writes==0);
        });
        test("Transient clipboard refusal retries the original content before reporting success",()=>
        {
            int writes=0;
            var result=ClipboardRestoration.Restore(10,()=>10,()=>++writes==3).GetAwaiter().GetResult();
            Require(result==ClipboardRestoreResult.Restored && writes==3);
        });
        test("A new copy during a clipboard retry wins over the saved original",()=>
        {
            uint sequence=10; int writes=0;
            var result=ClipboardRestoration.Restore(10,()=>sequence,()=> { writes++; sequence=11; return false; }).GetAwaiter().GetResult();
            Require(result==ClipboardRestoreResult.Superseded && writes==1);
        });
        test("A persistently busy clipboard reports failure after a bounded number of writes",()=>
        {
            int writes=0;
            var result=ClipboardRestoration.Restore(10,()=>10,()=> { writes++; return false; }).GetAwaiter().GetResult();
            Require(result==ClipboardRestoreResult.Busy && writes==3);
        });
        static Exception Busy()=>new System.Runtime.InteropServices.COMException("",unchecked((int)0x800401D0));
        static void Throws<T>(Action action) where T:Exception
        { try { action(); } catch(T) { return; } throw new Exception("Expected clipboard preparation failure."); }
        test("Clipboard preparation retries a temporary open failure against fresh views",()=>
        {
            int reads=0; var snapshot=new Snapshot();
            var result=ClipboardPreparation.Read(10,()=>10,()=>++reads<3?Task.FromException<Snapshot>(Busy()):Task.FromResult(snapshot),default).GetAwaiter().GetResult();
            Require(reads==3 && ReferenceEquals(result,snapshot) && !snapshot.Disposed);
        });
        test("A persistently locked clipboard stops preparation after six reads",()=>
        {
            int reads=0;
            Throws<System.Runtime.InteropServices.COMException>(()=>ClipboardPreparation.Read(10,()=>10,()=> { reads++; return Task.FromException<Snapshot>(Busy()); },default).GetAwaiter().GetResult());
            Require(reads==6);
        });
        test("A newer copy cancels clipboard preparation before reading",()=>
        {
            int reads=0;
            Throws<OperationCanceledException>(()=>ClipboardPreparation.Read(10,()=>11,()=> { reads++; return Task.FromResult(new Snapshot()); },default).GetAwaiter().GetResult());
            Require(reads==0);
        });
        test("A copy made during a snapshot discards that snapshot",()=>
        {
            uint sequence=10; var snapshot=new Snapshot();
            Throws<OperationCanceledException>(()=>ClipboardPreparation.Read(10,()=>sequence,async()=> { await Task.Yield(); sequence=11; return snapshot; },default).GetAwaiter().GetResult());
            Require(snapshot.Disposed);
        });
        test("A new copy during contention prevents another clipboard read",()=>
        {
            uint sequence=10; int reads=0;
            Throws<OperationCanceledException>(()=>ClipboardPreparation.Read(10,()=>sequence,()=> { reads++; sequence=11; return Task.FromException<Snapshot>(Busy()); },default).GetAwaiter().GetResult());
            Require(reads==1);
        });
        test("Clipboard provider errors other than open contention are never retried",()=>
        {
            int reads=0;
            Throws<InvalidOperationException>(()=>ClipboardPreparation.Read(10,()=>10,()=> { reads++; return Task.FromException<Snapshot>(new InvalidOperationException()); },default).GetAwaiter().GetResult());
            Require(reads==1);
        });
        test("Canceling preparation interrupts the clipboard retry delay",()=>
        {
            using var cancel=new CancellationTokenSource(); int reads=0;
            Throws<OperationCanceledException>(()=>ClipboardPreparation.Read(10,()=>10,()=> { reads++; cancel.Cancel(); return Task.FromException<Snapshot>(Busy()); },cancel.Token).GetAwaiter().GetResult());
            Require(reads==1);
        });
        test("Canceling a completed clipboard read disposes its snapshot",()=>
        {
            using var cancel=new CancellationTokenSource(); var snapshot=new Snapshot();
            Throws<OperationCanceledException>(()=>ClipboardPreparation.Read(10,()=>10,()=> { cancel.Cancel(); return Task.FromResult(snapshot); },cancel.Token).GetAwaiter().GetResult());
            Require(snapshot.Disposed);
        });
        test("Publishing retries a refused clipboard write before editing",()=>
        {
            int writes=0;
            Require(ClipboardPreparation.Write(10,()=>10,()=>++writes==3,default).GetAwaiter().GetResult() && writes==3);
        });
        test("A new copy during publication prevents any further replacement write",()=>
        {
            uint sequence=10; int writes=0;
            bool published=ClipboardPreparation.Write(10,()=>sequence,()=> { writes++; sequence=11; return false; },default).GetAwaiter().GetResult();
            Require(!published && writes==1);
        });
        test("A locked publication reports failure after six refusals",()=>
        {
            int writes=0;
            Throws<InvalidOperationException>(()=>ClipboardPreparation.Write(10,()=>10,()=> { writes++; return false; },default).GetAwaiter().GetResult());
            Require(writes==6);
        });
        test("Publishing retries only clipboard-open exceptions",()=>
        {
            int writes=0;
            Require(ClipboardPreparation.Write(10,()=>10,()=> { if(++writes<3) throw Busy(); return true; },default).GetAwaiter().GetResult() && writes==3);
            writes=0;
            Throws<NotSupportedException>(()=>ClipboardPreparation.Write(10,()=>10,()=> { writes++; throw new NotSupportedException(); },default).GetAwaiter().GetResult());
            Require(writes==1);
        });
        test("Canceling a refused publication prevents retrying the write",()=>
        {
            using var cancel=new CancellationTokenSource(); int writes=0;
            Throws<OperationCanceledException>(()=>ClipboardPreparation.Write(10,()=>10,()=> { writes++; cancel.Cancel(); return false; },cancel.Token).GetAwaiter().GetResult());
            Require(writes==1);
        });
        test("A newer copy prevents publication without calling the writer",()=>
        {
            int writes=0;
            Require(!ClipboardPreparation.Write(10,()=>11,()=> { writes++; return true; },default).GetAwaiter().GetResult() && writes==0);
        });
    }
    private sealed class Snapshot:IDisposable { internal bool Disposed; public void Dispose()=>Disposed=true; }
}
