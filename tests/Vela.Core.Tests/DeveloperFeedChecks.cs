using System.Net;
using System.Text;
using Vela.Core;

internal static class DeveloperFeedChecks
{
    private const string Rss = """<rss version="2.0"><channel><title>Dev</title><item><title>Version 2 &amp; fixes</title><link>/v2</link><pubDate>Mon, 19 Oct 2026 12:00:00 GMT</pubDate></item></channel></rss>""";
    private static readonly Uri Address = new("https://example.test/releases/feed.xml");
    private static IReadOnlyList<DeveloperFeedEntry> Parse(string xml) => DeveloperFeeds.Parse(Encoding.UTF8.GetBytes(xml), Address, "Dev");
    public static IEnumerable<(string Name, Action Check)> All()
    {
        yield return ("RSS headlines preserve Unicode, relative links and dates", () =>
        { var entry = Parse(Rss).Single(); Require(entry.Title == "Version 2 & fixes" && entry.Link.AbsoluteUri == "https://example.test/v2" && entry.Published == new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)); });
        yield return ("Atom alternate HTML links respect inherited XML bases", () =>
        {
            var item = Parse("""<feed xmlns="http://www.w3.org/2005/Atom" xml:base="/dev/"><entry xml:base="versions/"><title type="html">&lt;b&gt;更新&lt;/b&gt; &amp;amp; fixes</title><updated>2026-10-18T20:30:00-07:00</updated><link rel="self" href="self.xml"/><link rel="enclosure" href="setup.exe"/><link href="release" type="text/html"/></entry></feed>""").Single();
            Require(item.Title == "更新 & fixes" && item.Link.AbsoluteUri == "https://example.test/dev/versions/release" && item.Published?.UtcDateTime.Hour == 3);
        });
        yield return ("Feed entries reject unsafe links and deduplicate article fragments", () =>
        {
            var items = Parse("""<rss><channel><item><title>Bad</title><link>file:///C:/test</link></item><item><link>javascript:alert(1)</link></item><item><link>https://user:password@example.test/</link></item><item><link>/v2#first</link></item><item><link>/v2#second</link></item><item><guid isPermaLink="false">https://example.test/guid</guid></item></channel></rss>""");
            Require(items.Count == 1 && items[0].Title == "Untitled release");
        });
        yield return ("RSS permalink GUIDs work and malformed dates stay unknown", () =>
        { var item = Parse("""<rss><channel><item><guid>https://example.test/version</guid><pubDate>not a date</pubDate></item></channel></rss>""").Single(); Require(item.Published is null && item.Link.AbsolutePath == "/version"); });
        yield return ("DTD and entity expansion are prohibited and non-feeds fail explicitly", () =>
        { Expect<System.Xml.XmlException>(() => Parse("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///private'>]><rss><channel><item><title>&x;</title></item></channel></rss>")); Expect<InvalidDataException>(() => Parse("<html/>")); Expect<System.Xml.XmlException>(() => Parse("<rss>")); });
        yield return ("Feed parser bounds bytes, title text and per-source entries", () =>
        {
            Expect<InvalidDataException>(() => DeveloperFeeds.Parse(new byte[DeveloperFeeds.MaximumBytes + 1], Address, "Dev"));
            string item = "<item><title>" + new string('a', 500) + "</title><link>/v{0}</link></item>";
            var parsed = Parse("<rss><channel>" + string.Concat(Enumerable.Range(0, 130).Select(i => string.Format(item, i))) + "</channel></rss>");
            Require(parsed.Count == 100 && parsed.All(e => e.Title.Length == 240));
        });
        yield return ("Feed settings normalize null, unsafe, duplicate and excess sources", () =>
        {
            var settings = SettingsStore.Deserialize("""{"DeveloperFeeds":[null,{"Name":"","Url":"https://example.test"},{"Name":"Unsafe","Url":"file:///x"},{"Name":"One","Url":"https://example.test"},{"Name":"Duplicate","Url":"https://example.test/"}],"EnabledFeatures":["developerFeeds","mixer"]}""");
            Require(settings.DeveloperFeeds.Count == 1 && settings.EnabledFeatures.SetEquals(["developerFeeds"]));
            settings.DeveloperFeeds = Enumerable.Range(0, 40).Select(i => new DeveloperFeed("Feed " + i, "https://example.test/" + i)).ToList(); settings.Normalize();
            Require(SettingsStore.Deserialize(SettingsStore.Serialize(settings)).DeveloperFeeds.Count == 16);
            Require(SettingsStore.Deserialize("{}").DeveloperFeeds.Count == 0 && SettingsStore.Deserialize("{\"DeveloperFeeds\":null}").DeveloperFeeds.Count == 0);
        });
        yield return ("Feed aggregation isolates failures, deduplicates and ranks dates", () =>
        {
            using var server = new FeedServer((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/bad" ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Xml(request.RequestUri.AbsolutePath == "/atom" ? """<feed xmlns="http://www.w3.org/2005/Atom"><entry><title>New</title><updated>2026-10-20T00:00:00Z</updated><link href="/new"/></entry><entry><title>Same</title><link href="/v2#dup"/></entry></feed>""" : Rss)));
            using var client = new HttpClient(server); var result = new DeveloperFeeds(client).Refresh([new("RSS", "https://example.test/rss"), new("Atom", "https://example.test/atom"), new("Bad", "https://example.test/bad")], default).GetAwaiter().GetResult();
            Require(result.Succeeded == 2 && result.Total == 3 && result.Entries.Count == 2 && result.Entries[0].Title == "New" && result.Failures.Single() == new DeveloperFeedFailure("Bad", "HTTP 503."));
        });
        yield return ("Feed redirects resolve links at final URL without fetching articles", () =>
        {
            using var server = new FeedServer((r, _) => Task.FromResult(r.RequestUri!.AbsolutePath == "/start" ? Redirect("/final/feed") : Xml(Rss.Replace("/v2", "v2"))));
            using var client = new HttpClient(server); var result = Read(client, "/start");
            Require(server.Calls == 2 && result.Entries.Single().Link.AbsolutePath == "/final/v2");
        });
        yield return ("Feed redirect loops, unsafe schemes and HTTPS downgrade stop before dispatch", () =>
        {
            foreach (string target in new[] { "/loop", "file:///local", "http://example.test/plain", "https://user:secret@example.test/private" })
            {
                using var server = new FeedServer((_, _) => Task.FromResult(Redirect(target))); using var client = new HttpClient(server);
                Require(Read(client, "/loop").Failures.Count == 1 && server.Calls == (target == "/loop" ? 4 : 1));
            }
        });
        yield return ("Oversized feed bodies are bounded with and without Content-Length", () =>
        {
            foreach (bool known in new[] { true, false })
            {
                using var body = new CountingStream(DeveloperFeeds.MaximumBytes * 2); using var server = new FeedServer((_, _) =>
                {
                    var content = new StreamContent(body); if (known) content.Headers.ContentLength = DeveloperFeeds.MaximumBytes * 2;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
                });
                using var client = new HttpClient(server); Require(Read(client, "/big").Failures.Count == 1);
                Require(known ? body.ReadBytes == 0 : body.ReadBytes == DeveloperFeeds.MaximumBytes + 1);
            }
        });
        yield return ("Canceling feeds before and during HTTP requests yields no partial success", () =>
        {
            using var cancel = new CancellationTokenSource();
            using var server = new FeedServer((_, token) => { cancel.Cancel(); token.ThrowIfCancellationRequested(); return Task.FromResult(Xml(Rss)); }); using var client = new HttpClient(server);
            Expect<OperationCanceledException>(() => Read(client, "/rss", cancel.Token)); Require(server.Calls == 1);
            Expect<OperationCanceledException>(() => Read(client, "/rss", cancel.Token)); Require(server.Calls == 1);
        });
        yield return ("Feed refresh caps concurrent sources at two and the combined view at 200", () =>
        {
            int active = 0, peak = 0; object gate = new();
            using var server = new FeedServer(async (request, token) =>
            {
                lock (gate) { active++; peak = Math.Max(peak, active); }
                await Task.Delay(10, token);
                lock (gate) active--;
                return Xml("<rss><channel>" + string.Concat(Enumerable.Range(0, 100).Select(i => $"<item><link>{request.RequestUri!.AbsolutePath}/{i}</link></item>")) + "</channel></rss>");
            }); using var client = new HttpClient(server);
            var result = new DeveloperFeeds(client).Refresh(Enumerable.Range(0, 30).Select(i => new DeveloperFeed("Feed " + i, "https://example.test/feed" + i)), default).GetAwaiter().GetResult();
            Require(peak == 2 && result.Total == 16 && server.Calls == 16 && result.Entries.Count == 200);
        });
        yield return ("Unsafe Atom bases cannot turn a web link into a local target", () =>
        { Require(Parse("""<feed xmlns="http://www.w3.org/2005/Atom" xml:base="file:///private/"><entry><title>No</title><link href="https://example.test/article"/></entry></feed>""").Count == 0); });
        yield return ("Feed XML nesting is bounded before constructing entry trees", () =>
        { Expect<InvalidDataException>(() => Parse("<rss>" + string.Concat(Enumerable.Repeat("<nested>", 70)) + string.Concat(Enumerable.Repeat("</nested>", 70)) + "</rss>")); });
        yield return ("Per-feed timeouts leave other feeds usable", () =>
        {
            using var server = new FeedServer((request, _) => request.RequestUri!.AbsolutePath == "/timeout" ? throw new TaskCanceledException() : Task.FromResult(Xml(Rss)));
            using var client = new HttpClient(server);
            var result = new DeveloperFeeds(client).Refresh([new("Slow", "https://example.test/timeout"), new("Good", "https://example.test/rss")], default).GetAwaiter().GetResult();
            Require(result.Succeeded == 1 && result.Entries.Count == 1 && result.Failures.Single().Reason == "Request timed out.");
        });
    }
    private static DeveloperFeedResult Read(HttpClient client, string path, CancellationToken cancellation = default) => new DeveloperFeeds(client).Refresh([new("Test", "https://example.test" + path)], cancellation).GetAwaiter().GetResult();
    private static HttpResponseMessage Xml(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/xml") };
    private static HttpResponseMessage Redirect(string target) { var result = new HttpResponseMessage(HttpStatusCode.Redirect); result.Headers.Location = new(target, UriKind.RelativeOrAbsolute); return result; }
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Developer feed contract failed."); }
    private static void Expect<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private sealed class FeedServer(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        { Interlocked.Increment(ref Calls); Require(request.Method == HttpMethod.Get && request.Headers.UserAgent.ToString() == "Vela/0.2.2"); return respond(request, cancellation); }
    }
    private sealed class CountingStream(int length) : Stream
    {
        public int ReadBytes;
        public override int Read(byte[] buffer, int offset, int count) { int amount = Math.Min(count, length - ReadBytes); Array.Fill(buffer, (byte)'x', offset, amount); ReadBytes += amount; return amount; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) { token.ThrowIfCancellationRequested(); int amount = Math.Min(buffer.Length, length - ReadBytes); buffer.Span[..amount].Fill((byte)'x'); ReadBytes += amount; return ValueTask.FromResult(amount); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => ReadBytes; set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
