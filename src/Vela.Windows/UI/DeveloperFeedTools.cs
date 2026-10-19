using System.Diagnostics;
using System.Net;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Vela.Core;

namespace Vela.Windows.UI;

public sealed partial class ToolWindow
{
    private void DeveloperFeedReader()
    {
        Body.Children.Add(Ui.Text("Add developer release feeds, then choose Refresh. This reads headlines only; it does not check installed versions or install updates."));
        var sources = new ListView { DisplayMemberPath = "Name", MaxHeight = 100, SelectionMode = ListViewSelectionMode.Single };
        AutomationProperties.SetName(sources, "Saved developer feeds"); Body.Children.Add(sources);
        var name = new TextBox { Header = "Feed name", MaxLength = 80 };
        var address = new TextBox { Header = "RSS or Atom URL", PlaceholderText = "https://developer.example/releases.atom", MaxLength = 2048 };
        Body.Children.Add(name); Body.Children.Add(address);
        var editActions = Ui.Stack(); editActions.Orientation = Orientation.Horizontal;
        void ShowSources() => sources.ItemsSource = controller.Settings.DeveloperFeeds.ToArray();
        sources.SelectionChanged += (_, _) =>
        {
            if (sources.SelectedItem is DeveloperFeed selected) { name.Text = selected.Name; address.Text = selected.Url; }
        };
        editActions.Children.Add(Ui.Button("Save feed", () =>
        {
            if (DeveloperFeed.Normalize([new(name.Text, address.Text)]) is not [var saved])
            { Message("Enter a feed name and an HTTP or HTTPS URL without embedded credentials.", true); return; }
            var selected = sources.SelectedItem as DeveloperFeed;
            if (controller.Settings.DeveloperFeeds.Any(f => f != selected && f.Url == saved.Url))
            { Message("That feed URL is already saved.", true); return; }
            if (selected is null && controller.Settings.DeveloperFeeds.Count >= DeveloperFeeds.MaximumSources)
            { Message("You can save up to 16 feeds.", true); return; }
            int index = selected is null ? -1 : controller.Settings.DeveloperFeeds.IndexOf(selected);
            if (index >= 0) controller.Settings.DeveloperFeeds[index] = saved;
            else controller.Settings.DeveloperFeeds.Add(saved);
            controller.Save(false); ShowSources(); name.Text = ""; address.Text = ""; Message("Feed saved. Choose Refresh to read it.");
        }));
        editActions.Children.Add(Ui.Button("New feed", () => { sources.SelectedItem = null; name.Text = ""; address.Text = ""; }));
        editActions.Children.Add(Ui.Button("Remove feed", () =>
        {
            if (sources.SelectedItem is not DeveloperFeed selected) { Message("Select a saved feed first.", true); return; }
            controller.Settings.DeveloperFeeds.Remove(selected); controller.Save(false); ShowSources(); name.Text = ""; address.Text = "";
            Message("Feed removed. Previously fetched headlines remain until the next refresh.");
        }));
        Body.Children.Add(editActions); ShowSources();
        Body.Children.Add(Ui.Text("Refresh contacts the addresses above and follows up to three HTTP(S) redirects. Servers receive your IP. Up to 16 feeds, 1 MiB per response and 100 headlines per feed; the newest 200 unique links are shown. No account cookies, background polling, article downloads or persistent headline cache.", "VelaCaption"));
        var summary = Ui.Text("No headlines fetched."); Body.Children.Add(summary);
        var actions = Ui.Stack(); actions.Orientation = Orientation.Horizontal;
        var cancel = Ui.Button("Cancel refresh", () => conversion?.Cancel()); cancel.IsEnabled = false;
        var filter = new TextBox { Header = "Find a release", PlaceholderText = "Search titles and feed names" };
        var entries = new ListView { DisplayMemberPath = "Label", MaxHeight = 260, SelectionMode = ListViewSelectionMode.Single };
        AutomationProperties.SetName(entries, "Release headlines");
        var link = new TextBox { Header = "Selected article URL", IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
        var selectedTitle = Ui.Text(""); selectedTitle.TextWrapping = TextWrapping.Wrap;
        var open = Ui.Button("Open selected article", () =>
        {
            if (entries.SelectedItem is DeveloperFeedEntry entry && DeveloperFeeds.TryWebUri(entry.Link.AbsoluteUri, out var target))
                Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true });
        }); open.IsEnabled = false;
        IReadOnlyList<DeveloperFeedEntry> headlines = [];
        void Filter() => entries.ItemsSource = headlines.Where(e => (e.Title + " " + e.Source).Contains(filter.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
        filter.TextChanged += (_, _) => Filter();
        entries.SelectionChanged += (_, _) => { selectedTitle.Text = (entries.SelectedItem as DeveloperFeedEntry)?.Label ?? ""; link.Text = (entries.SelectedItem as DeveloperFeedEntry)?.Link.AbsoluteUri ?? ""; open.IsEnabled = entries.SelectedItem is DeveloperFeedEntry; };
        var refresh = AsyncButton("Refresh feeds", async () =>
        {
            if (conversion is not null) return;
            var selected = controller.Settings.DeveloperFeeds.ToArray();
            if (selected.Length == 0) { Message("Save a feed before refreshing.", true); return; }
            using var cancellation = new CancellationTokenSource(); conversion = cancellation;
            sources.IsEnabled = name.IsEnabled = address.IsEnabled = false;
            foreach (var button in editActions.Children.OfType<Button>()) button.IsEnabled = false;
            cancel.IsEnabled = true;
            summary.Text = $"Refreshing {selected.Length} feed(s)… Previous headlines stay available until refresh completes.";
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false,
                UseDefaultCredentials = false, MaxResponseHeadersLength = 32,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli }) { Timeout = Timeout.InfiniteTimeSpan };
            try
            {
                var result = await new DeveloperFeeds(client).Refresh(selected, cancellation.Token);
                headlines = result.Entries; Filter();
                summary.Text = $"{headlines.Count} headline(s) · {result.Succeeded}/{result.Total} feeds read · {DateTime.Now:t}";
                if (result.Failures.Count > 0) Message(string.Join("\n", result.Failures.Select(f => $"{f.Source}: {f.Reason}")), true);
                else if (headlines.Count == 0) Message("Feeds read successfully. No entries with usable web links were found.");
            }
            catch (OperationCanceledException) { summary.Text = "Refresh canceled. Previous headlines kept."; }
            finally
            {
                conversion = null; cancel.IsEnabled = false;
                sources.IsEnabled = name.IsEnabled = address.IsEnabled = true;
                foreach (var button in editActions.Children.OfType<Button>()) button.IsEnabled = true;
            }
        }, waitOnExit: true);
        actions.Children.Add(refresh); actions.Children.Add(cancel); Body.Children.Add(actions);
        Body.Children.Add(filter); Body.Children.Add(entries); Body.Children.Add(selectedTitle); Body.Children.Add(link); Body.Children.Add(open);
    }
}
