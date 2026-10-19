using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Vela.Core;
using Vela.Windows.Interop;

namespace Vela.Windows.Services;

internal sealed partial class InputService
{
    private const nuint SnippetReplay=0x56454C41;
    private readonly InputReplayBuffer<Native.Input> deferredSnippetKeys=new();
    private bool preparingSnippet, snippetControlHeld;
    private long pendingSnippetId;
    private int snippetGeneration;
    private CancellationTokenSource? snippetPreparation;
    private void ReleaseSnippetControl()
    { if(snippetControlHeld) { snippetControlHeld=false; Send([KeyInput(0x56,2),KeyInput(0x11,2)]); } }
    private void AbortPendingSnippetInput()
    {
        preparingSnippet=false;
        var releases=deferredSnippetKeys.Discard();
        snippetKeys.DropReplay(); ReleaseSnippetControl();
        Send(releases.Select(key=>KeyInput((ushort)key,2)).ToArray());
    }
    private async Task ReplaySnippetInput(long requestId)
    {
        // A replayed trigger can queue the next replacement. Leave the rest of
        // the FIFO for that request, including physical input appended meanwhile.
        while(preparingSnippet && pendingSnippetId==requestId && deferredSnippetKeys.TryTake(out var input))
        {
            Send([input]);
            await Task.Delay(12);
        }
        if(pendingSnippetId==requestId)
        { Send(DrainSnippetInput()); preparingSnippet=false; }
    }
    private Native.Input[] DrainSnippetInput()
    {
        var inputs=deferredSnippetKeys.Drain(out var releases).ToList();
        inputs.AddRange(releases.Select(key=>KeyInput((ushort)key,2))); return inputs.ToArray();
    }
    private static Native.Input ReplayKey(Native.KeyboardData data,bool down)
    {
        var input=KeyInput((ushort)data.Key,(down?0u:2u)|(data.Flags&1));
        input.Data.Keyboard.ExtraInfo=SnippetReplay; return input;
    }
    private static bool SameSnippetField(nint window,nint focus)
    {
        var info=new Native.GuiThreadInfo { Size=(uint)Marshal.SizeOf<Native.GuiThreadInfo>() };
        Native.GetGUIThreadInfo(0,ref info);
        return Native.GetForegroundWindow()==window && info.Focus==focus;
    }
    private bool SameSnippetTarget(nint window,nint focus,int generation) => generation==snippetGeneration && active.Contains("textSnippets")
        && settings().EnabledFeatures.Contains("textSnippets") && !suspended && !Cleaning && SameSnippetField(window,focus) && !Excluded(window) && !StandardPasswordField();
    private void QueueSnippet(SnippetMatch match,nint window,nint focus,Native.KeyboardData completion)
    {
        if(!preparingSnippet)
            deferredSnippetKeys.Begin(new uint[]{0x10,0x11,0x12,0xA0,0xA1,0xA2,0xA3,0xA4,0xA5,0x5B,0x5C}.Where(key=>Down((int)key)));
        preparingSnippet=true;
        long requestId=++pendingSnippetId;
        int generation=snippetGeneration;
        queue.TryEnqueue(async ()=>await runOperation(()=>DeliverSnippet(match,window,focus,generation,completion,requestId)));
    }
    private async Task DeliverSnippet(SnippetMatch match,nint window,nint focus,int generation,Native.KeyboardData completion,long requestId)
    {
        bool gate=false,submitted=false;
        string phase="wait for clipboard";
        uint temporarySequence=0;
        ClipboardSnapshot? saved=null;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        snippetPreparation=timeout;
        try
        {
            await ClipboardService.TransferGate.WaitAsync(timeout.Token); gate=true;
            phase="read clipboard";
            uint originalSequence=Native.GetClipboardSequenceNumber();
            phase="snapshot clipboard";
            saved=await ClipboardPreparation.Read(originalSequence,Native.GetClipboardSequenceNumber,
                ()=>ClipboardSnapshot.Capture(Clipboard.GetContent(),timeout.Token),timeout.Token);
            phase="release physical modifiers";
            for(int i=0;i<100 && new[]{0x10,0x11,0x12,0x5B,0x5C}.Any(Down);i++) await Task.Delay(10,timeout.Token);
            phase="validate destination";
            if(!SameSnippetTarget(window,focus,generation))
                throw new OperationCanceledException("The snippet destination changed.");
            phase="validate clipboard";
            if(originalSequence!=Native.GetClipboardSequenceNumber())
                throw new OperationCanceledException("The clipboard changed before the replacement was ready.");
            phase="validate modifiers";
            if(new[]{0x10,0x11,0x12,0x5B,0x5C}.Any(Down))
                throw new OperationCanceledException("Release the shortcut keys and try the trigger again.");
            string body=SnippetExpander.Expand(match.Snippet.Text,DateTimeOffset.Now,saved.Text);
            phase="publish replacement";
            var publication=await TextClipboardPublisher.Publish(clipboardOwner,originalSequence,body,timeout.Token);
            if(publication is null)
                throw new OperationCanceledException("The clipboard changed before the replacement was ready.");
            temporarySequence=publication.Sequence;
            if(publication.Error is not null) throw publication.Error;
            var inputs=new List<Native.Input>();
            phase="remove trigger";
            for(int i=0;i<match.DeleteCount;i++)
            {
                Submit([KeyInput(8,0),KeyInput(8,2)]);
                await Task.Delay(12);
            }
            if(body.Length>0)
            {
                phase="paste replacement";
                // Let the destination process Control before queuing its paste key.
                // Keep physical input buffered until the chord has been released.
                snippetControlHeld=true;
                Submit([KeyInput(0x11,0)]);
                await WaitForControl(true);
                Submit([KeyInput(0x56,0),KeyInput(0x56,2)]);
                await Task.Delay(80);
                ReleaseSnippetControl();
                await WaitForControl(false);
            }
            if(match.Delimiter.Length>0 && SameSnippetTarget(window,focus,generation))
                AddKey(inputs,match.Delimiter=="\t"?(ushort)9:match.Delimiter is "\r" or "\n"?(ushort)13:(ushort)32);
            // Keep this key's down/up pair together so a focus change between
            // awaits cannot leave the delimiter held in the destination.
            Submit(inputs.ToArray());
            if(inputs.Count>0) await Task.Delay(12);
            submitted=true;
            phase="replay buffered input";
            await ReplaySnippetInput(requestId);
            // Give the destination time to read the clipboard; a new copy always wins.
            if(body.Length>0) await Task.Delay(800);
        }
        catch(Exception error)
        {
            RecordSnippetFailure(phase,error,submitted);
            string reason=string.IsNullOrWhiteSpace(error.Message)
                ?"Windows could not finish the replacement. Try the trigger again.":error.Message;
            Notice?.Invoke((submitted?"Snippet delivery stopped: ":"Snippet kept: ")+reason);
        }
        finally
        {
            ReleaseSnippetControl();
            try
            {
                if(saved is not null && temporarySequence!=0) await saved.Restore(temporarySequence);
            }
            catch(Exception error)
            {
                RecordSnippetFailure("restore clipboard",error,submitted);
                Notice?.Invoke("The clipboard could not be restored. Try copying the item again.");
            }
            saved?.Dispose();
            if(gate) ClipboardService.TransferGate.Release();
            if(ReferenceEquals(snippetPreparation,timeout)) snippetPreparation=null;
            if(preparingSnippet && pendingSnippetId==requestId)
            {
                // Failed preparation leaves the prefix untouched. Restore its completing key
                // only in the original field; subsequent physical input follows its current focus.
                var replay=new List<Native.Input>();
                if(!submitted && !Cleaning && SameSnippetField(window,focus)) { replay.Add(KeyInput((ushort)completion.Key,0)); replay.Add(KeyInput((ushort)completion.Key,2)); }
                else if(!submitted) Notice?.Invoke("Snippet canceled because the destination changed; its typed prefix was kept.");
                Send(replay.ToArray());
                await ReplaySnippetInput(requestId);
            }
        }
        void Submit(Native.Input[] batch)
        {
            if(batch.Length==0) return;
            if(!SameSnippetTarget(window,focus,generation))
                throw new OperationCanceledException("The snippet destination changed.");
            uint sent=Native.SendInput((uint)batch.Length,batch,Marshal.SizeOf<Native.Input>());
            submitted|=sent>0;
            if(sent!=batch.Length)
            {
                Send(batch.Where(input=>(input.Data.Keyboard.Flags&2)!=0).ToArray());
                throw new InvalidOperationException("Windows could not deliver the complete snippet. Check the destination app's permissions.");
            }
        }
        async Task WaitForControl(bool pressed)
        {
            // SendInput queues an event; the asynchronous key state changes only
            // after the low-level hook returns. Do not replay a letter while
            // Windows still regards our paste modifier as held.
            long until=Environment.TickCount64+750;
            do
            {
                await Task.Delay(5,timeout.Token);
                if(!SameSnippetTarget(window,focus,generation))
                    throw new OperationCanceledException("The snippet destination changed.");
                if(Down(0x11)==pressed) return;
            } while(Environment.TickCount64<until);
            throw new InvalidOperationException("Windows did not confirm the paste modifier state.");
        }
    }
    private static void RecordSnippetFailure(string phase,Exception error,bool submitted)
    {
        // Failure metadata only: no trigger, replacement, clipboard data, key events,
        // provider message or original exception/stack is written to the local log.
        StartupDiagnostics.Record("Snippet operation",new InvalidOperationException(
            $"Phase={phase}; Type={error.GetType().Name}; HRESULT=0x{error.HResult:X8}; Submitted={submitted}."));
    }
}
