using Vela.Core;

internal static class CommandQueryChecks
{
    private static void Require(bool value) { if(!value) throw new Exception("Command query assertion failed."); }
    public static IEnumerable<(string Name,Action Check)> All()
    {
        yield return ("Command search requires every word in any order across explicit fields",()=>
        {
            var query=new CommandQuery("editor projects");
            Require(query.Rank("Projects › Editor","Launcher",@"C:\Apps\editor.exe")==3);
            Require(query.Rank("Editor","Launcher",@"C:\Projects\editor.exe")==4);
            Require(query.Rank("Editor","Launcher",@"C:\Apps\editor.exe") is null);
        });
        yield return ("Quoted command phrases cannot be split across fields or words",()=>
        {
            var query=new CommandQuery("\"work notes\" launcher");
            Require(query.Rank("Work notes","Launcher") is not null);
            Require(query.Rank("Work","Launcher","notes") is null);
            Require(query.Rank("Work personal notes","Launcher") is null);
        });
        yield return ("Command search tolerates an unfinished quote while typing",()=>
        {
            Require(new CommandQuery("\"work notes").Rank("Work notes") == 0);
            Require(new CommandQuery("\"work notes").Rank("Work personal notes") is null);
        });
        yield return ("Blank command queries preserve source order and include all commands",()=>
        {
            string[] names=["Zulu","Alpha","Middle"];
            foreach(string? text in new string?[]{null,""," \t\r\n ","\"\""})
            {
                var query=new CommandQuery(text);
                Require(names.OrderBy(name=>query.Rank(name)).SequenceEqual(names));
                Require(query.Rank(null)==0);
            }
        });
        yield return ("Command ranking favors exact names then prefixes before metadata",()=>
        {
            var query=new CommandQuery("notes");
            var entries=new[]{("Other","notes"),("My notes",""),("Notes archive",""),("Notes","")};
            var ranked=entries.OrderBy(entry=>query.Rank(entry.Item1,entry.Item2)).Select(entry=>entry.Item1);
            Require(ranked.SequenceEqual(["Notes","Notes archive","My notes","Other"]));
        });
        yield return ("Equal command ranks keep the original order",()=>
        {
            var query=new CommandQuery("editor");
            string[] names=["Work editor","Home editor","Travel editor"];
            Require(names.OrderBy(name=>query.Rank(name)).SequenceEqual(names));
        });
        yield return ("Command matching is literal and retains Unicode and path punctuation",()=>
        {
            Require(new CommandQuery("日本語 notes").Rank("日本語 › Notes") is not null);
            Require(new CommandQuery(@"[draft] C:\Work").Rank("[draft]",@"C:\Work\draft.txt") is not null);
            Require(new CommandQuery(".*").Rank("Everything") is null);
            Require(new CommandQuery("CAFÉ").Rank("Café")==0);
        });
        yield return ("Repeated command words and Unicode whitespace do not change ranking",()=>
        {
            Require(new CommandQuery("Notes notes NOTES").Rank("Notes")==0);
            Require(new CommandQuery("\u2003work\u2003notes\t").Rank("Work notes")==0);
            Require(new CommandQuery("missing").Rank(null,null) is null);
        });
    }
}
