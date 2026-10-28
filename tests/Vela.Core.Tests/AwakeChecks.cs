using Vela.Core;

internal static class AwakeChecks
{
    internal static void Run(Action<string,Action> test)
    {
        static void Require(bool value) { if(!value) throw new Exception("Wake request exceeded its duration, power policy or service lifetime."); }
        static (AwakeState,Clock) Active() { var clock=new Clock(); var state=new AwakeState(clock); state.Start(); return (state,clock); }
        test("Keep awake stays off until an active timer or explicit rule requests it",()=>
        {
            var (state,_)=Active(); Require(!state.Decide(false,null,false,false).Requested);
            state.Stop(); Require(!state.Decide(false,true,true,true).Requested);
        });
        test("A Keep awake timer expires at its exact monotonic deadline",()=>
        {
            var (state,clock)=Active(); state.Hold(TimeSpan.FromMinutes(1));
            clock.Advance(TimeSpan.FromSeconds(59)); Require(state.Remaining==TimeSpan.FromSeconds(1));
            clock.Advance(TimeSpan.FromSeconds(1)); Require(state.Remaining==TimeSpan.Zero && !state.Decide(false,true,false,false).Requested);
        });
        test("Forward and backward calendar adjustments cannot change a Keep awake duration",()=>
        {
            var (state,clock)=Active(); state.Hold(TimeSpan.FromHours(1));
            clock.Calendar+=TimeSpan.FromDays(10); clock.Advance(TimeSpan.FromMinutes(20)); Require(state.Remaining==TimeSpan.FromMinutes(40));
            clock.Calendar-=TimeSpan.FromDays(20); clock.Advance(TimeSpan.FromMinutes(40)); Require(!state.Decide(false,true,false,false).Requested);
        });
        test("Disabling and re-enabling Keep awake cannot resurrect an old timer",()=>
        {
            var (state,_)=Active(); state.Hold(TimeSpan.FromHours(2)); state.Stop(); state.Start();
            Require(state.Remaining==TimeSpan.Zero && !state.Decide(false,true,false,false).Requested);
        });
        test("A stopped Keep awake service ignores late hold actions",()=>
        {
            var state=new AwakeState(); Require(!state.Hold(TimeSpan.FromMinutes(1)) && state.Remaining==TimeSpan.Zero);
        });
        test("Invalid Keep awake durations leave an existing timer intact",()=>
        {
            var (state,_)=Active(); state.Hold(TimeSpan.FromMinutes(1));
            foreach(var invalid in new[]{TimeSpan.Zero,TimeSpan.FromTicks(-1),TimeSpan.FromDays(1)+TimeSpan.FromTicks(1)})
            {
                bool rejected=false; try { state.Hold(invalid); } catch(ArgumentOutOfRangeException) { rejected=true; }
                Require(rejected && state.Remaining==TimeSpan.FromMinutes(1));
            }
        });
        test("Starting a new Keep awake duration replaces the previous deadline",()=>
        {
            var (state,clock)=Active(); state.Hold(TimeSpan.FromMinutes(1)); clock.Advance(TimeSpan.FromSeconds(30));
            state.Hold(TimeSpan.FromMinutes(2)); clock.Advance(TimeSpan.FromSeconds(30)); Require(state.Remaining==TimeSpan.FromSeconds(90));
        });
        test("Stopping only the timer preserves explicit application and display rules",()=>
        {
            var (state,_)=Active(); state.Hold(TimeSpan.FromMinutes(1)); state.Release();
            Require(state.Decide(false,true,true,false).Reason==AwakeReason.Application);
            Require(state.Decide(false,true,false,true).Reason==AwakeReason.ExternalDisplay);
        });
        test("AC-only pauses on battery and resumes only the original remaining interval",()=>
        {
            var (state,clock)=Active(); state.Hold(TimeSpan.FromMinutes(2));
            Require(state.Decide(true,false,false,false).Reason==AwakeReason.Battery);
            clock.Advance(TimeSpan.FromMinutes(1)); var plugged=state.Decide(true,true,false,false);
            Require(plugged.Requested && plugged.Remaining==TimeSpan.FromMinutes(1));
        });
        test("Unknown power status cannot bypass the AC-only restriction",()=>
        {
            var (state,_)=Active(); state.Hold(TimeSpan.FromMinutes(1));
            var decision=state.Decide(true,null,true,true); Require(!decision.Requested && decision.Reason==AwakeReason.PowerUnavailable);
        });
        test("An unrestricted request works without a reported AC state",()=>
        {
            var (state,_)=Active(); Require(state.Decide(false,null,true,false).Requested);
        });
        test("A timer expiring while on battery cannot restart when AC returns",()=>
        {
            var (state,clock)=Active(); state.Hold(TimeSpan.FromMinutes(1)); clock.Advance(TimeSpan.FromMinutes(2));
            Require(!state.Decide(true,false,false,false).Requested && !state.Decide(true,true,false,false).Requested);
        });
        test("Removing rule matches releases Keep awake after its timer ends",()=>
        {
            var (state,_)=Active(); Require(state.Decide(false,true,true,true).Requested);
            Require(!state.Decide(false,true,false,false).Requested);
        });
        test("Sub-millisecond timers retain precision and a reset monotonic clock cancels safely",()=>
        {
            var (state,clock)=Active(); state.Hold(TimeSpan.FromTicks(10)); clock.Advance(TimeSpan.FromTicks(9)); Require(state.Remaining==TimeSpan.FromTicks(1));
            clock.Advance(TimeSpan.FromTicks(1)); Require(state.Remaining==TimeSpan.Zero);
            state.Hold(TimeSpan.FromMinutes(1)); clock.Timestamp--; Require(state.Remaining==TimeSpan.Zero);
        });
    }
    private sealed class Clock : TimeProvider
    {
        internal long Timestamp=1000;
        internal DateTimeOffset Calendar=new(2026,10,22,0,0,0,TimeSpan.Zero);
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>Timestamp;
        public override DateTimeOffset GetUtcNow()=>Calendar;
        internal void Advance(TimeSpan value)=>Timestamp+=value.Ticks;
    }
}
