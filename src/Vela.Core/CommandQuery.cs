using System.Text;

namespace Vela.Core;

// Literal, case-insensitive AND terms. Quotes keep a phrase together; a phrase
// must occur within one field, never across a label/metadata boundary.
public sealed class CommandQuery
{
    private readonly string[] terms;
    private readonly string phrase;

    public CommandQuery(string? text)
    {
        var parsed=new List<string>();
        var token=new StringBuilder();
        bool quoted=false;
        void Complete()
        {
            string value=token.ToString().Trim(); token.Clear();
            if(value.Length>0 && !parsed.Contains(value,StringComparer.OrdinalIgnoreCase)) parsed.Add(value);
        }
        foreach(char character in text??"")
        {
            if(character=='"') { quoted=!quoted; continue; }
            if(!quoted && char.IsWhiteSpace(character)) Complete();
            else token.Append(character);
        }
        Complete(); // An unfinished quote remains a useful phrase while typing.
        terms=parsed.ToArray(); phrase=string.Join(" ",terms);
    }

    // Lower ranks sort first. LINQ's stable ordering keeps the existing command
    // order for an empty query and for equally ranked results.
    public int? Rank(string? label,params string?[]? keywords)
    {
        label??="";
        keywords??=[];
        if(terms.Length==0) return 0;
        bool Contains(string? field,string term)=>field?.Contains(term,StringComparison.OrdinalIgnoreCase)==true;
        if(terms.Any(term=>!Contains(label,term) && !keywords.Any(field=>Contains(field,term)))) return null;
        if(label.Equals(phrase,StringComparison.OrdinalIgnoreCase)) return 0;
        if(label.StartsWith(phrase,StringComparison.OrdinalIgnoreCase)) return 1;
        if(Contains(label,phrase)) return 2;
        if(terms.All(term=>Contains(label,term))) return 3;
        return 4;
    }
}
