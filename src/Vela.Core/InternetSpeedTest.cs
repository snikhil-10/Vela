using System.Diagnostics;
using System.Net.Http.Headers;

namespace Vela.Core;

public sealed record SpeedMeasurement(double MegabitsPerSecond, long Bytes, double Seconds);
public sealed record InternetSpeedResult(double LatencyMilliseconds, SpeedMeasurement Download, SpeedMeasurement Upload);

/// <summary>A bounded, single-connection HTTP measurement. Never sends user content or a results report.</summary>
public sealed class InternetSpeedTest(HttpClient client)
{
    public const long DownloadBudget = 64_000_000;
    public const long UploadBudget = 16_000_000;
    private const int DownloadChunk = 8_000_000, UploadChunk = 1_000_000;
    private static readonly Uri Server = new("https://speed.cloudflare.com/");

    public async Task<InternetSpeedResult> Run(CancellationToken cancellation, IProgress<string>? progress = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        progress?.Report("Measuring HTTP latency…");
        var latency = new List<double>();
        for (int i = 0; i < 5; i++)
        {
            deadline.Token.ThrowIfCancellationRequested();
            using var request = Request(HttpMethod.Get, "__down?bytes=0");
            var clock = Stopwatch.StartNew();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (i > 0) latency.Add(clock.Elapsed.TotalMilliseconds); // Exclude DNS/TLS warm-up.
        }
        latency.Sort();
        double median = (latency[1] + latency[2]) / 2;
        progress?.Report("Measuring download…");
        var download = await Transfer(false, deadline.Token).ConfigureAwait(false);
        progress?.Report($"Download {download.MegabitsPerSecond:F1} Mbps · measuring upload…");
        var upload = await Transfer(true, deadline.Token).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        return new(median, download, upload);
    }

    private async Task<SpeedMeasurement> Transfer(bool upload, CancellationToken cancellation)
    {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        phase.CancelAfter(TimeSpan.FromSeconds(5));
        long bytes = 0, budget = upload ? UploadBudget : DownloadBudget;
        byte[] buffer = new byte[64 * 1024];
        byte[]? payload = upload ? new byte[UploadChunk] : null;
        var clock = Stopwatch.StartNew();
        try
        {
            while (bytes < budget)
            {
                phase.Token.ThrowIfCancellationRequested();
                int chunk = (int)Math.Min(upload ? UploadChunk : DownloadChunk, budget - bytes);
                using var request = Request(upload ? HttpMethod.Post : HttpMethod.Get, upload ? "__up" : $"__down?bytes={chunk}");
                if (upload)
                {
                    request.Content = new ByteArrayContent(payload!, 0, chunk);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                }
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, phase.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (upload) bytes += chunk; // Count only requests acknowledged by the server.
                else
                {
                    using var stream = await response.Content.ReadAsStreamAsync(phase.Token).ConfigureAwait(false);
                    int received = 0;
                    while (received < chunk)
                    {
                        int count = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, chunk - received)), phase.Token).ConfigureAwait(false);
                        if (count == 0) break;
                        received += count; bytes += count;
                    }
                    if (received < chunk) throw new HttpRequestException("The speed-test server returned an incomplete measurement response.");
                }
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested && bytes > 0) { }
        cancellation.ThrowIfCancellationRequested();
        if (bytes == 0) throw new HttpRequestException("No data was transferred before the measurement deadline. Try again when the connection is available.");
        double seconds = Math.Max(clock.Elapsed.TotalSeconds, .000001);
        return new(bytes * 8 / seconds / 1_000_000, bytes, seconds);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(Server, path + (path.Contains('?') ? "&" : "?") + "nocache=" + Guid.NewGuid().ToString("N")));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        request.Headers.AcceptEncoding.ParseAdd("identity");
        return request;
    }
}
