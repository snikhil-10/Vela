namespace Vela.Core;

// Coordinates are DIPs relative to the wheel centre. The UI owns pointer capture.
public sealed class RadialGesture
{
    public const double DeadZone = 72, OuterRadius = 208;
    public bool Active { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    private int count;
    private bool moved;

    public void Begin(int itemCount)
    {
        if (itemCount is < 0 or > LauncherTree.MaxSiblings) throw new ArgumentOutOfRangeException(nameof(itemCount));
        count = itemCount; moved = false; SelectedIndex = -1; Active = true;
    }
    public int Move(double x, double y, bool inside = true)
    {
        SelectedIndex = -1;
        if (!Active) return -1;
        if (!double.IsFinite(x) || !double.IsFinite(y)) { moved = true; return -1; }
        double distance = Math.Sqrt(x * x + y * y);
        if (distance > DeadZone || !inside) moved = true;
        if (!inside || distance <= DeadZone || distance > OuterRadius || count == 0) return -1;
        double turn = (Math.Atan2(y, x) + Math.PI / 2 + Math.Tau) % Math.Tau;
        SelectedIndex = (int)Math.Floor(turn / (Math.Tau / count) + .5) % count;
        return SelectedIndex;
    }
    public (int Index, bool CenterClick) End(double x, double y, bool inside = true)
    {
        if (!Active) return (-1, false);
        int index = Move(x, y, inside);
        bool centerClick = !moved && inside;
        Cancel();
        return (index, centerClick);
    }
    public void Cancel() { Active = false; SelectedIndex = -1; moved = false; }
}
