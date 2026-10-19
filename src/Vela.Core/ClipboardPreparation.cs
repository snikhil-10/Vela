namespace Vela.Core;

public static class ClipboardPreparation
{
    public static async Task<bool> Write(uint originalSequence,Func<uint> currentSequence,Func<bool> write,CancellationToken token)
    {
        for(int attempt=0;attempt<6;attempt++)
        {
            token.ThrowIfCancellationRequested();
            if(currentSequence()!=originalSequence) return false;
            try { if(write()) return true; }
            catch(Exception error) when(error.HResult==unchecked((int)0x800401D0) && attempt<5) { }
            if(attempt<5) await Task.Delay(40,token);
        }
        throw new InvalidOperationException("The clipboard is busy. Try again.");
    }
    // CLIPBRD_E_CANT_OPEN is transient contention, not a malformed format or
    // an unavailable provider. Retry the entire snapshot against fresh views.
    public static async Task<T> Read<T>(uint originalSequence,Func<uint> currentSequence,Func<Task<T>> read,CancellationToken token) where T:IDisposable
    {
        for(int attempt=0;;attempt++)
        {
            token.ThrowIfCancellationRequested();
            CheckSequence();
            T result;
            try { result=await read(); }
            catch(Exception error) when(error.HResult==unchecked((int)0x800401D0) && attempt<5)
            {
                await Task.Delay(40,token);
                continue;
            }
            try { token.ThrowIfCancellationRequested(); CheckSequence(); return result; }
            catch { result.Dispose(); throw; }
        }
        void CheckSequence()
        {
            if(currentSequence()!=originalSequence)
                throw new OperationCanceledException("The clipboard changed before the replacement was ready.");
        }
    }
}
