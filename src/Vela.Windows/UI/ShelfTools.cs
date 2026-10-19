using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private List<ShelfItem> draggedShelf=[];
    private CancellationTokenSource? shelfAdds;
    private void Shelf()
    {
        shelfAdds=new();
        Closed+=(_,_)=> { shelfAdds.Cancel(); shelfAdds.Dispose(); };
        Body.Children.Add(Ui.Text("Park files, folders, text and links here, then drag them into another app. Originals stay where they are; the shelf lasts for this Vela session.","VelaCaption"));
        string[] modes=["manual","taskbar","velaIcon","tray"];
        var target=new ComboBox { Header="Show a drop target over",ItemsSource=new[]{"Only this shelf window","The entire taskbar","Vela's taskbar icon","Vela's tray icon"},SelectedIndex=Array.IndexOf(modes,controller.Settings.ShelfDropTarget),HorizontalAlignment=HorizontalAlignment.Stretch };
        target.SelectionChanged+=(_,_)=>
        { if(target.SelectedIndex>=0) { controller.Settings.ShelfDropTarget=modes[target.SelectedIndex]; controller.Save(false); } };
        Body.Children.Add(target);
        Body.Children.Add(Ui.Text("Pick up a file to highlight the chosen drop location. Release directly on the highlighted icon or taskbar area; the shelf opens after the drop. Canceling hides the highlight. Taskbar-icon mode keeps a Vela button available. Tray mode needs a visible Vela tray icon; you can show it in Windows Taskbar settings.","VelaCaption"));
        Add("Remove after a successful drag","Remove transferred entries from the shelf after another app accepts them. Canceled drags keep their entries. Original files are kept.",Ui.Toggle(controller.Settings.ShelfRemoveAfterDrop,on=> { controller.Settings.ShelfRemoveAfterDrop=on; controller.Save(false); }));
        var list=new ListView { ItemsSource=controller.Shelf,DisplayMemberPath="Name",MinHeight=180,MaxHeight=300,CanDragItems=true,SelectionMode=ListViewSelectionMode.Extended };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(list,"Shelf items; use Control or Shift to select several");
        list.DragStarting+=(_,e)=>e.AllowedOperations=DataPackageOperation.Copy;
        list.DragItemsStarting+=(_,e)=>
        {
            try { draggedShelf=e.Items.OfType<ShelfItem>().ToList(); ShelfService.Fill(e.Data,draggedShelf); }
            catch(Exception ex) { e.Cancel=true; draggedShelf.Clear(); Message(ex.Message,true); }
        };
        list.DragItemsCompleted+=(_,e)=>
        { controller.ShelfData.CompleteDrag(draggedShelf,e.DropResult,controller.Settings.ShelfRemoveAfterDrop); draggedShelf.Clear(); };
        Body.Children.Add(new Border { Style=Ui.Style("VelaCard"),Child=list });
        Root.AllowDrop=true;
        Root.DragOver+=(_,e)=>e.AcceptedOperation=draggedShelf.Count==0 && ShelfService.Accepts(e.DataView) ? DataPackageOperation.Copy : DataPackageOperation.None;
        Root.Drop+=async (_,e)=>
        {
            var deferral=e.GetDeferral();
            try
            {
                if(draggedShelf.Count>0 || !controller.ShelfEnabled) { e.AcceptedOperation=DataPackageOperation.None; return; }
                int count=await controller.ShelfData.Add(e.DataView,shelfAdds.Token); e.AcceptedOperation=DataPackageOperation.Copy;
                Message($"Added {count} {(count==1?"item":"items")}. Originals were kept.");
            }
            catch(OperationCanceledException) { e.AcceptedOperation=DataPackageOperation.None; }
            catch(Exception ex) { e.AcceptedOperation=DataPackageOperation.None; Message(ex.Message,true); }
            finally { deferral.Complete(); }
        };
        Body.Children.Add(AsyncButton("Add clipboard",async ()=>Message($"Added {await controller.ShelfData.Add(Clipboard.GetContent(),shelfAdds.Token)} items.")));
        Body.Children.Add(Ui.Button("Copy selected",()=>
        { ClipboardService.WritePackage(ShelfService.Package(list.SelectedItems.OfType<ShelfItem>().ToArray())); Message("Selection copied. Shelf entries and originals were kept."); }));
        Body.Children.Add(Ui.Button("Remove selected",()=>
        { foreach(var item in list.SelectedItems.OfType<ShelfItem>().ToArray()) controller.Shelf.Remove(item); }));
        Body.Children.Add(Ui.Text("Control-click or Shift-click to select several files. Drag files together or text together; use separate transfers for a mixed selection. A moved or missing original cannot be dragged out.","VelaCaption"));
    }
}
