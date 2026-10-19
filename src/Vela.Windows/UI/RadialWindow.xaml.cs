using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Vela.Core;
using Windows.System;
using Windows.UI.Core;

namespace Vela.Windows.UI;

public sealed partial class RadialWindow : Window
{
    internal const int WidthDips = 464, HeightDips = 552;
    private readonly AppController controller;
    private readonly LauncherNavigation menu;
    private readonly List<Button> wheelButtons = [], listButtons = [];
    private bool listMode, closed;

    internal RadialWindow(AppController controller)
    {
        this.controller = controller;
        menu = new(controller.Settings.CurrentLaunchers);
        InitializeComponent(); WindowStyle.Configure(this, WidthDips, HeightDips, true);
        Profile.Text = controller.Settings.ActiveLauncherProfile;
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated) { if (!closed) Close(); }
            else DispatcherQueue.TryEnqueue(FocusSelected);
        };
        Closed += (_, _) => closed = true;
        BackButton.Click += (_, _) => Back();
        ManageButton.Click += (_, _) =>
        {
            Close(); controller.EditLaunchers();
        };
        Root.PreviewKeyDown += Keys;
        Root.Loaded += (_, _) => FocusSelected();
        MenuArea.SizeChanged += (_, _) =>
        {
            bool compact = MenuArea.ActualWidth < 408 || MenuArea.ActualHeight < 368;
            if (compact == listMode) return;
            listMode = compact; Wheel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            ListScroll.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
            DispatcherQueue.TryEnqueue(FocusSelected);
        };
        Render();
    }

    private void Render()
    {
        Wheel.Children.Clear(); MenuList.Children.Clear(); wheelButtons.Clear(); listButtons.Clear();
        BackButton.IsEnabled = menu.CanGoBack;
        Heading.Text = menu.Folders.LastOrDefault() ?? "Launchers";
        ToolTipService.SetToolTip(Heading, string.Join(" › ", menu.Folders.Prepend(Profile.Text)));
        Error.Visibility = Visibility.Collapsed;
        var center = Ui.Button(menu.CanGoBack ? "Back" : "Open Vela", () =>
        {
            if (menu.CanGoBack) Back();
            else { Close(); controller.ShowPanel(); }
        }, true);
        center.Width = 120; center.Height = 64;
        Canvas.SetLeft(center, 144); Canvas.SetTop(center, 152); Wheel.Children.Add(center);
        for (int i = 0; i < menu.Items.Count; i++)
        {
            int index = i;
            Button ItemButton(bool compact)
            {
                var item = menu.Items[index];
                var button = Ui.Button(item.Name, () => Activate(index));
                button.Content = new TextBlock
                {
                    Text = $"{index + 1} · {item.Name}" + (item.IsFolder ? " ›" : ""),
                    Style = Ui.Style("BodyTextBlockStyle"), TextWrapping = TextWrapping.Wrap,
                    TextAlignment = compact ? TextAlignment.Left : TextAlignment.Center,
                    MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis
                };
                AutomationProperties.SetName(button, $"{index + 1}. {item.Name}" + (item.IsFolder ? ", submenu" : ""));
                AutomationProperties.SetAcceleratorKey(button, (index + 1).ToString());
                ToolTipService.SetToolTip(button, item.Name + (item.IsFolder ? " — open submenu" : "") + $" ({index + 1})");
                button.Padding = new Thickness(6); button.MinHeight = 48;
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                button.HorizontalContentAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
                return button;
            }
            var wheel = ItemButton(false); wheel.Width = 100; wheel.Height = 60;
            double angle = -Math.PI / 2 + i * 2 * Math.PI / menu.Items.Count;
            Canvas.SetLeft(wheel, 154 + Math.Cos(angle) * 150);
            Canvas.SetTop(wheel, 154 + Math.Sin(angle) * 150);
            Wheel.Children.Add(wheel); wheelButtons.Add(wheel);
            var row = ItemButton(true); MenuList.Children.Add(row); listButtons.Add(row);
        }
        if (menu.Items.Count == 0)
        {
            const string hint = "This menu is empty. Use Manage launchers to add items.";
            var text = Ui.Text(hint, "VelaCaption"); text.Width = 260; text.TextWrapping = TextWrapping.Wrap; text.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(text, 74); Canvas.SetTop(text, 248); Wheel.Children.Add(text);
            var empty = Ui.Text(hint, "VelaCaption"); empty.TextWrapping = TextWrapping.Wrap;
            MenuList.Children.Add(empty);
        }
        DispatcherQueue.TryEnqueue(FocusSelected);
    }

    private void FocusSelected()
    {
        if (closed || !Root.IsLoaded) return;
        var buttons = listMode ? listButtons : wheelButtons;
        if (menu.SelectedIndex >= 0 && menu.SelectedIndex < buttons.Count)
        {
            buttons[menu.SelectedIndex].Focus(FocusState.Keyboard);
            if (listMode) buttons[menu.SelectedIndex].StartBringIntoView();
        }
        else if (menu.CanGoBack) BackButton.Focus(FocusState.Keyboard);
        else ManageButton.Focus(FocusState.Keyboard);
    }
    private void Back() { if (menu.Back()) Render(); }
    private void Activate(int index)
    {
        if (index < 0 || index >= menu.Items.Count || closed || controller.Quitting) return;
        menu.Select(index);
        if (menu.Enter(index)) { Render(); return; }
        try { ToolWindow.Launch(menu.Items[index]); Close(); }
        catch (Exception)
        {
            Error.Text = $"Could not open “{menu.Items[index].Name}”. Check its saved path or website and access permissions in Manage launchers.";
            Error.Visibility = Visibility.Visible; FocusSelected();
        }
    }
    private void Keys(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape) { e.Handled = true; Close(); return; }
        bool Down(VirtualKey key) => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
        if (Down(VirtualKey.Control) || Down(VirtualKey.Menu) || Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows)) return;
        int number = e.Key is >= VirtualKey.Number1 and <= VirtualKey.Number8 ? (int)e.Key - (int)VirtualKey.Number1
            : e.Key is >= VirtualKey.NumberPad1 and <= VirtualKey.NumberPad8 ? (int)e.Key - (int)VirtualKey.NumberPad1 : -1;
        if (number >= 0) { e.Handled = true; if (!e.KeyStatus.WasKeyDown) Activate(number); return; }
        var visibleButtons = listMode ? listButtons : wheelButtons;
        if (FocusManager.GetFocusedElement(Root.XamlRoot) is Button current)
        {
            int focusedIndex = visibleButtons.IndexOf(current);
            if (focusedIndex >= 0) menu.Select(focusedIndex);
        }
        switch (e.Key)
        {
            case VirtualKey.Down: menu.Move(1); break;
            case VirtualKey.Up: menu.Move(-1); break;
            case VirtualKey.Home: menu.Select(0); break;
            case VirtualKey.End: menu.Select(menu.Items.Count - 1); break;
            case VirtualKey.Back:
            case VirtualKey.Left: e.Handled = true; Back(); return;
            case VirtualKey.Right:
                if (FocusManager.GetFocusedElement(Root.XamlRoot) is Button focused
                    && (listMode ? listButtons : wheelButtons).Contains(focused)
                    && menu.SelectedIndex >= 0 && menu.Items[menu.SelectedIndex].IsFolder)
                { e.Handled = true; if (!e.KeyStatus.WasKeyDown) Activate(menu.SelectedIndex); }
                return;
            default: return; // Native buttons retain Enter/Space and Tab behavior.
        }
        e.Handled = true; FocusSelected();
    }
}
