using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Vela.Core;

public sealed record DeveloperFeed(string Name, string Url)
{
    public static List<DeveloperFeed> Normalize(IEnumerable<DeveloperFeed>? sources) => (sources ?? [])
        .Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Name) && DeveloperFeeds.TryWebUri(s.Url, out _))
        .Select(s => new DeveloperFeed(DeveloperFeeds.PlainText(s.Name, 80), new Uri(s.Url.Trim()).AbsoluteUri))
        .Where(s => s.Name.Length > 0).DistinctBy(s => s.Url, StringComparer.Ordinal).Take(DeveloperFeeds.MaximumSources).ToList();
}

public sealed record DeveloperFeedEntry(string Source, string Title, Uri Link, DateTimeOffset? Published)
{
    public string Label => $"{Title}\n{Source} · {(Published is { } date ? date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) : "Date unavailable")}\n{Link.Host}";
}
public sealed record DeveloperFeedFailure(string Source, string Reason);
public sealed record DeveloperFeedResult(int Succeeded, int Total, IReadOnlyList<DeveloperFeedEntry> Entries, IReadOnlyList<DeveloperFeedFailure> Failures);

// A headline reader, not a package manager. No item bodies, images or enclosures are fetched.
public sealed class DeveloperFeeds(HttpClient client)
{
    public const int MaximumSources = 16, MaximumBytes = 1_048_576, MaximumEntriesPerSource = 100, MaximumEntries = 200;
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly Regex Tags = new("<[^>]*>", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    public static bool TryWebUri(string? text, out Uri uri)
    {
        uri = null!;
        if (text is not { Length: <= 2048 } || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var candidate)
            || candidate.Scheme is not ("https" or "http") || candidate.Host.Length == 0 || candidate.UserInfo.Length != 0) return false;
        uri = candidate; return true;
    }

    internal static string PlainText(string text, int maximum)
    {
        string value = Spaces.Replace(Tags.Replace(WebUtility.HtmlDecode(text), " "), " ").Trim();
        value = new string(value.Where(c => !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format).ToArray());
        return value.Length <= maximum ? value : value[..maximum];
    }

