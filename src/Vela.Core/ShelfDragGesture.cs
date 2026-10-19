namespace Vela.Core;

public sealed class ShelfDragGesture
{
    private (int X,int Y)? origin;
    private bool beganOnTaskbar,canceled;
    public bool Update(bool leftDown,int x,int y,bool onTaskbar,bool movingWindow,bool escapeDown)
    {
        if(!leftDown) { Reset(); return false; }
        if(movingWindow || escapeDown) canceled=true;
        if(canceled) return false;
        if(origin is null) { origin=(x,y); beganOnTaskbar=onTaskbar; return false; }
        return !beganOnTaskbar && Math.Abs((long)x-origin.Value.X)+Math.Abs((long)y-origin.Value.Y)>=8;
    }
    public void Reset() { origin=null; beganOnTaskbar=false; canceled=false; }
}
