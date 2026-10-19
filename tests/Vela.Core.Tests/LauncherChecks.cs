using Vela.Core;

internal static class LauncherChecks
{
    private static Launcher Link(string name) => new(name, "https://example.com/" + Uri.EscapeDataString(name));
    private static void Require(bool condition) { if (!condition) throw new Exception("Launcher assertion failed."); }
    public static IEnumerable<(string Name, Action Check)> All()
    {
        yield return ("Radial gestures select every sector clockwise from the top for one through eight entries", () =>
        {
            for (int count = 1; count <= 8; count++)
                for (int index = 0; index < count; index++)
                {
                    double angle = -Math.PI / 2 + index * Math.Tau / count;
                    var gesture = new RadialGesture(); gesture.Begin(count);
                    Require(gesture.End(Math.Cos(angle) * 150, Math.Sin(angle) * 150) == (index, false));
                }
        });
        yield return ("Radial centre clicks and return-to-centre cancellation do not launch a sector", () =>
        {
            var gesture = new RadialGesture(); gesture.Begin(8);
            Require(gesture.Move(0, -RadialGesture.DeadZone) == -1);
            Require(gesture.End(0, 0) == (-1, true));
            gesture.Begin(8); Require(gesture.Move(0, -150) == 0);
            Require(gesture.End(0, 0) == (-1, false));
        });
        yield return ("Radial release uses its final sector and rejects outside or invalid coordinates", () =>
        {
            var gesture = new RadialGesture(); gesture.Begin(4); gesture.Move(0, -150);
            Require(gesture.End(150, 0) == (1, false));
            foreach (var point in new[] { (0d, -209d, true), (150d, 0d, false), (double.NaN, 0d, true), (double.PositiveInfinity, 0d, true) })
            {
                gesture.Begin(4); gesture.Move(0, -150);
                Require(gesture.End(point.Item1, point.Item2, point.Item3) == (-1, false));
            }
        });
        yield return ("Canceled radial capture cannot launch on a later release or leak into the next drag", () =>
        {
            var gesture = new RadialGesture(); gesture.Begin(8); gesture.Move(150, 0); gesture.Cancel();
            Require(!gesture.Active && gesture.End(150, 0) == (-1, false));
            gesture.Begin(8); Require(gesture.End(0, 0) == (-1, true));
            gesture.Begin(0); Require(gesture.End(150, 0) == (-1, false));
        });
        yield return ("Radial sector boundaries select the adjacent item without swallowing the top wrap", () =>
        {
            var gesture = new RadialGesture();
            foreach(int count in new[]{3,5,8})
            {
                double half = Math.PI / count;
                foreach(var (angle, expected) in new[]{(-Math.PI/2+half-.001,0),(-Math.PI/2+half+.001,1),(-Math.PI/2-half-.001,count-1),(-Math.PI/2-half+.001,0)})
                {
                    gesture.Begin(count);
                    Require(gesture.End(Math.Cos(angle)*150,Math.Sin(angle)*150).Index == expected);
                }
            }
        });
        yield return ("Legacy flat launchers remain runnable after a settings round trip", () =>
        {
            var settings = SettingsStore.Deserialize("""{"Launchers":[{"Name":"Old app","Target":"https://example.com"}]}""");
            var item = SettingsStore.Deserialize(SettingsStore.Serialize(settings)).Launchers.Single();
            Require(item.Name == "Old app" && !item.IsFolder && item.Target == "https://example.com");
        });
        yield return ("Nested and empty submenus survive export without executable folder targets", () =>
        {
            var settings = new AppSettings { Launchers = [new("Work", "javascript:ignored", [new("Deep", "", [Link("Notes")]), new("Empty", "", [])])] };
            var restored = SettingsStore.Deserialize(SettingsStore.Serialize(settings));
            Require(restored.Launchers[0].Target == "" && restored.Launchers[0].Children![1].IsFolder);
            Require(LauncherTree.Leaves(restored.Launchers).Single().Path == "Work › Deep › Notes");
        });
        yield return ("Imported submenus reject unsafe and missing leaf targets at every level", () =>
        {
            var items = LauncherTree.Normalize([new("Group", "", [new("Bad", "javascript:alert(1)"), new("Missing", ""), Link("Good"), null!]), null!]);
            Require(items.Single().Children!.Single().Name == "Good");
        });
        yield return ("Menu sibling and depth limits retain valid leaves in order", () =>
        {
            var items = LauncherTree.Normalize([
                new("Root", "", [new("Child", "", [new("Too deep", "", [Link("Hidden")]), Link("Visible")])]),
                .. Enumerable.Range(1, 12).Select(n => Link(n.ToString()))]);
            Require(items.Count == 8 && items[7].Name == "7");
            Require(items[0].Children![0].Children!.Single().Name == "Visible");
        });
        yield return ("Total imported menu size is bounded independently for each profile", () =>
        {
            var many = Enumerable.Range(0, 8).Select(n => new Launcher("Folder " + n, "", Enumerable.Range(0, 8).Select(m => Link(n + "-" + m)).ToList())).ToList();
            var settings = new AppSettings { Launchers = many, LauncherProfiles = [new("Work", many)] };
            settings.Normalize();
            Require(LauncherTree.Count(settings.Launchers) == 64 && LauncherTree.Count(settings.LauncherProfiles[0].Items) == 64);
            settings.Launchers[0].Children!.Clear();
            Require(settings.LauncherProfiles[0].Items[0].Children!.Count == 8);
        });
        yield return ("Normalization does not mutate source menu names or child collections", () =>
        {
            var source = new Launcher("  Group  ", "ignored", [Link("Keep"), new("Bad", "javascript:x")]);
            var clean = LauncherTree.Normalize([source]).Single();
            Require(source.Name == "  Group  " && source.Children!.Count == 2);
            Require(clean.Name == "Group" && clean.Children!.Count == 1 && !ReferenceEquals(source.Children, clean.Children));
        });
        yield return ("Navigation restores the parent selection through nested and empty menus", () =>
        {
            var menu = new LauncherNavigation([Link("One"), new("Group", "", [Link("Two"), new("Empty", "", [])])]);
            Require(menu.Enter(1) && menu.Folders.Single() == "Group" && menu.SelectedIndex == 0);
            Require(menu.Enter(1) && menu.Items.Count == 0 && menu.SelectedIndex == -1);
            menu.Move(1); Require(menu.SelectedIndex == -1);
            Require(menu.Back() && menu.SelectedIndex == 1 && menu.Back() && menu.SelectedIndex == 1 && !menu.CanGoBack);
            Require(!menu.Back() && !menu.Enter(0) && !menu.Enter(99));
        });
        yield return ("Keyboard selection wraps and ignores invalid direct choices", () =>
        {
            var menu = new LauncherNavigation([Link("One"), Link("Two"), Link("Three")]);
            menu.Move(-1); Require(menu.SelectedIndex == 2);
            menu.Move(1); Require(menu.SelectedIndex == 0);
            menu.Select(1); menu.Select(20); Require(menu.SelectedIndex == 1);
            menu.Move(int.MinValue); Require(menu.SelectedIndex is >= 0 and < 3);
        });
        yield return ("Search discovers nested leaves with group context and skips empty groups", () =>
        {
            var items = LauncherTree.Normalize([Link("First"), new("Research", "", [Link("Paper"), new("Empty", "", [])])]);
            var leaves = LauncherTree.Leaves(items).ToArray();
            Require(leaves.Length == 2 && leaves[0].Path == "First" && leaves[1].Path == "Research › Paper");
            Require(leaves.All(l => !l.Item.IsFolder));
        });
        yield return ("Menu normalization bounds cyclic in-memory groups without modifying them", () =>
        {
            var children = new List<Launcher>();
            var folder = new Launcher("Cycle", "", children); children.Add(folder); children.Add(Link("Safe"));
            var clean = LauncherTree.Normalize([folder]);
            Require(LauncherTree.Count(clean) < 10 && children.Count == 2);
            Require(LauncherTree.Leaves(clean).Count() == 2);
        });
    }
}
