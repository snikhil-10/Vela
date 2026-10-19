using Vela.Core;

internal static class InputChecks
{
    public static void Run(Action<string,Action> test)
    {
        static void Require(bool condition) { if(!condition) throw new Exception("Input behavior did not match the contract."); }
        test("Explorer's observed hyphen-separated running-window label identifies Vela",()=>
        {
            Require(TaskbarAppLabel.IsVela("Vela.Windows - 1 running window"));
            Require(TaskbarAppLabel.IsVela("Vela - 2 running windows"));
            Require(!TaskbarAppLabel.IsVela("Vela.Windows - 1.txt - Notepad"));
            Require(!TaskbarAppLabel.IsVela("Vela.Windows - Notes"));
        });
        test("Shelf highlights a drag before taskbar hover while ordinary clicks remain quiet",()=>
        {
            var gesture=new ShelfDragGesture();
            Require(!gesture.Update(true,400,400,false,false,false));
            Require(!gesture.Update(true,403,402,false,false,false));
            Require(gesture.Update(true,410,400,false,false,false));
            Require(gesture.Update(true,410,1000,true,false,false));
            Require(!gesture.Update(false,410,1000,true,false,false));
        });
        test("Taskbar button rearranging and window movement do not expose shelf targets",()=>
        {
            var gesture=new ShelfDragGesture();
            Require(!gesture.Update(true,400,1000,true,false,false));
            Require(!gesture.Update(true,410,400,false,false,false));
            gesture.Update(false,410,400,false,false,false);
            Require(!gesture.Update(true,400,400,false,true,false));
            Require(!gesture.Update(true,600,600,false,false,false));
            gesture.Update(false,600,600,false,false,false);
            Require(!gesture.Update(true,400,400,false,false,false));
            Require(gesture.Update(true,420,400,false,false,false));
        });
        test("Escape hides the shelf target until mouse release and cleanup resets the next drag",()=>
        {
            var gesture=new ShelfDragGesture();
            gesture.Update(true,400,400,false,false,false);
            Require(gesture.Update(true,420,400,false,false,false));
            Require(!gesture.Update(true,420,400,false,false,true));
            Require(!gesture.Update(true,800,800,false,false,false));
            gesture.Update(false,800,800,false,false,false);
            gesture.Update(true,800,800,false,false,false);
            Require(gesture.Update(true,820,800,false,false,false));
            gesture.Reset();
            Require(!gesture.Update(true,-800,-400,false,false,false));
            Require(gesture.Update(true,-810,-400,false,false,false));
        });
        test("Shelf icon preparation probes the button band rather than taskbar edge padding",()=>
        {
            Require(TaskbarDropGeometry.ButtonProbe(0,1000,1920,1096,740,900)==(740,1048));
            Require(TaskbarDropGeometry.ButtonProbe(0,1000,1920,1096,740,1001)==(740,1048));
            Require(TaskbarDropGeometry.ButtonProbe(-1920,-96,0,0,-740,100)==(-740,-48));
            Require(TaskbarDropGeometry.ButtonProbe(-96,0,0,1080,20,400)==(-48,400));
        });
        test("Shelf taskbar targeting recognizes executable labels and grouped running-window counts",()=>
        {
            foreach(string name in new[]{"Vela","Vela.Windows","Vela.Windows.exe","Vela · File shelf","Vela · File shelf - Vela","Vela, 1 running window","Vela.Windows, 2 running windows","Vela.Windows.exe (3 running windows)","Vela (2)"," vela, 2 fenêtres ouvertes "})
                Require(TaskbarAppLabel.IsVela(name));
        });
        test("Shelf taskbar targeting rejects other apps and similarly named document titles",()=>
        {
            foreach(string? name in new[]{null,"","Running applications","VelaHelper","Vela.Windows.Helper","Vela - Notepad","Vela, notes - Notepad","Vela, 12.txt - Notepad","Vela · File shelfish"})
                Require(!TaskbarAppLabel.IsVela(name));
        });
        test("Canceling partially replayed typing releases delivered keys and discards unplayed shortcuts",()=>
        {
            var buffer=new InputReplayBuffer<string>(); buffer.Begin([]);
            buffer.Capture(0xA0,true,"Shift down");
            buffer.Capture(0x41,true,"A down"); buffer.Capture(0x41,false,"A up");
            buffer.Capture(0xA0,false,"Shift up");
            buffer.Capture(0xA2,true,"unplayed Ctrl down"); buffer.Capture(0x43,true,"unplayed C down");
            Require(buffer.TryTake(out var shift) && shift=="Shift down");
            Require(buffer.TryTake(out var a) && a=="A down");
            Require(buffer.TryTake(out _) && buffer.Discard().SequenceEqual(new uint[]{0xA0}));
            Require(!buffer.TryTake(out _) && buffer.Discard().Length==0);
            buffer.Begin([]); buffer.Capture(0xA2,true,"Ctrl down");
            Require(buffer.TryTake(out _) && buffer.Discard().SequenceEqual(new uint[]{0xA2}));
            buffer.Begin([]); buffer.Capture(0xA0,true,"Shift down"); buffer.Capture(0xA0,false,"Shift up");
            Require(buffer.TryTake(out _) && buffer.TryTake(out _) && buffer.Discard().Length==0);
        });
        test("A replayed snippet delimiter cannot swallow the next physical delimiter",()=>
        {
            var guard=new SnippetKeyGuard();
            guard.Complete(32,true); // A queued Space completed a second snippet.
            Require(!guard.Consume(32,true,false) && !guard.Consume(32,false,false));
            Require(guard.Consume(32,false,true)); // Only its queued release is swallowed.
            Require(!guard.Consume(32,true,true)); // The next queued Space remains usable.
            guard.Complete(86,false);
            Require(guard.Consume(86,true,false) && !guard.Consume(86,true,true));
            Require(guard.Consume(86,false,false) && !guard.Consume(86,false,false));
            guard.Complete(32,true); guard.DropReplay(); Require(!guard.Consume(32,true,true));
            guard.Complete(86,false); guard.Clear(); Require(!guard.Consume(86,true,false));
        });
        test("Paced snippet replay retains FIFO order when a second trigger pauses the reader",()=>
        {
            var buffer=new InputReplayBuffer<string>(); buffer.Begin([]);
            buffer.Capture(0x41,true,"A down"); buffer.Capture(0x41,false,"A up");
            buffer.Capture(0x42,true,"B down");
            Require(buffer.TryTake(out var first) && first=="A down");
            buffer.Capture(0x42,false,"B up"); // New physical input during replay.
            Require(buffer.TryTake(out var second) && second=="A up");
            Require(buffer.Drain(out var releases).SequenceEqual(new[]{"B down","B up"}) && releases.Length==0);
            Require(!buffer.TryTake(out _));
        });
        test("Asynchronous snippet preparation retains new modifier pairs and releases overflowed keys",()=>
        {
            var buffer=new InputReplayBuffer<string>(); buffer.Begin([0x10,0xA0]);
            Require(buffer.Capture(0xA0,false,"old Shift up")==BufferedInputResult.Forward);
            buffer.Capture(0x10,true,"new Shift down");
            Require(buffer.Capture(0x10,false,"new Shift up")==BufferedInputResult.Buffered);
            Require(buffer.Drain(out _).Length==2);
            Require(buffer.Capture(0xA2,true,"Ctrl down")==BufferedInputResult.Buffered);
            buffer.Capture(0x43,true,"C down"); buffer.Capture(0x43,false,"C up");
            Require(buffer.Capture(0xA2,false,"Ctrl up")==BufferedInputResult.Buffered);
            Require(buffer.Drain(out var release).SequenceEqual(new[]{"Ctrl down","C down","C up","Ctrl up"}) && release.Length==0);
            buffer.Begin([]); buffer.Capture(0xA2,true,"Ctrl down");
            for(int i=1;i<InputReplayBuffer<string>.Capacity;i++) buffer.Capture(0x41,true,"repeat");
            Require(buffer.Capture(0xA2,false,"overflow Ctrl up")==BufferedInputResult.Full);
            Require(buffer.Drain(out release).Length==InputReplayBuffer<string>.Capacity && release.Contains(0xA2u));
            Require(buffer.Drain(out release).Length==0 && release.Length==0);
        });
        static SnippetMatch? Type(SnippetMatcher matcher,string text,params Snippet[] snippets)
        { SnippetMatch? match=null; foreach(char c in text) match=matcher.Push(c.ToString(),snippets); return match; }
        test("Arbitrary snippet triggers and activation modes survive settings migration",()=>
        {
            var old=SettingsStore.Deserialize("{\"Snippets\":[{\"Name\":\"Legacy\",\"Trigger\":\";s\",\"Text\":\"one\"},{\"Name\":\"Letter\",\"Trigger\":\"x\",\"Text\":\"two\",\"Expansion\":\"afterDelimiter\"}]}");
            var round=SettingsStore.Deserialize(SettingsStore.Serialize(old));
            Require(round.Snippets.Count==2 && round.Snippets[0].Expansion=="immediate" && round.Snippets[1].Expansion=="afterDelimiter");
        });
        test("Immediate snippets swallow only the completing key and erase the typed prefix",()=>
        {
            var match=Type(new(),"sig",new Snippet("Signature","sig","Hello"));
            Require(match is { DeleteCount:2,Completion:"g",Delimiter:"" });
        });
        test("Space Tab and Enter each trigger delayed expansion and are preserved once",()=>
        {
            foreach(string separator in new[]{" ","\t","\r","\n"})
            {
                var matcher=new SnippetMatcher(); var snippet=new Snippet("Signature","sig","Hello","afterDelimiter");
                Require(Type(matcher,"sig",snippet) is null);
                var match=matcher.Push(separator,[snippet]);
                Require(match is { DeleteCount:3 } && match.Delimiter==separator && matcher.Push(separator,[snippet]) is null);
            }
        });
        test("The longest delayed suffix wins independently of snippet list order",()=>
        {
            var match=Type(new(),"address ",new("Short","ess","short","afterDelimiter"),new("Long","address","long","afterDelimiter"));
            Require(match?.Snippet.Name=="Long" && match.DeleteCount==7);
        });
        test("Matching is exact and whitespace prevents triggers spanning separate words",()=>
        {
            var snippet=new Snippet("S","sig","hello"); var matcher=new SnippetMatcher();
            Require(Type(matcher,"SIG",snippet) is null); matcher.Clear();
            Require(Type(matcher,"si g",snippet) is null);
        });
        test("Backspace and context reset cannot combine unrelated trigger fragments",()=>
        {
            var snippet=new Snippet("S","sig","hello"); var matcher=new SnippetMatcher();
            Type(matcher,"six",snippet); matcher.Backspace(); Require(Type(matcher,"g",snippet) is not null);
            Type(matcher,"si",snippet); matcher.Clear(); Require(Type(matcher,"g",snippet) is null);
        });
        test("Unicode triggers count visible typed characters rather than surrogate units",()=>
        {
            var matcher=new SnippetMatcher(); var snippet=new Snippet("Unicode","😀x","done");
            matcher.Push("😀",[snippet]); var match=matcher.Push("x",[snippet]);
            Require(match?.DeleteCount==1);
            Require(SnippetMatcher.ValidTrigger("a") && SnippetMatcher.ValidTrigger("@email") && !SnippetMatcher.ValidTrigger("two words") && !SnippetMatcher.ValidTrigger("bad\0"));
        });
        test("A single-character empty replacement can consume its completing key",()=>
        { Require(Type(new(),"x",new Snippet("Remove","x","")) is { DeleteCount:0,Delimiter:"" }); });
        test("Keyboard-only and mouse-lock cleaning sessions keep a 60-second bound",()=>
        {
            foreach(bool mouse in new[]{false,true})
            {
                var session=new CleaningSession(); session.Start(100,mouse);
                Require(session.Active && session.LockMouse==mouse && session.SecondsRemaining(100)==60);
                Require(!session.ShouldEnd(60099) && session.ShouldEnd(60100)); session.Stop();
                Require(!session.Active && !session.LockMouse && session.SecondsRemaining(60100)==0);
            }
        });
        test("Escape must be continuously held for two seconds including repeated keydowns",()=>
        {
            var session=new CleaningSession(); session.Start(0,true); session.Escape(true,100);
            session.Escape(true,1000); Require(!session.ShouldEnd(2099) && session.ShouldEnd(2100));
            session.Start(0,true); session.Escape(true,100);
            session.Escape(false,1500); Require(!session.ShouldEnd(3000)); session.Escape(true,3100);
            Require(!session.ShouldEnd(5099) && session.ShouldEnd(5100));
            session.Escape(false,5100); Require(session.ShouldEnd(5101)); // Release between timer ticks still unlocks.
        });
        test("Cleaning restart cannot inherit a previous Escape hold",()=>
        {
            var session=new CleaningSession(); session.Start(0,true); session.Escape(true,10); session.Stop(); session.Start(1000,false);
            Require(!session.ShouldEnd(3000) && !session.LockMouse);
        });
        test("Shelf targeting and mouse-lock preferences persist independently",()=>
        {
            foreach(string mode in new[]{"manual","taskbar","velaIcon","tray"})
            {
                var settings=SettingsStore.Deserialize(SettingsStore.Serialize(new AppSettings { ShelfDropTarget=mode,CleaningLockMouse=true,ShelfRemoveAfterDrop=true }));
                Require(settings.ShelfDropTarget==mode && settings.CleaningLockMouse && settings.ShelfRemoveAfterDrop);
            }
            Require(SettingsStore.Deserialize("{\"ShelfDropTarget\":\"unknown\"}").ShelfDropTarget=="manual");
        });
    }
}
