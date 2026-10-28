using System.Text;

namespace Vela.Core;

public static class SettingsBackup
{
    public const int MaximumBytes=4*1024*1024;
    public static async Task<AppSettings> Read(string path,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,8192,FileOptions.Asynchronous|FileOptions.SequentialScan);
        if(input.Length>MaximumBytes) throw TooLarge();
        using var bytes=new MemoryStream();
        byte[] buffer=new byte[8192];
        while(true)
        {
            // The bounded read also covers a file growing after its metadata
            // was checked, rather than trusting the picker's earlier size.
            int count=await input.ReadAsync(buffer.AsMemory(0,(int)Math.Min(buffer.Length,MaximumBytes+1-bytes.Length)),cancellation).ConfigureAwait(false);
            if(count==0) break;
            bytes.Write(buffer,0,count);
            if(bytes.Length>MaximumBytes) throw TooLarge();
        }
        var data=bytes.GetBuffer().AsSpan(0,(int)bytes.Length);
        Encoding encoding=new UTF8Encoding(false,true); int preamble=0;
        if(data.StartsWith(new byte[]{0xFF,0xFE,0,0})) { encoding=new UTF32Encoding(false,false,true); preamble=4; }
        else if(data.StartsWith(new byte[]{0,0,0xFE,0xFF})) { encoding=new UTF32Encoding(true,false,true); preamble=4; }
        else if(data.StartsWith(new byte[]{0xEF,0xBB,0xBF})) preamble=3;
        else if(data.StartsWith(new byte[]{0xFF,0xFE})) { encoding=new UnicodeEncoding(false,false,true); preamble=2; }
        else if(data.StartsWith(new byte[]{0xFE,0xFF})) { encoding=new UnicodeEncoding(true,false,true); preamble=2; }
        bytes.Position=preamble;
        using var reader=new StreamReader(bytes,encoding,detectEncodingFromByteOrderMarks:false);
        string json=await reader.ReadToEndAsync(cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        return SettingsStore.Deserialize(json);
    }
    public static Task Write(string path,AppSettings settings,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        if(settings.Version!=1) throw new NotSupportedException("This settings version is not supported.");
        string json=SettingsStore.Serialize(settings);
        if(Encoding.UTF8.GetByteCount(json)>MaximumBytes) throw TooLarge();
        return AtomicFileOutput.Write(path,(temporary,token)=>File.WriteAllTextAsync(temporary,json,token),cancellation);
    }
    private static InvalidDataException TooLarge()=>new("That settings backup exceeds the 4 MB limit. No settings were replaced.");
}