    public static IReadOnlyList<DeveloperFeedEntry> Parse(ReadOnlyMemory<byte> bytes, Uri address, string source)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Feed exceeds the 1 MiB limit.");
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = MaximumBytes, IgnoreComments = true, IgnoreProcessingInstructions = true });
        while (reader.Read()) if (reader.Depth > 64) throw new InvalidDataException("Feed XML exceeds the nesting limit.");
        stream.Position = 0;
        using var documentReader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = MaximumBytes, IgnoreComments = true, IgnoreProcessingInstructions = true });
        var root = XDocument.Load(documentReader).Root ?? throw new InvalidDataException("Empty feed.");
        bool atom = root.Name == Atom + "feed";
        var container = atom ? root : root.Name == "rss" ? root.Element("channel") : null;
        if (container is null) throw new InvalidDataException("Expected an RSS 2.0 or Atom 1.0 feed.");
        var results = new List<DeveloperFeedEntry>(); var links = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in container.Elements(atom ? Atom + "entry" : "item").Take(1000))
        {
            string? linkText; XElement? linkElement;
            if (atom)
            {
                linkElement = item.Elements(Atom + "link").Where(e => (string?)e.Attribute("rel") is null or "alternate")
                    .OrderByDescending(e => (string?)e.Attribute("type") == "text/html")
                    .FirstOrDefault(e => ResolveLink(e, (string?)e.Attribute("href"), address) is not null);
                linkText = (string?)linkElement?.Attribute("href");
            }
            else
            {
                linkElement = item.Element("link"); linkText = linkElement?.Value;
                if (string.IsNullOrWhiteSpace(linkText) && item.Element("guid") is { } guid && (string?)guid.Attribute("isPermaLink") is null or "true")
                { linkElement = guid; linkText = guid.Value; }
            }
            var link = linkElement is null ? null : ResolveLink(linkElement, linkText, address);
            if (link is null || !links.Add(link.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped))) continue;
            string title = PlainText(item.Element(atom ? Atom + "title" : "title")?.Value ?? "", 240);
            string? date = atom ? item.Element(Atom + "published")?.Value ?? item.Element(Atom + "updated")?.Value : item.Element("pubDate")?.Value;
            DateTimeOffset? published = DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
            results.Add(new(source, title.Length == 0 ? "Untitled release" : title, link, published));
            if (results.Count == MaximumEntriesPerSource) break;
        }
        return results;
    }

    private static Uri? ResolveLink(XElement element, string? value, Uri address)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        foreach (var ancestor in element.AncestorsAndSelf().Reverse())
            if (ancestor.Attribute(XNamespace.Xml + "base") is { } basis)
            {
                if (!Uri.TryCreate(address, basis.Value, out var next) || !TryWebUri(next.AbsoluteUri, out address)) return null;
            }
        return Uri.TryCreate(address, value.Trim(), out var result) && TryWebUri(result.AbsoluteUri, out result) ? result : null;
    }

    public async Task<DeveloperFeedResult> Refresh(IEnumerable<DeveloperFeed> sources, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var selected = DeveloperFeed.Normalize(sources);
        using var slots = new SemaphoreSlim(2);
        var tasks = selected.Select(async source =>
        {
            await slots.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                try { return (Entries: await Fetch(source, deadline.Token).ConfigureAwait(false), Failure: (DeveloperFeedFailure?)null); }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                { return (Entries: (IReadOnlyList<DeveloperFeedEntry>)[], Failure: new DeveloperFeedFailure(source.Name, "Request timed out.")); }
                catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or XmlException)
                {
                    string reason = ex is HttpRequestException http ? http.StatusCode is { } status ? $"HTTP {(int)status}." : "Network request failed."
                        : ex is XmlException ? "Invalid XML or a prohibited document type." : ex.Message;
                    return (Entries: (IReadOnlyList<DeveloperFeedEntry>)[], Failure: new DeveloperFeedFailure(source.Name, reason));
                }
            }
            finally { slots.Release(); }
        }).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        var entries = results.SelectMany(r => r.Entries).DistinctBy(e => e.Link.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped), StringComparer.Ordinal)
            .OrderByDescending(e => e.Published).Take(MaximumEntries).ToArray();
        var failures = results.Where(r => r.Failure is not null).Select(r => r.Failure!).ToArray();
        return new(selected.Count - failures.Length, selected.Count, entries, failures);
    }

    private async Task<IReadOnlyList<DeveloperFeedEntry>> Fetch(DeveloperFeed source, CancellationToken cancellation)
    {
        var address = new Uri(source.Url);
        for (int redirect = 0; ; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.UserAgent.ParseAdd("Vela/0.2.2");
            request.Headers.Accept.ParseAdd("application/atom+xml, application/rss+xml, application/xml, text/xml");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            // The production client disables automatic redirects and cookies. Validate every redirect before sending it.
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                if (redirect == 3 || response.Headers.Location is not { } location || !Uri.TryCreate(address, location, out var next)
                    || !TryWebUri(next.AbsoluteUri, out next) || address.Scheme == "https" && next.Scheme != "https")
                    throw new InvalidDataException("Feed redirect was unsupported or exceeded three hops.");
                address = next; continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumBytes) throw new InvalidDataException("Feed exceeds the 1 MiB limit.");
            using var input = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
            using var bytes = new MemoryStream(); var buffer = new byte[16_384];
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, MaximumBytes + 1 - (int)bytes.Length)), cancellation).ConfigureAwait(false);
                if (read == 0) break;
                bytes.Write(buffer, 0, read);
                if (bytes.Length > MaximumBytes) throw new InvalidDataException("Feed exceeds the 1 MiB limit.");
            }
            cancellation.ThrowIfCancellationRequested();
            return Parse(bytes.ToArray(), address, source.Name);
        }
    }
}
