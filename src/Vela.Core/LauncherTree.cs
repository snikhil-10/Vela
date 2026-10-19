namespace Vela.Core;

public static class LauncherTree
{
    public const int MaxSiblings = 8;
    public const int MaxLevels = 3;
    public const int MaxItems = 64;

    public static List<Launcher> Normalize(IEnumerable<Launcher>? source)
    {
        int remaining = MaxItems;
        return Level(source, 0);

        List<Launcher> Level(IEnumerable<Launcher>? items, int depth)
        {
            var result = new List<Launcher>();
            foreach (var item in items ?? [])
            {
                if (result.Count == MaxSiblings || remaining == 0) break;
                if (item is null || string.IsNullOrWhiteSpace(item.Name)) continue;
                string name = item.Name.Trim();
                if (name.Length > 80) name = name[..80];
                if (item.IsFolder)
                {
                    if (depth >= MaxLevels - 1) continue;
                    remaining--;
                    result.Add(new(name, "", Level(item.Children, depth + 1)));
                }
                else if (Launcher.IsAllowedTarget(item.Target))
                {
                    remaining--;
                    result.Add(new(name, item.Target.Trim()));
                }
            }
            return result;
        }
    }

    public static int Count(IEnumerable<Launcher> items) =>
        items.Sum(item => 1 + (item.Children is {} children ? Count(children) : 0));

    // Callers use normalized settings; groups are searchable context, never executable targets.
    public static IEnumerable<(Launcher Item, string Path)> Leaves(IEnumerable<Launcher> items, string prefix = "")
    {
        foreach (var item in items)
        {
            string path = prefix.Length == 0 ? item.Name : prefix + " › " + item.Name;
            if (item.Children is {} children)
            {
                foreach (var leaf in Leaves(children, path)) yield return leaf;
            }
            else yield return (item, path);
        }
    }
}

public sealed class LauncherNavigation
{
    private readonly List<IReadOnlyList<Launcher>> levels;
    private readonly List<string> folders = [];
    private readonly List<int> parentSelections = [];
    public IReadOnlyList<Launcher> Items => levels[^1];
    public IReadOnlyList<string> Folders => folders;
    public int SelectedIndex { get; private set; }
    public bool CanGoBack => levels.Count > 1;

    public LauncherNavigation(IEnumerable<Launcher> items)
    {
        levels = [LauncherTree.Normalize(items)];
        SelectedIndex = Items.Count == 0 ? -1 : 0;
    }

    public void Select(int index)
    {
        if (index >= 0 && index < Items.Count) SelectedIndex = index;
    }

    public void Move(int offset)
    {
        if (Items.Count == 0) return;
        SelectedIndex = ((SelectedIndex + offset % Items.Count) % Items.Count + Items.Count) % Items.Count;
    }

    public bool Enter(int index)
    {
        if (index < 0 || index >= Items.Count || Items[index].Children is not {} children) return false;
        parentSelections.Add(index);
        folders.Add(Items[index].Name);
        levels.Add(children);
        SelectedIndex = children.Count == 0 ? -1 : 0;
        return true;
    }

    public bool Back()
    {
        if (!CanGoBack) return false;
        levels.RemoveAt(levels.Count - 1);
        folders.RemoveAt(folders.Count - 1);
        SelectedIndex = parentSelections[^1];
        parentSelections.RemoveAt(parentSelections.Count - 1);
        return true;
    }
}
