using Vela.Windows.Services;

internal static class MicrophoneChecks
{
    // Explicit opt-in mode. The ordinary backend suite stays read-only.
    internal static int RoundTrip()
    {
        try
        {
            var original=MicrophoneService.List();
            if(original.Count!=1 || original[0].Muted)
            {
                Console.WriteLine($"SKIP microphone write check: requires one initially unmuted active endpoint; found {original.Count}. No state changed.");
                return 2;
            }
            var device=original[0]; bool muted=false,unmuted=false,restored=false;
            try
            {
                MicrophoneService.SetAll(true);
                muted=MicrophoneService.List().SingleOrDefault(item=>item.Id==device.Id)?.Muted==true;
                if(!muted) throw new InvalidOperationException("Mute-all was not observed on the original endpoint.");
                var active=MicrophoneService.List();
                if(active.Count!=1 || active[0].Id!=device.Id) throw new InvalidOperationException("Capture endpoints changed during the guarded check.");
                MicrophoneService.SetAll(false);
                unmuted=MicrophoneService.List().SingleOrDefault(item=>item.Id==device.Id)?.Muted==false;
                if(!unmuted) throw new InvalidOperationException("Unmute-all was not observed on the original endpoint.");
            }
            finally
            {
                MicrophoneService.SetMute(device.Id,device.Muted);
                restored=MicrophoneService.List().SingleOrDefault(item=>item.Id==device.Id)?.Muted==device.Muted;
                if(!restored) throw new InvalidOperationException("Original microphone mute state was not restored.");
            }
            Console.WriteLine($"PASS microphone mute/unmute-all round trip: mute observed={muted}; unmute observed={unmuted}; original state restored by endpoint identity={restored}. No recording or playback.");
            return 0;
        }
        catch(Exception error) { Console.WriteLine($"FAIL microphone round trip: {error.GetType().Name}: {error.Message}"); return 1; }
    }
}
