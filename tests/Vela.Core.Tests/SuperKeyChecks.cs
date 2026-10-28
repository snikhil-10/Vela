using Vela.Core;

internal static class SuperKeyChecks
{
    internal static void Run(Action<string,Action> test)
    {
        static void Require(bool value) { if(!value) throw new Exception("Caps Lock gesture ownership differs from the paired input."); }
        test("A paired Caps Lock tap emits one Escape",()=>
        {
            var key=new SuperKeyState(); key.PressCaps();
            Require(key.ReleaseCaps()==(true,false) && !key.CapsHeld && !key.ChordHeld);
            Require(key.ReleaseCaps()==(false,false));
        });
        test("A Super key chord starts once and releases its modifiers without Escape",()=>
        {
            var key=new SuperKeyState(); key.PressCaps();
            Require(key.BeginChord() && !key.BeginChord());
            Require(key.ReleaseCaps()==(false,true) && !key.Reset());
        });
        test("An unmatched Caps Lock release cannot emit Escape after pause or enable",()=>
        {
            var key=new SuperKeyState(); Require(key.ReleaseCaps()==(false,false));
        });
        test("Caps Lock auto-repeat cannot reset an active Super key chord",()=>
        {
            var key=new SuperKeyState(); key.PressCaps(); Require(key.BeginChord());
            key.PressCaps(); Require(!key.BeginChord() && key.ReleaseCaps()==(false,true));
        });
        test("Canceling a Caps Lock tap cannot complete it on a later release",()=>
        {
            var key=new SuperKeyState(); key.PressCaps(); Require(!key.Reset());
            Require(key.ReleaseCaps()==(false,false));
            key.PressCaps(); Require(key.ReleaseCaps()==(true,false));
        });
        test("Canceling a Super chord releases ownership once and never synthesizes Escape",()=>
        {
            var key=new SuperKeyState(); key.PressCaps(); key.BeginChord();
            Require(key.Reset() && !key.Reset() && key.ReleaseCaps()==(false,false));
        });
        test("Ordinary keys cannot start a Super chord without Caps Lock",()=>
        {
            var key=new SuperKeyState(); Require(!key.BeginChord() && !key.ChordHeld);
            key.PressCaps(); key.ReleaseCaps(); Require(!key.BeginChord());
        });
    }
}
