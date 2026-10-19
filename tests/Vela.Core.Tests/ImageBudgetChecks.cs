using Vela.Core;

internal static class ImageBudgetChecks
{
    private static void Require(bool value) { if(!value) throw new Exception("JPEG budget assertion failed."); }
    public static IEnumerable<(string Name,Action Check)> All()
    {
        yield return ("JPEG size selection retains a fitting preferred quality without extra encoding",()=>
        {
            var calls=new List<int>();
            var choice=JpegSizeBudget.Choose(1000,90,(q,_)=> { calls.Add(q); return Task.FromResult(1000L); },default).GetAwaiter().GetResult();
            Require(choice is { Quality:90,Bytes:1000,Attempts:1 } && calls.SequenceEqual([90]));
        });
        yield return ("JPEG size search chooses a tested fitting quality with at most nine encodes",()=>
        {
            var calls=new List<int>();
            var choice=JpegSizeBudget.Choose(5730,100,(q,_)=> { calls.Add(q); return Task.FromResult(q*100L); },default).GetAwaiter().GetResult();
            Require(choice is { Quality:57,Bytes:5700 } && choice.Value.Attempts<=9 && calls.Distinct().Count()==calls.Count && calls.All(q=>q is >=10 and <=100));
        });
        yield return ("An impossible JPEG size cap stops after the two endpoint probes",()=>
        {
            var calls=new List<int>();
            var choice=JpegSizeBudget.Choose(999,90,(q,_)=> { calls.Add(q); return Task.FromResult(q*100L); },default).GetAwaiter().GetResult();
            Require(choice is null && calls.SequenceEqual([90,10]));
        });
        yield return ("Minimum JPEG quality is probed only once and cannot be lowered further",()=>
        {
            int calls=0;
            var choice=JpegSizeBudget.Choose(1000,10,(q,_)=> { calls++; return Task.FromResult(1001L); },default).GetAwaiter().GetResult();
            Require(choice is null && calls==1);
        });
        yield return ("JPEG search honors cancellation before and during an encoder measurement",()=>
        {
            using var stop=new CancellationTokenSource(); stop.Cancel(); int calls=0;
            try { JpegSizeBudget.Choose(1000,90,(_,_)=> { calls++; return Task.FromResult(1000L); },stop.Token).GetAwaiter().GetResult(); throw new Exception("Expected cancellation"); }
            catch(OperationCanceledException) { Require(calls==0); }
            using var during=new CancellationTokenSource();
            try { JpegSizeBudget.Choose(1000,90,(_,_)=> { calls++; during.Cancel(); return Task.FromResult(1000L); },during.Token).GetAwaiter().GetResult(); throw new Exception("Expected cancellation"); }
            catch(OperationCanceledException) { Require(calls==1); }
        });
        yield return ("JPEG search rejects invalid limits, quality and empty encoder output",()=>
        {
            foreach(var (limit,quality) in new[]{(0L,90),(1000L,9),(1000L,101)})
            {
                bool rejected=false;
                try { JpegSizeBudget.Choose(limit,quality,(_,_)=>Task.FromResult(100L),default).GetAwaiter().GetResult(); } catch(ArgumentOutOfRangeException) { rejected=true; }
                Require(rejected);
            }
            bool emptyRejected=false;
            try { JpegSizeBudget.Choose(1000,90,(_,_)=>Task.FromResult(0L),default).GetAwaiter().GetResult(); } catch(InvalidDataException) { emptyRejected=true; }
            Require(emptyRejected);
        });
    }
}
