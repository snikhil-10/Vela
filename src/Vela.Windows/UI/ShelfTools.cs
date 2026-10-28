using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Vela.Windows.Services;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private List<ShelfItem> draggedShelf=[];
    private CancellationTokenSource? shelfAdds;
    private ShellDropFeedback? shelfDragFeedback;
    private CancellationToken shelfDragSession;
    private void Shelf()
    {
        shelfAdds=new();
        var lifetime=shelfAdds.Token;
        Closed+=(_,_)=> { shelfAdds.Cancel(); shelfAdds.Dispose(); shelfDragFeedback?.Dispose(); shelfDragFeedback=null; };
        Body.Children.Add(Ui.Text("Park files, folders, text and links here, then drag them into another app. Originals stay where they are; the shelf lasts for this Vela session.","VelaCaption"));
        string[] modes=["manual","taskbar","velaIcon","tray"];
        var target=new ComboBox { Header="Show a drop target over",ItemsSource=new[]{"Only this shelf window","The entire taskbar","Vela's taskbar icon","Vela's tray icon"},SelectedIndex=Array.IndexOf(modes,controller.Settings.ShelfDropTarget),HorizontalAlignment=HorizontalAlignment.Stretch };
        target.SelectionChanged+=(_,_)=>
        { if(target.SelectedIndex>=0) { controller.Settings.ShelfDropTarget=modes[target.SelectedIndex]; controller.Save(false); } };
        Body.Children.Add(target);
        Body.Children.Add(Ui.Text("Pick up a file to highlight the chosen drop location. Release directly on the highlighted icon or taskbar area; the shelf opens after the drop. Canceling hides the highlight. Taskbar-icon mode keeps a Vela button available. Tray mode needs a visible Vela tray icon; you can show it in Windows Taskbar settings.","VelaCaption"));
        Add("Remove after a successful drag","Remove entries after a confirmed transfer. Canceled or unconfirmed file copies keep their entries; some apps do not report completion. Original files are kept.",Ui.Toggle(controller.Settings.ShelfRemoveAfterDrop,on=> { controller.Settings.ShelfRemoveAfterDrop=on; controller.Save(false); }));
        var search=new TextBox { Header="Find shelf items",PlaceholderText="Search names, paths, text or links",MaxLength=256 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(search,"Find shelf items");
        Body.Children.Add(search);
        Body.Children.Add(Ui.Text("All words must match. Use quotes for a phrase. Search only filters this session's shelf; hidden entries stay in the shelf and are not selected.","VelaCaption"));
        var results=Ui.Text("","VelaCaption");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(results,"Shelf search results");
        Body.Children.Add(results);
        var list=new ListView { DisplayMemberPath="Name",MinHeight=180,MaxHeight=300,CanDragItems=true,SelectionMode=ListViewSelectionMode.Extended };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(list,"Shelf items; use Control or Shift to select several");
        void Filter()
        {
            var selected=list.SelectedItems.OfType<ShelfItem>().ToHashSet();
            var visible=controller.ShelfData.Find(search.Text);
            list.ItemsSource=visible;
            foreach(var item in visible) if(selected.Contains(item)) list.SelectedItems.Add(item);
            int total=controller.Shelf.Count;
            results.Text=total==0 ? "The shelf is empty. Drop items here or choose Add clipboard."
                : visible.Count==0 ? $"No matches. Clear search to see all {total} shelf entries."
                : $"Showing {visible.Count} of {total} shelf entries.";
        }
        search.TextChanged+=(_,_)=>Filter();
        System.Collections.Specialized.NotifyCollectionChangedEventHandler changed=(_,_)=>Filter();
        controller.Shelf.CollectionChanged+=changed;
        Closed+=(_,_)=>controller.Shelf.CollectionChanged-=changed;
        Filter();
        list.DragStarting+=(_,e)=>e.AllowedOperations=DataPackageOperation.Copy;
        list.DragItemsStarting+=(_,e)=>
        {
            try
            {
                shelfDragFeedback?.Dispose(); shelfDragFeedback=null;
                shelfDragSession=controller.ShelfData.PendingToken;
                draggedShelf=e.Items.OfType<ShelfItem>().ToList(); ShelfService.Fill(e.Data,draggedShelf);
                if(controller.Settings.ShelfRemoveAfterDrop && draggedShelf.Any(item=>item.File is not null))
                {
                    // WinUI owns pointer capture, selection and the drag image.
                    // Streamed file extraction supplies final async completion.
                    shelfDragFeedback=ShellDropFeedback.Capture(e.Data,draggedShelf.Select(item=>item.File!).ToArray());
                    shelfDragFeedback?.Trace("WinUI file drag; items="+draggedShelf.Count);
                }
            }
            catch(Exception ex) { e.Cancel=true; draggedShelf.Clear(); Message(ex.Message,true); }
        };
        list.DragItemsCompleted+=async (_,e)=>
        {
            var dragged=draggedShelf.ToArray(); var result=e.DropResult; var feedback=shelfDragFeedback; var session=shelfDragSession;
            draggedShelf.Clear(); shelfDragFeedback=null;
            using(feedback)
            {
                if(!controller.ShelfEnabled || !controller.Settings.ShelfRemoveAfterDrop || result!=DataPackageOperation.Copy || lifetime.IsCancellationRequested || session.IsCancellationRequested) return;
                using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime,session);
                try
                {
                    bool confirmed=dragged.All(item=>item.File is null) || feedback is not null && await feedback.ConfirmCopy(cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if(!controller.ShelfEnabled) return;
                    controller.ShelfData.CompleteDrag(dragged,result,controller.Settings.ShelfRemoveAfterDrop,confirmed);
                    if(!confirmed && controller.Settings.ShelfRemoveAfterDrop)
                    { Message("The destination did not confirm a completed copy. Shelf entries were kept."); Status.Severity=InfoBarSeverity.Informational; }
                }
                catch(OperationCanceledException) { }
            }
        };
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
