using Vela.Windows.Services;

internal static class DownloadSafetyChecks
{
    internal static Task Run(string root)
    {
        string folder=Path.Combine(root,"download-safeguards"); Directory.CreateDirectory(folder);
        string sibling=folder+"-outside"; Directory.CreateDirectory(sibling);
        string changed=Path.Combine(folder,"changed.txt"),outside=Path.Combine(sibling,"outside.txt");
        File.WriteAllText(changed,"original fixture"); File.WriteAllText(outside,"outside fixture");
        foreach(string path in new[]{changed,outside})
        { File.SetCreationTimeUtc(path,DateTime.UtcNow.AddDays(-100)); File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-100)); }
        var reviewed=DownloadReview.Scan(folder,30,default); Require(reviewed.Files.Count==1,"Review escaped its selected tree.");
        var info=new FileInfo(outside); var outsideSelection=new ReviewFile(info.FullName,info.Length,info.LastWriteTimeUtc,info.CreationTimeUtc);
        File.WriteAllText(changed,"changed after review with different bytes");
        string result=DownloadReview.Recycle(folder,[reviewed.Files.Single(),outsideSelection]);
        Require(result.StartsWith("Sent 0 files") && result.Contains("2 changed") && File.Exists(changed) && File.Exists(outside),"Changed or out-of-root file was recycled.");
        Require(File.ReadAllText(changed)=="changed after review with different bytes" && File.ReadAllText(outside)=="outside fixture","Skipped file content changed.");
        bool canceled=false;
        try { DownloadReview.Scan(folder,30,new CancellationToken(true)); }
        catch(OperationCanceledException) { canceled=true; }
        Require(canceled,"Pre-canceled review continued.");
        Console.WriteLine("INFO Download review: changed metadata and sibling-prefix path rejected; pre-canceled scan stopped; zero files deleted or recycled.");
        return Task.CompletedTask;
    }
    private static void Require(bool value,string reason) { if(!value) throw new InvalidOperationException(reason); }
}
