using Vela.Core;

internal static class AlertChecks
{
    public static void Run(Action<string,Action> test)
    {
        static void Require(bool condition) { if(!condition) throw new Exception("Alert fired outside its sustained interval or cooldown."); }
        static void HighUntil(SustainedAlertGate gate,long start,long end)
        { for(long now=start;now<=end;now+=5_000) Require(!gate.Observe(95,90,now)); }
        test("Six five-second samples do not prove thirty seconds of high usage",()=>
        { var gate=new SustainedAlertGate(); HighUntil(gate,0,25_000); Require(gate.Observe(95,90,30_000)); });
        test("Repeated reads of one sample cannot age into an alert",()=>
        { var gate=new SustainedAlertGate(); for(int i=0;i<100;i++) Require(!gate.Observe(95,90,0)); });
        test("A low reading restarts the sustained observation",()=>
        { var gate=new SustainedAlertGate(); HighUntil(gate,0,25_000); Require(!gate.Observe(80,90,30_000)); HighUntil(gate,35_000,60_000); Require(gate.Observe(95,90,65_000)); });
        test("An unavailable reading restarts the sustained observation",()=>
        { var gate=new SustainedAlertGate(); HighUntil(gate,0,25_000); Require(!gate.Observe(null,90,30_000)); HighUntil(gate,35_000,60_000); Require(gate.Observe(95,90,65_000)); });
        test("A long sampling gap cannot turn a single burst into a sustained alert",()=>
        { var gate=new SustainedAlertGate(); HighUntil(gate,0,25_000); HighUntil(gate,100_000,125_000); Require(gate.Observe(95,90,130_000)); });
        test("Changing a threshold requires a fresh sustained interval",()=>
        { var gate=new SustainedAlertGate(); HighUntil(gate,0,25_000); Require(!gate.Observe(95,92,30_000)); for(long now=35_000;now<60_000;now+=5_000) Require(!gate.Observe(95,92,now)); Require(gate.Observe(95,92,60_000)); });
        test("Resetting observations preserves the fifteen-minute notification cooldown",()=>
        { var gate=new SustainedAlertGate(); HighUntil(gate,0,25_000); Require(gate.Observe(95,90,30_000)); gate.ResetObservation(); HighUntil(gate,35_000,925_000); Require(gate.Observe(95,90,930_000)); });
        test("Identically named GPU devices maintain separate alert intervals",()=>
        { var gates=new Dictionary<string,SustainedAlertGate> { ["uuid-a"]=new(), ["uuid-b"]=new() }; HighUntil(gates["uuid-a"],0,25_000); HighUntil(gates["uuid-b"],10_000,25_000); Require(gates["uuid-a"].Observe(95,90,30_000)); Require(!gates["uuid-b"].Observe(95,90,30_000)); Require(!gates["uuid-b"].Observe(95,90,35_000)); Require(gates["uuid-b"].Observe(95,90,40_000)); });
        test("Non-finite readings cannot sustain an alert",()=>
        { var gate=new SustainedAlertGate(); HighUntil(gate,0,25_000); Require(!gate.Observe(double.NaN,90,30_000)); Require(!gate.Observe(double.PositiveInfinity,90,35_000)); HighUntil(gate,40_000,65_000); Require(gate.Observe(95,90,70_000)); });
        test("Low disk warnings use a strict threshold and bounded cooldown",()=>
        { var gate=new SustainedAlertGate(holdMilliseconds:0,below:true); Require(!gate.Observe(10,10,0)); Require(gate.Observe(9,10,5_000)); Require(!gate.Observe(8,10,10_000)); gate.ResetObservation(); Require(!gate.Observe(7,10,900_000)); Require(gate.Observe(7,10,905_000)); });
    }
}
