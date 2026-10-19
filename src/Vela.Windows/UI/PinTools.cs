using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Vela.Windows.Services;
using WinRT.Interop;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private void PinImages()
    {
        Add("Keep a reference in view","Use Windows Snipping Tool (Win+Shift+S), then pin the copied image here. You can also choose an existing image. Each pin can stay above other windows and be resized.");
        Add("Session only","Up to six pins stay in memory until you close them, disable Image pins or exit Vela. Images are not saved or uploaded. Large images use a preview up to 2048 pixels per edge.");
        Body.Children.Add(AsyncButton("Pin clipboard image",async ()=>
        {
            if(conversion is not null) return;
            var content=Clipboard.GetContent();
            if(!content.Contains(StandardDataFormats.Bitmap)) { Message("Copy an image first, then try again.",true); return; }
            using var cancellation=new CancellationTokenSource(); conversion=cancellation;
            try
            {
                using var stream=await (await content.GetBitmapAsync().AsTask(cancellation.Token)).OpenReadAsync().AsTask(cancellation.Token);
                using var bitmap=await PinImageSource.Decode(stream,cancellation.Token);
                await controller.Pins.Open(bitmap,"Clipboard image",cancellation.Token);
            }
            finally { conversion=null; }
        }));
        Body.Children.Add(AsyncButton("Choose image to pin",async ()=>
        {
            if(conversion is not null) return;
            using var cancellation=new CancellationTokenSource(); conversion=cancellation;
            try
            {
                var picker=new FileOpenPicker();
                foreach(string extension in new[]{".png",".jpg",".jpeg",".bmp",".tif",".tiff",".webp"}) picker.FileTypeFilter.Add(extension);
                InitializeWithWindow.Initialize(picker,WindowStyle.Handle(this));
                var file=await picker.PickSingleFileAsync(); if(file is null) return;
                using var stream=await file.OpenReadAsync().AsTask(cancellation.Token);
                using var bitmap=await PinImageSource.Decode(stream,cancellation.Token);
                await controller.Pins.Open(bitmap,file.Name,cancellation.Token);
            }
            finally { conversion=null; }
        }));
        Body.Children.Add(Ui.Button("Close all image pins",controller.Pins.CloseAll));
    }
}
