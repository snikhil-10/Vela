namespace Vela.Core;

public sealed record FeatureDefinition(string Id, string Name, string Description, string Group,
    string Glyph, string Access = "Works on demand. No background access.");

public static class FeatureCatalog
{
    // The catalog is deliberately an allowlist. Windows-native and deferred features cannot be enabled.
    public static readonly IReadOnlyList<FeatureDefinition> All = [
        new("keepAwake", "Keep awake", "Stay awake for a while, or while your chosen apps are running.", "Everyday", "\uE708", "Temporarily requests system or display wakefulness. Never changes your power plan."),
        new("urlCleaner", "Clean links", "Remove tracking parameters before a link leaves your clipboard.", "Everyday", "\uE71B", "Automatic mode reads HTTP links on the clipboard. Nothing is sent online."),
        new("shelf", "File shelf", "A small place to park files, text, and links between tasks.", "Everyday", "\uE8B7", "Reads only items you drop. File references stay local; originals are never moved."),
        new("textSnippets", "Text snippets", "Type a short trigger to expand reusable text, dates, and times.", "Everyday", "\uE8D2", "Uses a keyboard hook for trigger matching. Recent characters stay in memory only. Expansion temporarily uses the clipboard and restores its formats unless a newer copy or clear occurs."),
        new("clipboardGuard", "Clipboard auto-clear", "Clear the current clipboard after a delay, on lock, or on sleep.", "Everyday", "\uE72E", "Watches clipboard changes. Windows clipboard history is managed separately by Windows."),
        new("pasteImage", "Save clipboard image", "Save a copied image as a PNG in a folder you choose.", "Everyday", "\uEB9F"),
        new("pastePlain", "Paste plain text", "Paste without formatting and restore your original clipboard afterward.", "Everyday", "\uE77F", "Reads clipboard formats only when you press its shortcut. Works in regular desktop apps; no history collection."),
        new("cleaningMode", "Cleaning mode", "Pause keyboard input, with an optional mouse lock, while you clean.", "Everyday", "\uE790", "Blocks keyboard and optionally mouse input for at most 60 seconds. Hold Esc for two seconds to release."),
        new("quickLauncher", "Quick panel", "Open your enabled tools from anywhere with one shortcut.", "Workspace", "\uE8A5", "Registers one global shortcut. It can be changed in Settings."),
        new("commandBar", "Command bar", "Search your enabled tools, saved launchers, and snippets.", "Workspace", "\uE721", "Searches Vela tools, launchers, snippets and enabled window/workspace tools. Does not index your disk or clipboard history."),
        new("radialMenu", "Radial menu", "Open grouped apps, files, and links near your pointer, using the mouse or keyboard.", "Workspace", "\uE80A", "Opens only the launchers and submenus you save. Uses a global shortcut. Small windows use a scrollable list."),
        new("windowSearch", "Find a window", "Search open window titles and filter them by display.", "Workspace", "\uE774", "Reads open window titles on demand. Does not capture previews, index files, or replace Alt+Tab."),
        new("savedLayouts", "Saved workspaces", "Save custom window positions and restore a workspace later.", "Workspace", "\uE8A9", "Stores process names, window titles and relative positions. Only moves windows already open; no app is launched or closed."),
        new("imagePins", "Image pins", "Keep copied screenshots and selected images above your work.", "Workspace", "\uE718", "Reads an image only when you choose Pin. Up to six previews stay in memory for this session; nothing is saved or uploaded."),
        new("smoothScroll", "Smooth wheel scrolling", "Give a traditional mouse wheel an adjustable, fluid glide.", "Input", "\uE74B", "Uses a mouse hook. Disable per app in Input settings. Precision touchpad events pass through."),
        new("scrollHorizontal", "Scroll sideways", "Hold Alt while scrolling to move horizontally.", "Input", "\uE8AB", "Uses a mouse hook and sends horizontal wheel events to the app under the pointer."),
        new("scrollInverter", "Reverse horizontal scrolling", "Reverse a mouse's horizontal wheel without changing vertical scrolling.", "Input", "\uE8AB", "Uses a mouse hook. Windows continues to manage vertical and touchpad scroll direction."),
        new("mouseButtonShortcuts", "Mouse button shortcuts", "Give the Back and Forward buttons your own key combinations.", "Input", "\uE962", "Uses a mouse hook. Native Back and Forward behavior is preserved until you assign a shortcut."),
        new("mouseClickDebounce", "Extra click filter", "Filter unusually fast duplicate clicks from worn mouse buttons.", "Input", "\uE8B0", "Uses a mouse hook. A conservative adjustable threshold preserves intentional double clicks."),
        new("superKey", "Super key", "Use Caps Lock as Ctrl + Alt + Shift + Windows. Tap it for Escape.", "Input", "\uE765", "Uses a keyboard hook. Stops immediately when disabled; no registry remapping."),
        new("quitWindowProtection", "Close protection", "Require a second press of Alt+F4 or Ctrl+W in selected apps.", "Input", "\uE72E", "Uses a keyboard hook only for the process names you select. Mouse closes remain unchanged."),
        new("monitorBrightness", "External monitor brightness", "Change hardware brightness on DDC/CI-capable external monitors.", "Display", "\uE706", "Queries connected monitors on demand. Only applies a brightness value when you choose Apply."),
        new("thermalAlerts", "GPU thermal alerts", "Receive sustained temperature alerts from supported NVIDIA driver sensors.", "System", "\uE9CA", "Reads driver sensor totals while enabled. No fan, voltage or clock settings are changed."),
        new("displayDimmer", "Extra dimming", "Dim individual displays below their hardware minimum.", "Display", "\uE706", "Uses click-through dark overlays. HDR content and secure desktops may behave differently."),
        new("resourceAlerts", "Resource alerts", "Get a quiet alert for sustained CPU, memory, or low disk space.", "System", "\uE7F4", "Samples resource totals every five seconds while enabled. No process or content logging."),
        new("micMute", "Mute all microphones", "Mute or unmute every active recording device together.", "Sound", "\uE720", "Uses Windows audio endpoint controls on demand. No microphone audio is recorded."),
        new("messagingDownloads", "Messaging download review", "Review older files in a messaging download folder before recycling them.", "Files", "\uE8B7", "Scans metadata only in your selected folder. Every removal requires selection and review; linked folders are skipped."),
        new("developerFeeds", "Developer feeds", "Read release headlines from the RSS or Atom feeds you choose.", "System", "\uE8A1", "Contacts your saved feed addresses only when you choose Refresh. No background polling, app scanning or package installation. Feed servers receive your IP; URLs are included in settings exports."),
        new("videoTools", "Batch video tools", "Convert to MP4, extract M4A audio, or make animated GIFs.", "Files", "\uE714", "Uses installed Windows codecs on selected files only. No uploads, downloads or edits to originals."),
        new("mediaTools", "Batch image tools", "Convert, resize, watermark, or extract text from selected images locally.", "Files", "\uE91B", "Reads only selected images and writes new copies. No uploads or edits to originals."),
        new("internetSpeed", "Internet speed test", "Measure latency, download and upload throughput on demand.", "System", "\uE774", "Connects to Cloudflare only when you start a test. Uses up to 80 MB plus overhead; no user content or results report is sent.")
    ];

    public static FeatureDefinition Get(string id) => All.First(f => f.Id == id);
    public static bool Contains(string id) => All.Any(f => f.Id == id);
    public static HashSet<string> Preset(string name) => name switch
    {
        "Everyday" => ["keepAwake", "urlCleaner", "shelf", "clipboardGuard", "pasteImage", "quickLauncher"],
        "Focused work" => ["keepAwake", "textSnippets", "quitWindowProtection", "commandBar", "quickLauncher"],
        "Minimal" => ["keepAwake", "urlCleaner", "quickLauncher"],
        _ => []
    };
}
