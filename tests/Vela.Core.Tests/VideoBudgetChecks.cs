using Vela.Core;

internal static class VideoBudgetChecks
{
    private static void Require(bool value) { if(!value) throw new Exception("Video budget assertion failed."); }
    public static IEnumerable<(string Name,Action Check)> All()
    {
        yield return ("A generous video budget retains preset rates without increasing quality",()=>
        {
            Require(VideoSizeBudget.Plan(100*1024*1024,TimeSpan.FromSeconds(10),4000000,192000)==new VideoBitrates(4000000,192000));
        });
        yield return ("Video budgets reserve container overhead and include audio in the duration estimate",()=>
        {
            const long limit=1024*1024; const int seconds=30;
            var plan=VideoSizeBudget.Plan(limit,TimeSpan.FromSeconds(seconds),4000000,192000);
            Require(plan is {} value && value.Video>=64000 && value.Audio is 96000 or 128000 or 160000 or 192000
                && ((double)value.Video+value.Audio)*seconds/8<=limit-Math.Max(4096,limit/10));
        });
        yield return ("Silent video budgets do not reserve a fictional audio stream",()=>
        {
            var plan=VideoSizeBudget.Plan(65536,TimeSpan.FromSeconds(5),1000000,0);
            Require(plan is { Audio:0 } && plan.Value.Video>64000);
        });
        yield return ("Impossible video budgets are rejected before encoding",()=>
        {
            Require(VideoSizeBudget.Plan(4096,TimeSpan.FromSeconds(1),4000000,192000) is null);
            Require(VideoSizeBudget.Plan(65536,TimeSpan.FromHours(1),4000000,192000) is null);
        });
        yield return ("Video budget retry reduces measured oversize without raising either rate",()=>
        {
            var old=new VideoBitrates(1000000,128000);
            var lower=VideoSizeBudget.Lower(old,100000,200000);
            Require(lower is {} value && value.Video<old.Video && value.Audio<old.Audio && value.Video>=64000 && value.Audio>=96000);
        });
        yield return ("Video retry stops at minimum rates and preserves an absent audio stream",()=>
        {
            Require(VideoSizeBudget.Lower(new(64000,96000),100000,200000) is null);
            Require(VideoSizeBudget.Lower(new(64000,0),100000,200000) is null);
            Require(VideoSizeBudget.Lower(new(128000,128000),100000,200000) is { Video:64000,Audio:96000 });
        });
        yield return ("Large video sizes and very short durations cannot overflow bitrate planning",()=>
        {
            Require(VideoSizeBudget.Plan(VideoSizeBudget.MaximumBytes,TimeSpan.FromTicks(1),uint.MaxValue,uint.MaxValue)==new VideoBitrates(uint.MaxValue,192000));
            Require(VideoSizeBudget.Lower(new(uint.MaxValue,192000),1,long.MaxValue)==new VideoBitrates(64000,96000));
        });
        yield return ("Invalid video budgets, durations and encoder measurements are rejected",()=>
        {
            foreach(Action invalid in new Action[]{
                ()=>VideoSizeBudget.Plan(0,TimeSpan.FromSeconds(1),1000000,128000),
                ()=>VideoSizeBudget.Plan(VideoSizeBudget.MaximumBytes+1,TimeSpan.FromSeconds(1),1000000,128000),
                ()=>VideoSizeBudget.Plan(100000,TimeSpan.Zero,1000000,128000),
                ()=>VideoSizeBudget.Plan(100000,TimeSpan.FromSeconds(1),0,128000),
                ()=>VideoSizeBudget.Plan(100000,TimeSpan.FromSeconds(1),1000000,32000),
                ()=>VideoSizeBudget.Lower(new(1000000,128000),100000,100000)})
            {
                bool rejected=false; try { invalid(); } catch(ArgumentOutOfRangeException) { rejected=true; }
                Require(rejected);
            }
        });
    }
}
