using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Vela.Windows.Services;

internal sealed class ShelfItem(string name,string? text,IStorageItem? file)
{
    public string Name { get; }=name;
    public string? Text { get; }=text;
    public IStorageItem? File { get; }=file;
    public string Detail=>File?.Path??Text??"";
}

internal sealed class ShelfService
{
    internal const int Capacity=256;
    private CancellationTokenSource additions=new();
    public ObservableCollection<ShelfItem> Items { get; }=[];
    public static bool Accepts(DataPackageView data) => data.Contains(StandardDataFormats.StorageItems)
        || data.Contains(StandardDataFormats.Text) || data.Contains(StandardDataFormats.WebLink);
    public async Task<int> Add(DataPackageView data,CancellationToken cancellationToken=default)
    {
        using var pending=CancellationTokenSource.CreateLinkedTokenSource(additions.Token,cancellationToken);
        var token=pending.Token; token.ThrowIfCancellationRequested();
        var incoming=new List<ShelfItem>();
        if(data.Contains(StandardDataFormats.StorageItems))
            foreach(var item in await data.GetStorageItemsAsync().AsTask(token)) incoming.Add(new(item.Name,null,item));
        else
        {
            string text=data.Contains(StandardDataFormats.WebLink) ? (await data.GetWebLinkAsync().AsTask(token)).AbsoluteUri
                : data.Contains(StandardDataFormats.Text) ? await data.GetTextAsync().AsTask(token) : "";
            if(text.Length>262144) throw new InvalidOperationException("That text is too large for the shelf (256 KiB limit).");
            if(text.Length>0) incoming.Add(new(text.Length>80?text[..80]+"…":text,text,null));
        }
        token.ThrowIfCancellationRequested();
        if(incoming.Count==0) throw new InvalidOperationException("The shelf accepts files, folders, links and text.");
        if(Items.Count+incoming.Count>Capacity) throw new InvalidOperationException("The shelf holds up to 256 items. Remove some before adding more.");
        foreach(var item in incoming) Items.Add(item);
        return incoming.Count;
    }
    public void CancelPendingAdds()
    {
        var previous=additions; additions=new(); previous.Cancel(); previous.Dispose();
    }
    public static bool Available(ShelfItem item) => item.File is null || string.IsNullOrEmpty(item.File.Path)
        || System.IO.File.Exists(item.File.Path) || Directory.Exists(item.File.Path);
    public static DataPackage Package(IReadOnlyList<ShelfItem> items)
    {
        var package=new DataPackage { RequestedOperation=DataPackageOperation.Copy };
        Fill(package,items); return package;
    }
    public static void Fill(DataPackage package,IReadOnlyList<ShelfItem> items)
    {
        if(items.Count==0) throw new InvalidOperationException("Select at least one shelf item.");
        if(items.Any(item=>!Available(item))) throw new InvalidOperationException("An original file has moved or is unavailable. Remove its shelf entry or restore the original first.");
        package.RequestedOperation=DataPackageOperation.Copy;
        if(items.All(i=>i.File is not null)) package.SetStorageItems(items.Select(i=>i.File!).ToArray());
        else if(items.All(i=>i.File is null)) package.SetText(string.Join(Environment.NewLine,items.Select(i=>i.Text)));
        else throw new InvalidOperationException("Drag files together or text together. Mixed file and text selections need separate transfers.");
    }
    public void CompleteDrag(IEnumerable<ShelfItem> dragged,DataPackageOperation result,bool remove)
    { if(remove && result==DataPackageOperation.Copy) foreach(var item in dragged.ToArray()) Items.Remove(item); }
}
