using Vela.Core;

internal static class AudioBudgetChecks
{
    private static void Require(bool value) { if(!value) throw new InvalidOperationException("Audio budget contract failed."); }
    public static IEnumerable<(string Name,Action Check)> All()
    {
        yield return ("Generous audio budgets preserve the preset and never raise its bitrate",()=>
        {
            Require(AudioSizeBudget.Plan(10*1024*1024,TimeSpan.FromMinutes(1),192000)==192000);
            Require(AudioSizeBudget.Plan(10*1024*1024,TimeSpan.FromMinutes(1),128000)==128000);
        });
        yield return ("Audio plans use supported Windows mono/stereo AAC rates",()=>
        {
            Require(AudioSizeBudget.Plan(17000,TimeSpan.FromSeconds(1),192000)==96000);
            Require(AudioSizeBudget.Plan(21000,TimeSpan.FromSeconds(1),192000)==128000);
            Require(AudioSizeBudget.Plan(25000,TimeSpan.FromSeconds(1),192000)==160000);
            Require(AudioSizeBudget.Plan(30000,TimeSpan.FromSeconds(1),192000)==192000);
        });
        yield return ("Audio planning includes duration and reserves container overhead",()=>
        {
            const long limit=1024*1024; const int seconds=60;
            var rate=AudioSizeBudget.Plan(limit,TimeSpan.FromSeconds(seconds),192000);
            Require(rate==96000 && rate.Value*seconds/8d<=limit-Math.Max(4096,limit/10));
        });
        yield return ("Impossible audio limits stop before encoding instead of claiming success",()=>
        {
            Require(AudioSizeBudget.Plan(4096,TimeSpan.FromSeconds(1),192000) is null);
            Require(AudioSizeBudget.Plan(65536,TimeSpan.FromMinutes(1),192000) is null);
        });
        yield return ("Audio retries reduce supported rates using measured oversize",()=>
        {
            Require(AudioSizeBudget.Lower(192000,100000,100001)==160000);
            Require(AudioSizeBudget.Lower(160000,100000,100001)==128000);
            Require(AudioSizeBudget.Lower(128000,100000,100001)==96000);
            Require(AudioSizeBudget.Lower(192000,100000,200000)==96000);
        });
        yield return ("Audio retries stop at the Windows AAC minimum without increasing quality",()=>
        { Require(AudioSizeBudget.Lower(96000,100000,200000) is null); Require(AudioSizeBudget.Plan(30000,TimeSpan.FromSeconds(1),100000)==96000); });
        yield return ("Large audio limits and tiny durations cannot overflow supported rates",()=>
        {
            Require(AudioSizeBudget.Plan(AudioSizeBudget.MaximumBytes,TimeSpan.FromTicks(1),uint.MaxValue)==192000);
            Require(AudioSizeBudget.Lower(uint.MaxValue,1,long.MaxValue)==96000);
        });
        yield return ("Invalid audio bounds, durations, rates and measurements fail explicitly",()=>
        {
            foreach(Action action in new Action[]{
                ()=>AudioSizeBudget.Plan(0,TimeSpan.FromSeconds(1),192000),
                ()=>AudioSizeBudget.Plan(AudioSizeBudget.MaximumBytes+1,TimeSpan.FromSeconds(1),192000),
                ()=>AudioSizeBudget.Plan(100000,TimeSpan.Zero,192000),
                ()=>AudioSizeBudget.Plan(100000,TimeSpan.FromSeconds(-1),192000),
                ()=>AudioSizeBudget.Plan(100000,TimeSpan.FromSeconds(1),95999),
                ()=>AudioSizeBudget.Lower(192000,100000,100000),
                ()=>AudioSizeBudget.Lower(95999,100000,200000)})
            { bool rejected=false; try { action(); } catch(ArgumentOutOfRangeException) { rejected=true; } Require(rejected); }
        });
    }
}
