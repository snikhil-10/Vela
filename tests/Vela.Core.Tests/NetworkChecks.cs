using System.Net;
using Vela.Core;

internal static class NetworkChecks
{
    public static IEnumerable<(string Name, Action Check)> All()
    {
        yield return ("Speed tests respect transfer budgets and upload only generated data", () =>
        {
            using var handler = new MeasurementServer(); using var client = new HttpClient(handler);
            var result = new InternetSpeedTest(client).Run(CancellationToken.None).GetAwaiter().GetResult();
            Require(result.Download.Bytes == InternetSpeedTest.DownloadBudget && result.Upload.Bytes == InternetSpeedTest.UploadBudget);
            Require(handler.LatencyCalls == 5 && handler.DownloadOffered > result.Download.Bytes && handler.UploadReceived == result.Upload.Bytes);
            Require(result.LatencyMilliseconds >= 0 && double.IsFinite(result.Download.MegabitsPerSecond) && result.Download.MegabitsPerSecond > 0);
        });
        yield return ("A failed speed endpoint stops before any transfer", () =>
        {
            using var handler = new MeasurementServer { Fail = true }; using var client = new HttpClient(handler);
            Expect<HttpRequestException>(() => new InternetSpeedTest(client).Run(CancellationToken.None).GetAwaiter().GetResult());
            Require(handler.Calls == 1 && handler.UploadReceived == 0);
        });
        yield return ("A canceled speed test never starts a request", () =>
        {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            using var handler = new MeasurementServer(); using var client = new HttpClient(handler);
            Expect<OperationCanceledException>(() => new InternetSpeedTest(client).Run(cancel.Token).GetAwaiter().GetResult());
            Require(handler.Calls == 0);
        });
        yield return ("Canceling during download prevents the upload phase", () =>
        {
            using var cancel = new CancellationTokenSource();
            using var handler = new MeasurementServer { Cancel = cancel, CancelDownload = true }; using var client = new HttpClient(handler);
            Expect<OperationCanceledException>(() => new InternetSpeedTest(client).Run(cancel.Token).GetAwaiter().GetResult());
            Require(handler.DownloadCalls == 1 && handler.UploadReceived == 0);
        });
        yield return ("An empty speed response cannot produce a successful zero result", () =>
        {
            using var handler = new MeasurementServer { EmptyDownload = true }; using var client = new HttpClient(handler);
            Expect<HttpRequestException>(() => new InternetSpeedTest(client).Run(CancellationToken.None).GetAwaiter().GetResult());
            Require(handler.UploadReceived == 0);
        });
        yield return ("Canceling upload does not publish a completed speed result", () =>
        {
            using var cancel = new CancellationTokenSource();
            using var handler = new MeasurementServer { Cancel = cancel }; using var client = new HttpClient(handler);
            Expect<OperationCanceledException>(() => new InternetSpeedTest(client).Run(cancel.Token).GetAwaiter().GetResult());
            Require(handler.UploadReceived == 1_000_000);
        });
    }
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Network measurement contract failed."); }
    private static void Expect<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }

    private sealed class MeasurementServer : HttpMessageHandler
    {
        public bool Fail, EmptyDownload, CancelDownload;
        public CancellationTokenSource? Cancel;
        public int Calls, LatencyCalls, DownloadCalls;
        public long DownloadOffered, UploadReceived;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; token.ThrowIfCancellationRequested();
            Require(request.RequestUri is { Scheme: "https", Host: "speed.cloudflare.com" });
            Require(request.Headers.CacheControl?.NoCache == true && request.Headers.AcceptEncoding.Any(e => e.Value == "identity"));
            if (Fail) return new(HttpStatusCode.ServiceUnavailable);
            if (request.Method == HttpMethod.Post)
            {
                Require(request.RequestUri!.AbsolutePath == "/__up");
                byte[] data = await request.Content!.ReadAsByteArrayAsync(token);
                Require(data.Length <= 1_000_000 && data.All(b => b == 0)); UploadReceived += data.Length;
                Cancel?.Cancel(); token.ThrowIfCancellationRequested();
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            }
            Require(request.RequestUri!.AbsolutePath == "/__down");
            if (request.RequestUri.Query.StartsWith("?bytes=0&", StringComparison.Ordinal))
            { LatencyCalls++; return new(HttpStatusCode.OK) { Content = new ByteArrayContent([]) }; }
            DownloadCalls++;
            if (CancelDownload) { Cancel?.Cancel(); token.ThrowIfCancellationRequested(); }
            // Deliberately offer more than requested: the client must stop at its own budget.
            byte[] download = EmptyDownload ? [] : new byte[8_000_100]; DownloadOffered += download.Length;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(download) };
        }
    }
}
