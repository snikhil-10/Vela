using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;

// Metadata-only validation is portable. It does not instantiate or compile WinUI XAML.
string root=Path.GetFullPath(args.Length>0?args[0]:".");
var assets=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"src/Vela.Windows/obj/project.assets.json")));
var data=assets.RootElement;
string packages=data.GetProperty("packageFolders").EnumerateObject().First().Name;
var target=data.GetProperty("targets").EnumerateObject().First(p=>!p.Name.Contains('/')).Value;
var paths=Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!,"*.dll").ToList();
foreach(var package in target.EnumerateObject())
{
    if(!data.GetProperty("libraries").GetProperty(package.Name).GetProperty("type").ValueEquals("package")) continue;
    if(!package.Value.TryGetProperty("compile",out var compile)) continue;
    paths.AddRange(compile.EnumerateObject().Where(p=>p.Name.EndsWith(".dll")).Select(p=>Path.Combine(packages,package.Name.ToLowerInvariant(),p.Name)));
}
string sdk=Directory.GetDirectories(Path.Combine(packages,"microsoft.windows.sdk.net.ref")).Order().Last();
paths.AddRange(Directory.GetFiles(Path.Combine(sdk,"lib/net8.0"),"*.dll"));
using var context=new MetadataLoadContext(new PathAssemblyResolver(paths.Distinct()),"System.Private.CoreLib");
var assemblies=paths.Where(p=>Path.GetFileName(p).StartsWith("Microsoft.")).Distinct().Select(context.LoadFromAssemblyPath).ToList();
string[] namespaces=["Microsoft.UI.Xaml","Microsoft.UI.Xaml.Controls","Microsoft.UI.Xaml.Controls.Primitives","Microsoft.UI.Xaml.Shapes","Microsoft.UI.Xaml.Media","Microsoft.UI.Xaml.Automation"];
Type? Find(string name)=>namespaces.SelectMany(ns=>assemblies.Select(a=>a.GetType(ns+"."+name,false))).FirstOrDefault(t=>t is not null);
bool Member(Type? type,string member)=>type is not null && (type.GetProperty(member,BindingFlags.Instance|BindingFlags.Public|BindingFlags.FlattenHierarchy) is not null || type.GetEvent(member,BindingFlags.Instance|BindingFlags.Public|BindingFlags.FlattenHierarchy) is not null);
bool Attached(string owner,string member)
{
    var type=Find(owner); return type is not null && type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).Any(m=>m.Name=="Set"+member || m.Name=="Get"+member);
}
int count=0,failures=0;
var xamlFiles=Directory.EnumerateFiles(Path.Combine(root,"src/Vela.Windows"),"*.xaml",SearchOption.AllDirectories).Where(f=>!f.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar)).ToArray();
string winui=paths.First(p=>Path.GetFileName(p)=="Microsoft.WinUI.dll");
var generic=Path.Combine(Path.GetDirectoryName(winui)!,"Microsoft.WinUI","Themes","generic.xaml");
var resourceKeys=xamlFiles.Append(generic).Select(XDocument.Load).SelectMany(d=>d.Descendants())
    .Select(e=>e.Attribute(XName.Get("Key","http://schemas.microsoft.com/winfx/2006/xaml"))?.Value).Where(k=>k is not null).ToHashSet();
foreach(string file in xamlFiles)
{
    var doc=XDocument.Load(file,LoadOptions.SetLineInfo);
    foreach(var element in doc.Descendants())
    {
        if(element.Name.NamespaceName=="http://schemas.microsoft.com/winfx/2006/xaml") continue;
        string name=element.Name.LocalName;
        if(name.Contains('.'))
        {
            var pair=name.Split('.',2);
            if(!Member(Find(pair[0]),pair[1]) && !Attached(pair[0],pair[1])) Error(element,$"Unknown property element {name}");
            continue;
        }
        var type=Find(name);
        if(type is null) { Error(element,$"Unknown WinUI type {name}"); continue; }
        count++;
        foreach(var attribute in element.Attributes())
        {
            if(attribute.IsNamespaceDeclaration || attribute.Name.NamespaceName.Length>0) continue;
            foreach(Match reference in Regex.Matches(attribute.Value,@"\{(?:StaticResource|ThemeResource)\s+([^}]+)\}"))
                if(!resourceKeys.Contains(reference.Groups[1].Value)) Error(element,$"Unknown resource {reference.Groups[1].Value}");
            string property=attribute.Name.LocalName;
            if(property.Contains('.'))
            {
                var pair=property.Split('.',2);
                if(!Attached(pair[0],pair[1])) Error(element,$"Unknown attached property {property}");
            }
            else if(!Member(type,property)) Error(element,$"Unknown {name}.{property}");
        }
    }
    void Error(XElement element,string message)
    {
        var line=(System.Xml.IXmlLineInfo)element; Console.Error.WriteLine($"{Path.GetRelativePath(root,file)}:{line.LineNumber}: {message}"); failures++;
    }
}
Console.WriteLine($"Validated {count} XAML elements against Windows SDK metadata; {failures} errors. This is not XAML compilation or visual QA.");
return failures==0?0:1;
