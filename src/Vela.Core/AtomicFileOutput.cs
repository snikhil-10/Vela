namespace Vela.Core;

public static class AtomicFileOutput
{
    // Finish a new file beside the destination, close it, then replace the
    // destination in one same-directory move. A failed writer never truncates
    // the user's existing file. Cleanup names only this operation's staging file.
    public static async Task Write(string destination,Func<string,CancellationToken,Task> write,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(write);
        if(!Path.IsPathFullyQualified(destination) || string.IsNullOrEmpty(Path.GetFileName(destination)))
            throw new ArgumentException("Choose a file on a filesystem.",nameof(destination));
        cancellation.ThrowIfCancellationRequested();
        string target=Path.GetFullPath(destination);
        string temporary=Path.Combine(Path.GetDirectoryName(target)!,".vela-"+Guid.NewGuid().ToString("N")+".tmp");
        using(new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { }
        try
        {
            await write(temporary,cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            // Once committed, report success even if cancellation arrives
            // concurrently; never delete or roll back a completed destination.
            File.Move(temporary,target,overwrite:true);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}
