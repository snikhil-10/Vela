using Microsoft.UI.Xaml;
using Vela.Core;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private void InternetSpeed()
    {
        Add("A quick connection check", "Measures HTTP latency, download and upload throughput against Cloudflare. Single-connection estimates vary with Wi-Fi, VPNs, server distance and other traffic.");
        Add("Data use", "A test transfers up to 80 MB plus request overhead, and ends within 45 seconds. Cloudflare receives your public IP and test traffic. Only generated zero bytes are uploaded; Vela sends no files, clipboard content or results report.");
        var output = Ui.Text("Ready when you are."); Body.Children.Add(output);
        var run = AsyncButton("Start speed test", async () =>
        {
            if (conversion is not null) return;
            using var requestCancellation = new CancellationTokenSource(); conversion = requestCancellation;
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
            var progress = new Progress<string>(text => output.Text = text);
            try
            {
                var result = await new InternetSpeedTest(client).Run(requestCancellation.Token, progress);
                output.Text = $"Download  {result.Download.MegabitsPerSecond:F1} Mbps\nUpload  {result.Upload.MegabitsPerSecond:F1} Mbps\nHTTP latency  {result.LatencyMilliseconds:F0} ms\n\nMeasured {result.Download.Bytes / 1_000_000.0:F1} MB down and {result.Upload.Bytes / 1_000_000.0:F1} MB up. Results are kept only in this window.";
            }
            catch (OperationCanceledException)
            { output.Text = requestCancellation.IsCancellationRequested ? "Test canceled." : "The test reached its deadline. Check the connection and try again."; }
            catch (HttpRequestException ex)
            {
                string reason = ex.StatusCode is { } code ? $"Cloudflare returned HTTP {(int)code}." : ex.GetBaseException().Message;
                output.Text = $"The test could not complete. {reason}\nCheck the connection and try again.";
            }
            finally { conversion = null; }
        },waitOnExit:true);
        Body.Children.Add(run);
        Body.Children.Add(Ui.Button("Cancel test", () => conversion?.Cancel()));
        Body.Children.Add(Ui.Text("Closing this window or disabling the feature cancels the test. No background or scheduled tests run.", "VelaCaption"));
    }
}
