using System.Diagnostics;
using Vela.Windows.Services;
using Windows.Storage;

internal static class OcrBatchChecks
{
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
    private static async Task Recognized(StorageFile file)
    { Require((await FileIO.ReadTextAsync(file)).Contains("Vela local text extraction",StringComparison.OrdinalIgnoreCase),"OCR output does not contain the generated fixture text."); }
    private static List<int> OwnedWorkers(string worker)
    {
        var ids=new List<int>();
        foreach(var process in Process.GetProcessesByName("Vela.OcrWorker"))
        {
            using(process)
            {
                try { if(!process.HasExited && string.Equals(process.MainModule?.FileName,worker,StringComparison.OrdinalIgnoreCase)) ids.Add(process.Id); }
                catch(InvalidOperationException) { }
                catch(System.ComponentModel.Win32Exception) { }
            }
        }
        return ids;
    }
    internal static async Task Run(StorageFolder root,StorageFile fixture)
    {
        var working=await root.CreateFolderAsync("ocr-batch");
        var first=await fixture.CopyAsync(await working.CreateFolderAsync("one"),"same.png");
        var second=await fixture.CopyAsync(await working.CreateFolderAsync("two"),"same.png");
        var bad=await working.CreateFileAsync("broken.png"); await FileIO.WriteTextAsync(bad,"Not an image");
        var empty=await working.CreateFileAsync("empty.jpg");
        var originals=new[]{first,second,bad,empty}.ToDictionary(file=>file.Path,file=>File.ReadAllBytes(file.Path));
        var outputs=await working.CreateFolderAsync("outputs");
        var prior=await MediaService.Ocr([first],outputs,_=>{},default);
        Require(prior.Completed==1 && !prior.HasFailures,prior.Message);
        var previous=(await outputs.GetFilesAsync()).Single(); var previousBytes=File.ReadAllBytes(previous.Path);
        var mixed=await MediaService.Ocr([bad,first,empty,second],outputs,_=>{},default);
        Require(mixed.Completed==2 && mixed.Total==4 && mixed.Failures.Count==2 && mixed.HasFailures
            && mixed.Message.Contains("broken.png") && mixed.Message.Contains("empty.jpg"),"A bad image stopped later OCR work or hid its failure.");
        Require((await outputs.GetFilesAsync()).Count==3 && previousBytes.SequenceEqual(File.ReadAllBytes(previous.Path)),"OCR overwrote previous text or retained an extra partial output.");
        foreach(var copy in await outputs.GetFilesAsync()) await Recognized(copy);
        Console.WriteLine("INFO OCR mixed batch: 2/4 completed, two named failures, three unique recognized text copies including prior output; prior bytes unchanged.");

        string worker=Path.Combine(AppContext.BaseDirectory,"Ocr","Vela.OcrWorker.exe");
        Require(OwnedWorkers(worker).Count==0,"Another worker is using this isolated test output.");
        var failedWorker=await working.CreateFolderAsync("worker-failure");
        var ownedDirectories=new List<string>(); int calls=0;
        var recovered=await MediaService.Ocr([first,second],failedWorker,_=>{},default,async (input,text,token)=>
        {
            ownedDirectories.Add(Path.GetDirectoryName(input)!);
            // Corrupt only Vela's prepared temporary PNG, never the selected source.
            if(++calls==1) await File.WriteAllTextAsync(input,"Generated worker failure",token);
            await MediaService.RunOcrWorker(worker,input,text,null,token);
        });
        Require(calls==2 && recovered.Completed==1 && recovered.Total==2 && recovered.Failures.Count==1
            && recovered.Failures[0].StartsWith("same.png:"),"A nonzero worker exit stopped the remaining batch or was counted as success.");
        await Recognized((await failedWorker.GetFilesAsync()).Single());
        Require(ownedDirectories.All(path=>!Directory.Exists(path)) && OwnedWorkers(worker).Count==0,"Failed OCR left its private temporary input or process.");
        Console.WriteLine("INFO OCR worker failure: actual native worker rejected a corrupted private PNG; later image recognized; both owned temporary directories and workers removed.");

        var canceledOutput=await working.CreateFolderAsync("cancel-worker");
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false; int? active=null;
            try
            {
                await MediaService.Ocr([first],canceledOutput,message=>
                {
                    if(!message.StartsWith("Extracting text in")) return;
                    var running=OwnedWorkers(worker); Require(running.Count==1,"Cancellation did not observe the started native worker.");
                    active=running[0]; stop.Cancel();
                },stop.Token);
            }
            catch(OperationCanceledException) { canceled=true; }
            Require(canceled && active is not null && OwnedWorkers(worker).Count==0 && (await canceledOutput.GetFilesAsync()).Count==0,"Canceled native OCR left a child process or text copy.");
            Console.WriteLine($"INFO OCR active cancellation: observed owned worker PID {active}, canceled it, waited for exit; zero destination copies.");
        }
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false;
            try { await MediaService.Ocr([first],canceledOutput,message=> { if(message.StartsWith("Writing text for")) stop.Cancel(); },stop.Token); }
            catch(OperationCanceledException) { canceled=true; }
            Require(canceled && (await canceledOutput.GetFilesAsync()).Count==0,"Cancellation after output creation retained unfinished text.");
        }
        var partial=await working.CreateFolderAsync("cancel-next");
        using(var stop=new CancellationTokenSource())
        {
            bool canceled=false; int started=0;
            try { await MediaService.Ocr([first,second],partial,message=> { if(message.StartsWith("Reading text in") && ++started==2) stop.Cancel(); },stop.Token); }
            catch(OperationCanceledException) { canceled=true; }
            Require(canceled && started==2 && (await partial.GetFilesAsync()).Count==1,"Canceling the next image lost completed text or retained a partial copy.");
            await Recognized((await partial.GetFilesAsync()).Single());
        }
        bool preCanceled=false;
        try { await MediaService.Ocr([first],canceledOutput,_=>{},new CancellationToken(true)); }
        catch(OperationCanceledException) { preCanceled=true; }
        Require(preCanceled && (await canceledOutput.GetFilesAsync()).Count==0,"Pre-canceled OCR created an output.");
        var none=await MediaService.Ocr([],canceledOutput,_=>{},default);
        Require(none.Completed==0 && none.Total==0 && !none.HasFailures,"An empty batch reported a failure or output.");
        var failures=await MediaService.Ocr(Enumerable.Repeat(bad,10).ToArray(),canceledOutput,_=>{},default);
        Require(failures.Completed==0 && failures.Failures.Count==10 && failures.Message.Contains("2 more image(s) failed.") && (await canceledOutput.GetFilesAsync()).Count==0,"All-failure reporting lost counts or wrote output.");
        foreach(var source in new[]{first,second,bad,empty})
        {
            Require(originals[source.Path].SequenceEqual(File.ReadAllBytes(source.Path)),"OCR changed a selected original.");
            using var exclusive=File.Open(source.Path,FileMode.Open,FileAccess.Read,FileShare.None);
        }
        Require(OwnedWorkers(worker).Count==0,"An isolated OCR worker remained after the batch.");
        Console.WriteLine("INFO OCR cancellation: current partial text removed, earlier recognized text kept, pre-cancel/empty/all-failure cases correct; all selected originals byte-identical and handles released.");
    }
}
