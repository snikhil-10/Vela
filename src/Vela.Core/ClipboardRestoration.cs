namespace Vela.Core;

public enum ClipboardRestoreResult { Restored, Superseded, Busy }

// Clipboard writers can report a transient refusal without throwing. A new
// copy takes precedence, including while we wait between bounded retries.
public static class ClipboardRestoration
{
    public static async Task<ClipboardRestoreResult> Restore(uint ownedSequence,Func<uint> currentSequence,Func<bool> write)
    {
        for(int attempt=0;attempt<3;attempt++)
        {
            if(currentSequence()!=ownedSequence) return ClipboardRestoreResult.Superseded;
            if(write()) return ClipboardRestoreResult.Restored;
            if(attempt<2) await Task.Delay(40);
        }
        return ClipboardRestoreResult.Busy;
    }
}
