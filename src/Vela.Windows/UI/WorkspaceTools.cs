using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Vela.Core;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private void PlainPaste()
    {
        Add("Use in another app",$"Focus the destination and press {controller.Settings.PlainPasteShortcut}. Vela pastes plain text, then restores all original clipboard formats.");
        Add("Clipboard ownership","If you copy something new or lock Windows during the paste, Vela keeps that newer clipboard state. Content is never added to Windows history by this tool.");
        Add("Desktop compatibility","The destination must support ordinary Ctrl+V. Elevated apps, secure screens, and apps which delay clipboard requests may need their own paste command.");
        Body.Children.Add(Ui.Button("Shortcut settings",()=> { controller.ShowShortcutSettings(); Close(); }));
    }
    private void WindowSearch()
    {
        var search=new TextBox { Header="Window title or app",PlaceholderText="Find an open window" };
        var monitor=new ComboBox { Header="Display",ItemsSource=new[]{"All displays"}.Concat(WindowWorkspace.Monitors().Select(m=>m.Name)).ToArray(),SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
        var list=new ListView { DisplayMemberPath="Display",IsItemClickEnabled=true,MinHeight=180,MaxHeight=420 };
        var windows=controller.Workspaces.Windows();
        void Refresh()
        {
            var displays=WindowWorkspace.Monitors();
            list.ItemsSource=windows.Where(w=>w.Display.Contains(search.Text,StringComparison.OrdinalIgnoreCase) && (monitor.SelectedIndex<=0 || monitor.SelectedIndex>displays.Count || w.Monitor.Id==displays[monitor.SelectedIndex-1].Id)).ToList();
            if(list.Items.Count>0) list.SelectedIndex=0;
        }
        void Open() { if(list.SelectedItem is OpenWindow selected) { WindowWorkspace.Focus(selected); Close(); } }
        search.TextChanged+=(_,_)=>Refresh(); monitor.SelectionChanged+=(_,_)=>Refresh();
        search.KeyDown+=(_,e)=>
        {
            if(e.Key==global::Windows.System.VirtualKey.Enter) { e.Handled=true; Open(); }
            if(e.Key==global::Windows.System.VirtualKey.Down && list.Items.Count>0) { e.Handled=true; list.SelectedIndex=Math.Min(list.Items.Count-1,list.SelectedIndex+1); }
            if(e.Key==global::Windows.System.VirtualKey.Up && list.Items.Count>0) { e.Handled=true; list.SelectedIndex=Math.Max(0,list.SelectedIndex-1); }
        };
        list.ItemClick+=(_,e)=> { if(e.ClickedItem is OpenWindow selected) { WindowWorkspace.Focus(selected); Close(); } };
        Body.Children.Add(search); Body.Children.Add(monitor); Body.Children.Add(list);
        Body.Children.Add(Ui.Button("Refresh",()=> { windows=controller.Workspaces.Windows(); Refresh(); }));
        Body.Children.Add(Ui.Button("Switch to selected window",Open,true));
        Body.Children.Add(Ui.Button("Keep selected window on top",()=> { if(list.SelectedItem is OpenWindow selected) Message(controller.Workspaces.Pin(selected)); }));
        Body.Children.Add(Ui.Button("Restore selected window layer",()=> { if(list.SelectedItem is OpenWindow selected) Message(controller.Workspaces.Unpin(selected)); }));
        Add("Floating notes","Keep an existing Notepad or other desktop window above your work. Vela restores only layers it changed; pinning ends when this feature is disabled or Vela exits.");
        Refresh(); search.Loaded+=(_,_)=>search.Focus(FocusState.Programmatic);
    }
    private void SavedLayouts()
    {
        var name=new TextBox { Header="Workspace name",PlaceholderText="Writing, research, or editing",MaxLength=80 };
        var list=new ListView { DisplayMemberPath="Name",MaxHeight=240 };
        void Refresh()=>list.ItemsSource=controller.Settings.WindowLayouts.ToList();
        Refresh(); Body.Children.Add(name); Body.Children.Add(list);
        list.SelectionChanged+=(_,_)=> { if(list.SelectedItem is WindowLayout layout) name.Text=layout.Name; };
        Body.Children.Add(Ui.Button("Save current positions",()=>
        {
            string title=name.Text.Trim(); if(title.Length==0) { Message("Enter a workspace name.",true); return; }
            var layout=controller.Workspaces.Capture(title);
            if(layout.Windows.Count==0) { Message("Open some desktop windows first.",true); return; }
            var old=controller.Settings.WindowLayouts.FirstOrDefault(l=>l.Name.Equals(title,StringComparison.OrdinalIgnoreCase));
            if(old is null && controller.Settings.WindowLayouts.Count>=32) { Message("Up to 32 workspaces are supported. Delete a workspace before adding another.",true); return; }
            if(old is not null) controller.Settings.WindowLayouts.Remove(old);
            controller.Settings.WindowLayouts.Add(layout); controller.Save(false); Refresh(); Message($"Saved {layout.Windows.Count} window positions.");
        },true));
        Body.Children.Add(Ui.Button("Restore workspace",()=> { if(list.SelectedItem is WindowLayout layout) Message(controller.Workspaces.Restore(layout)); }));
        Body.Children.Add(Ui.Button("Undo last restore",()=>Message(controller.Workspaces.Undo())));
        Body.Children.Add(Ui.Button("Delete workspace",()=> { if(list.SelectedItem is WindowLayout layout) { controller.Settings.WindowLayouts.Remove(layout); controller.Save(false); Refresh(); } }));
        Add("Custom positions","Saves the windows already open on this desktop, without recreating Snap. Positions adapt to the current work area; a missing display falls back to the window's current display. Some elevated or fixed-size windows cannot be moved.");
    }
    private void LauncherProfiles()
    {
        void RefreshEditor()
        {
            Body.Children.Clear();
            if(FeatureId=="radialMenu") Launchers(); else Commands();
        }
        var profiles=new ComboBox { Header="Launcher profile",ItemsSource=new[]{"Default"}.Concat(controller.Settings.LauncherProfiles.Select(p=>p.Name)).ToArray(),SelectedItem=controller.Settings.ActiveLauncherProfile,HorizontalAlignment=HorizontalAlignment.Stretch };
        profiles.SelectionChanged+=(_,_)=>
        {
            if(profiles.SelectedItem is not string name) return;
            controller.Settings.ActiveLauncherProfile=name; controller.Save(false);
            RefreshEditor();
        };
        Body.Children.Add(profiles);
        var nameBox=new TextBox { Header="New profile",PlaceholderText="Work or personal",MaxLength=80 }; Body.Children.Add(nameBox);
        Body.Children.Add(Ui.Button("Create profile",()=>
        {
            string name=nameBox.Text.Trim();
            if(name.Length==0 || name.Equals("Default",StringComparison.OrdinalIgnoreCase) || controller.Settings.LauncherProfiles.Any(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase))) { Message("Choose a unique profile name.",true); return; }
            if(controller.Settings.LauncherProfiles.Count>=24) { Message("Up to 24 launcher profiles are supported.",true); return; }
            controller.Settings.LauncherProfiles.Add(new(name,[])); controller.Settings.ActiveLauncherProfile=name; controller.Save(false); RefreshEditor();
        }));
        if(controller.Settings.ActiveLauncherProfile!="Default") Body.Children.Add(Ui.Button("Delete this profile",()=>
        { controller.Settings.LauncherProfiles.RemoveAll(p=>p.Name==controller.Settings.ActiveLauncherProfile); controller.Settings.ActiveLauncherProfile="Default"; controller.Save(false); RefreshEditor(); }));
    }
}
