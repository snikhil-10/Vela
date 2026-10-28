namespace Vela.Core;

// Only a paired Caps Lock press/release may become Escape. Resetting a
// gesture for pause, exclusion or disable never completes a tap.
public sealed class SuperKeyState
{
    public bool CapsHeld { get; private set; }
    public bool ChordHeld { get; private set; }
    public void PressCaps()=>CapsHeld=true;
    public bool BeginChord()
    {
        if(!CapsHeld || ChordHeld) return false;
        ChordHeld=true; return true;
    }
    public (bool Escape,bool ReleaseModifiers) ReleaseCaps()
    {
        var release=(Escape:CapsHeld && !ChordHeld,ReleaseModifiers:ChordHeld);
        Reset(); return release;
    }
    public bool Reset()
    {
        bool release=ChordHeld;
        CapsHeld=false; ChordHeld=false; return release;
    }
}
