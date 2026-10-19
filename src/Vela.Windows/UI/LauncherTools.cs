using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Vela.Core;

namespace Vela.Windows.UI;

internal sealed record LauncherEditorRow(Launcher Item, int Index)
{
    public string Display => $"{Index + 1} · {Item.Name}" + (Item.IsFolder ? "  ›" : "");
}

public sealed partial class ToolWindow
{
    private void Launchers()
    {
        LauncherProfiles();
        Body.Children.Add(Ui.Text("Saved launchers", "SubtitleTextBlockStyle"));
        Body.Children.Add(Ui.Text("Group apps, files and websites into submenus. Each menu holds eight items, with up to three levels and 64 items per profile.", "VelaCaption"));
        var path = new List<int>();
        List<Launcher> Active() => controller.Settings.ActiveLauncherProfile == "Default"
            ? controller.Settings.Launchers
            : controller.Settings.LauncherProfiles.First(p => p.Name == controller.Settings.ActiveLauncherProfile).Items;
        List<Launcher> Level()
        {
            var items = Active();
            foreach (int index in path)
            {
                if (index >= items.Count || items[index].Children is not {} children) { path.Clear(); return Active(); }
                items = children;
            }
            return items;
        }
        var breadcrumb = Ui.Text("", "BodyStrongTextBlockStyle");
        var count = Ui.Text("", "VelaCaption");
        var back = new Button { Content = "Back" };
        Body.Children.Add(back); Body.Children.Add(breadcrumb); Body.Children.Add(count);
        var list = new ListView { DisplayMemberPath = "Display", MaxHeight = 220, SelectionMode = ListViewSelectionMode.Single };
        AutomationProperties.SetName(list, "Items in this launcher menu");
        Body.Children.Add(list);
        var kind = new ComboBox { Header = "Item type", ItemsSource = new[] { "Launcher", "Submenu" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var name = new TextBox { Header = "Name", MaxLength = 80 };
        var target = new TextBox { Header = "App, file, or website", PlaceholderText = @"C:\Path\App.exe or https://example.com" };
        back.Click += (_, _) => { if (path.Count > 0) { path.RemoveAt(path.Count - 1); Refresh(); } };
        var editor = Ui.Stack(12);
        editor.Children.Add(kind); editor.Children.Add(name); editor.Children.Add(target);
        Body.Children.Add(editor);
        kind.SelectionChanged += (_, _) => target.Visibility = kind.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is LauncherEditorRow row)
            {
                name.Text = row.Item.Name; target.Text = row.Item.Target;
                kind.SelectedIndex = row.Item.IsFolder ? 1 : 0; kind.IsEnabled = false;
            }
            else { name.Text = ""; target.Text = ""; kind.IsEnabled = true; kind.SelectedIndex = 0; }
        };
        void Refresh(int selected = -1)
        {
            Level(); // A settings import in another window may have replaced the menu.
            var labels = new List<string> { controller.Settings.ActiveLauncherProfile };
            var items = Active();
            foreach (int index in path) { labels.Add(items[index].Name); items = items[index].Children!; }
            breadcrumb.Text = string.Join(" › ", labels);
            count.Text = $"{items.Count} of 8 menu items · {LauncherTree.Count(Active())} of 64 profile items";
            back.IsEnabled = path.Count > 0;
            list.ItemsSource = items.Select((item, index) => new LauncherEditorRow(item, index)).ToList();
            list.SelectedIndex = selected;
            if (selected < 0) { name.Text = ""; target.Text = ""; kind.IsEnabled = true; kind.SelectedIndex = 0; }
        }
        void Persist(int selected = -1)
        {
            controller.Save(false); Refresh(selected); refreshCommands?.Invoke();
        }
        void OpenGroup()
        {
            if (list.SelectedItem is not LauncherEditorRow row || !row.Item.IsFolder) return;
            path.Add(row.Index); Refresh();
        }
        list.DoubleTapped += (_, _) => OpenGroup();
        Grid Actions(params Button[] buttons)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            foreach (var button in buttons)
            {
                grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
                Grid.SetColumn(button, grid.Children.Count); button.HorizontalAlignment = HorizontalAlignment.Stretch;
                grid.Children.Add(button);
            }
            return grid;
        }
        Body.Children.Add(Actions(
            Ui.Button("New item", () => { list.SelectedItem = null; name.Text = ""; target.Text = ""; kind.IsEnabled = true; kind.SelectedIndex = 0; name.Focus(FocusState.Programmatic); }),
            Ui.Button("Open submenu", OpenGroup)));
        Body.Children.Add(Ui.Button("Save item", () =>
        {
            string title = name.Text.Trim(), destination = target.Text.Trim();
            bool folder = kind.SelectedIndex == 1;
            if (title.Length == 0 || (!folder && !Launcher.IsAllowedTarget(destination)))
            { Message("Use a name and a full file path or HTTP website. Submenus need only a name.", true); return; }
            if (folder && path.Count >= LauncherTree.MaxLevels - 1)
            { Message("This is the deepest menu level. Add a launcher here, or go Back to create a submenu.", true); return; }
            var items = Level();
            int index = list.SelectedIndex;
            if (index < 0 && (items.Count >= LauncherTree.MaxSiblings || LauncherTree.Count(Active()) >= LauncherTree.MaxItems))
            { Message("This menu or profile is full. Remove an item before adding another.", true); return; }
            var children = folder ? (index >= 0 ? items[index].Children : []) : null;
            var item = new Launcher(title, folder ? "" : destination, children);
            if (index >= 0) items[index] = item;
            else { index = items.Count; items.Add(item); }
            Persist(index); Message("Launcher item saved.");
        }, true));
        void Move(int offset)
        {
            int index = list.SelectedIndex;
            var items = Level();
            int next = index + offset;
            if (index < 0 || next < 0 || next >= items.Count) return;
            (items[index], items[next]) = (items[next], items[index]); Persist(next);
        }
        var delete = AsyncButton("Delete", async () =>
        {
            if (list.SelectedItem is not LauncherEditorRow row) return;
            if (row.Item.Children is { Count: > 0 } children)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = Root.XamlRoot, Title = "Delete this submenu?",
                    Content = $"“{row.Item.Name}” and its {LauncherTree.Count(children)} contained items will be removed from this profile.",
                    PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close,
                    RequestedTheme = Root.RequestedTheme
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary || controller.Quitting) return;
            }
            var items = Level();
            if (!ReferenceEquals(items.ElementAtOrDefault(row.Index), row.Item)) { Refresh(); Message("The menu changed. Select the item again.", true); return; }
            items.RemoveAt(row.Index); Persist(); Message("Launcher item deleted.");
        });
        Body.Children.Add(Actions(Ui.Button("Move up", () => Move(-1)), Ui.Button("Move down", () => Move(1)), delete));
        Refresh();
    }
}
