using Microsoft.VisualBasic.FileIO;

namespace Vela.Windows.Services;

internal sealed record ReviewFile(string Path,long Bytes,DateTime LastWrite,DateTime Created)
{ public string Display=>$"{System.IO.Path.GetFileName(Path)} · {Bytes/1024.0:F0} KB · {LastWrite:d}\n{Path}"; }
internal sealed record ReviewResult(IReadOnlyList<ReviewFile> Files,int Skipped,bool Limited);

internal static class DownloadReview
{
    public static ReviewResult Scan(string root,int days,CancellationToken token)
    {
        root=Path.GetFullPath(root);
        if(!Directory.Exists(root) || (File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("Choose a local folder, not a linked folder.");
        var files=new List<ReviewFile>(); var pending=new Stack<string>(); pending.Push(root); int skipped=0,visited=0;
        DateTime cutoff=DateTime.UtcNow.AddDays(-Math.Clamp(days,1,3650));
        while(pending.TryPop(out string? directory))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach(string path in Directory.EnumerateFileSystemEntries(directory))
                {
                    token.ThrowIfCancellationRequested(); if(++visited>20000) return new(files.OrderBy(f=>f.LastWrite).ToList(),skipped,true);
                    try
                    {
                        var attributes=File.GetAttributes(path);
                        if((attributes&(FileAttributes.ReparsePoint|FileAttributes.System))!=0) { skipped++; continue; }
                        if((attributes&FileAttributes.Directory)!=0) { pending.Push(path); continue; }
                        var info=new FileInfo(path);
                        // A newly downloaded copy of an old document should not be treated as stale.
                        if(info.LastWriteTimeUtc>cutoff || info.CreationTimeUtc>cutoff) continue;
                        files.Add(new(info.FullName,info.Length,info.LastWriteTimeUtc,info.CreationTimeUtc));
                        if(files.Count>=5000) return new(files.OrderBy(f=>f.LastWrite).ToList(),skipped,true);
                    }
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { skipped++; }
                }
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { skipped++; }
        }
        return new(files.OrderBy(f=>f.LastWrite).ToList(),skipped,false);
    }
    public static string Recycle(string root,IReadOnlyList<ReviewFile> selected)
    {
        root=Path.GetFullPath(root); int recycled=0,skipped=0;
        foreach(var file in selected)
        {
            try
            {
                string path=Path.GetFullPath(file.Path);
                if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                // Recheck every ancestor so a replaced folder cannot redirect a reviewed path.
                var ancestor=new DirectoryInfo(Path.GetDirectoryName(path)!); bool linked=false;
                while(ancestor is not null)
                { if((ancestor.Attributes&FileAttributes.ReparsePoint)!=0) { linked=true; break; } if(ancestor.FullName.Equals(root,StringComparison.OrdinalIgnoreCase)) break; ancestor=ancestor.Parent; }
                var current=new FileInfo(path);
                if(linked || !current.Exists || (current.Attributes&FileAttributes.ReparsePoint)!=0 || current.Length!=file.Bytes || current.LastWriteTimeUtc!=file.LastWrite || current.CreationTimeUtc!=file.Created) { skipped++; continue; }
                FileSystem.DeleteFile(path,UIOption.AllDialogs,RecycleOption.SendToRecycleBin,UICancelOption.ThrowException); recycled++;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or OperationCanceledException) { skipped++; }
        }
        return $"Sent {recycled} files to the Recycle Bin. {skipped} changed, unavailable, or canceled. Restore files from Windows Recycle Bin if needed.";
    }
}
