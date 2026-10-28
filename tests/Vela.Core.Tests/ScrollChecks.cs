using Vela.Core;

internal static class ScrollChecks
{
    internal static void Run(Action<string,Action> test)
    {
        static void Require(bool value) { if(!value) throw new Exception("Queued wheel delivery differs from the gesture."); }
        static long Drain(SmoothScrollBuffer scroll)
        {
            long total=0; int steps=0;
            while(scroll.Pending)
            {
                int delta=scroll.TakeStep();
                Require(delta is >=short.MinValue and <=short.MaxValue && delta!=0 && ++steps<100000);
                total+=delta;
            }
            Require(scroll.TakeStep()==0); return total;
        }
        test("Smooth wheel direction and scaled total survive interpolation",()=>
        {
            foreach(int delta in new[]{-360,-120,120,360})
                foreach(double scale in new[]{.25,1,3})
                { var scroll=new SmoothScrollBuffer(); scroll.Add(1,delta,scale); Require(Drain(scroll)==(long)(delta*scale)); }
        });
        test("Moving a wheel gesture to another window cannot transfer the old window's queued distance",()=>
        {
            var scroll=new SmoothScrollBuffer(); scroll.Add(1,120,1);
            Require(scroll.TakeStep()>0); scroll.Add(2,-120,1);
            Require(scroll.Target==2 && Drain(scroll)==-120);
        });
        test("Rapid same-window wheel input coalesces without losing ticks",()=>
        {
            var scroll=new SmoothScrollBuffer(); scroll.Add(1,120,1);
            int first=scroll.TakeStep(); scroll.Add(1,240,1);
            Require(first+Drain(scroll)==360);
        });
        test("A large wheel burst cannot wrap the signed Windows message delta",()=>
        {
            foreach(int delta in new[]{-32768,32767})
            { var scroll=new SmoothScrollBuffer(); for(int i=0;i<100;i++) scroll.Add(1,delta,3); Require(Drain(scroll)==100L*delta*3); }
        });
        test("Fractional wheel multipliers keep their cumulative scaled distance",()=>
        {
            var scroll=new SmoothScrollBuffer(); long total=0;
            for(int i=0;i<5;i++) { scroll.Add(1,120,.26); total+=Drain(scroll); }
            Require(total==156);
        });
        test("Opposite wheel input cancels queued motion and clearing starts a fresh gesture",()=>
        {
            var scroll=new SmoothScrollBuffer(); scroll.Add(1,120,1); scroll.Add(1,-120,1);
            Require(!scroll.Pending); scroll.Add(1,120,1); scroll.Clear();
            Require(!scroll.Pending && scroll.Target==0 && scroll.TakeStep()==0);
            scroll.Add(2,120,1); Require(Drain(scroll)==120);
        });
        test("Invalid wheel destinations or scaling cannot leave pending motion",()=>
        {
            foreach(double scale in new[]{double.NaN,double.PositiveInfinity,0,-1})
            { var scroll=new SmoothScrollBuffer(); scroll.Add(1,120,1); scroll.Add(1,120,scale); Require(!scroll.Pending && scroll.Target==0); }
            var missing=new SmoothScrollBuffer(); missing.Add(0,120,1); Require(!missing.Pending);
        });
    }
}
