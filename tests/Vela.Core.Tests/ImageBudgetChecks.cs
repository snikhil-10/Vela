using Vela.Core;

internal static class ImageBudgetChecks
{
    private static void Require(bool value) { if(!value) throw new Exception("Image budget assertion failed."); }
    public static IEnumerable<(string Name,Action Check)> All()
    {
        yield return ("PNG/BMP budgets keep fitting dimensions and never enlarge small sources",()=>
        {
            foreach(uint edge in new uint[]{16,64,1024})
            {
                var calls=new List<uint>();
                var choice=ImageSizeBudget.Choose(1000,edge,(candidate,_)=> { calls.Add(candidate); return Task.FromResult(1000L); },default).GetAwaiter().GetResult();
                Require(choice is not null && choice.Value.LongestEdge==edge && choice.Value.Attempts==1 && calls.SequenceEqual([edge]));
            }
        });
        yield return ("PNG/BMP dimension search returns an actually measured fitting size within twelve attempts",()=>
        {
            var calls=new Dictionary<uint,long>();
            var choice=ImageSizeBudget.Choose(160_000,1024,(edge,_)=> { calls.Add(edge,edge*edge); return Task.FromResult(calls[edge]); },default).GetAwaiter().GetResult();
            Require(choice is { LongestEdge:400,Bytes:160_000 } && choice.Value.Attempts==calls.Count && calls.Count<=ImageSizeBudget.MaximumAttempts);
            var huge=ImageSizeBudget.Choose(10_000,uint.MaxValue,(edge,_)=>Task.FromResult((long)edge),default).GetAwaiter().GetResult();
            Require(huge is not null && huge.Value.Bytes<=10_000 && huge.Value.Attempts==ImageSizeBudget.MaximumAttempts);
        });
        yield return ("PNG/BMP budgets reject an unfit minimum without degrading below it",()=>
        {
            foreach(uint edge in new uint[]{16,64,1000})
            {
                var calls=new List<uint>();
                var choice=ImageSizeBudget.Choose(1,edge,(candidate,_)=> { calls.Add(candidate); return Task.FromResult(2L); },default).GetAwaiter().GetResult();
                Require(choice is null && calls.Count==(edge<=64?1:2) && calls.All(e=>e>=Math.Min(edge,64)));
            }
        });
        yield return ("PNG/BMP nonmonotonic measurements retain a tested fit without a maximality claim",()=>
        {
            var calls=new Dictionary<uint,long>();
            var choice=ImageSizeBudget.Choose(1000,1000,(edge,_)=> { calls.Add(edge,edge==64?100:edge%3==0?900:2000); return Task.FromResult(calls[edge]); },default).GetAwaiter().GetResult();
            Require(choice is not null && choice.Value.Bytes<=1000 && calls[choice.Value.LongestEdge]==choice.Value.Bytes && calls.Count<=12);
        });
        yield return ("PNG/BMP dimension selection cancels before or during measurement",()=>
        {
            foreach(bool preCanceled in new[]{true,false})
            {
                using var stop=new CancellationTokenSource(); if(preCanceled) stop.Cancel(); int calls=0;
                try { ImageSizeBudget.Choose(1000,512,(_,_)=> { calls++; stop.Cancel(); return Task.FromResult(100L); },stop.Token).GetAwaiter().GetResult(); throw new Exception("Expected cancellation"); }
                catch(OperationCanceledException) { Require(calls==(preCanceled?0:1)); }
            }
        });
        yield return ("PNG/BMP dimension selection rejects invalid bounds and empty encoded data",()=>
        {
            foreach(var (limit,edge) in new[]{(0L,100u),(1000L,0u)})
            {
                bool rejected=false;
                try { ImageSizeBudget.Choose(limit,edge,(_,_)=>Task.FromResult(100L),default).GetAwaiter().GetResult(); } catch(ArgumentOutOfRangeException) { rejected=true; }
                Require(rejected);
            }
            bool empty=false;
            try { ImageSizeBudget.Choose(1000,512,(_,_)=>Task.FromResult(0L),default).GetAwaiter().GetResult(); } catch(InvalidDataException) { empty=true; }
            Require(empty);
        });
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
