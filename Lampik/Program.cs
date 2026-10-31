using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Orientation = System.Windows.Controls.Orientation;
using ColorConverter = System.Windows.Media.ColorConverter;
using Path = System.IO.Path;

namespace Lampik;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        application.Run(new LampikWindow());
    }
}

public sealed class KeyPreset
{
    public string Shortcut { get; set; } = "";
    public int Minutes { get; set; } = 30;
    public bool Enabled { get; set; } = true;
}
public sealed class Preferences
{
    public bool DarkTheme { get; set; } = true;
    public string Language { get; set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
    public bool AlwaysOnTop { get; set; } = true;
    public List<KeyPreset> Hotkeys { get; set; } = Defaults();
    public static List<KeyPreset> Defaults() => new()
    {
        new() { Shortcut = "Ctrl+Alt+1", Minutes = 1 },
        new() { Shortcut = "Ctrl+Alt+3", Minutes = 30 },
        new() { Shortcut = "Ctrl+Alt+6, Ctrl+Alt+0", Minutes = 60 }
    };
}

internal enum PowerAction { Shutdown, Restart, Sleep, Hibernate }
internal record PowerCommand(PowerAction Action, DateTime Target);

internal static class Commands
{
    private static readonly Dictionary<string, string> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["одну"] = "1", ["один"] = "1", ["одна"] = "1", ["две"] = "2", ["два"] = "2",
        ["три"] = "3", ["четыре"] = "4", ["пять"] = "5", ["шесть"] = "6",
        ["семь"] = "7", ["восемь"] = "8", ["девять"] = "9", ["десять"] = "10",
        ["пятнадцать"] = "15", ["двадцать"] = "20", ["тридцать"] = "30",
        ["сорок"] = "40", ["пятьдесят"] = "50", ["шестьдесят"] = "60",
        ["one"] = "1", ["two"] = "2", ["three"] = "3", ["four"] = "4",
        ["five"] = "5", ["six"] = "6", ["seven"] = "7", ["eight"] = "8",
        ["nine"] = "9", ["ten"] = "10", ["fifteen"] = "15", ["twenty"] = "20",
        ["thirty"] = "30", ["forty"] = "40", ["fifty"] = "50", ["sixty"] = "60",
    };

    public static bool Parse(string text, DateTime now, string language,
        out PowerCommand? command, out string response)
    {
        command = null;
        response = "";
        text = text.ToLowerInvariant().Replace('ё', 'е').Trim();
        if (Regex.IsMatch(text, @"отмен|сброс|стоп|не надо|\b(cancel|stop|abort)\b"))
        {
            response = "cancel";
            return true;
        }
        if (Regex.IsMatch(text, @"статус|сколько осталось|когда сработает|\b(status|time left|remaining)\b"))
        {
            response = "status";
            return true;
        }
        if (Regex.IsMatch(text, @"помощь|что умеешь|\b(help|commands)\b"))
        {
            response = "help";
            return true;
        }

        var action = Regex.IsMatch(text, @"перезагру|рестарт|\b(restart|reboot)\b") ? PowerAction.Restart :
            Regex.IsMatch(text, @"гибернац|гибернир|\b(hibernate|hibernation)\b") ? PowerAction.Hibernate :
            Regex.IsMatch(text, @"\b(сон|сна|sleep)\b|усып") ? PowerAction.Sleep :
            PowerAction.Shutdown;

        DateTime target;
        // "at 23:30", "в 23:30", "at 11:30 pm"
        var absolute = Regex.Match(text, @"(?<!\d)(?<h>[01]?\d|2[0-3]):(?<m>[0-5]\d)\s*(?<ampm>am|pm)?(?!\w)");
        var twelveHour = !absolute.Success ? Regex.Match(text, @"\b(?:at\s+)?(?<h>1[0-2]|0?[1-9])\s*(?<ampm>am|pm)\b") : Match.Empty;
        if (absolute.Success || twelveHour.Success)
        {
            var match = absolute.Success ? absolute : twelveHour;
            int hour = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
            int minute = match.Groups["m"].Success
                ? int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
            string ampm = match.Groups["ampm"].Value;
            if (ampm == "pm" && hour < 12) hour += 12;
            if (ampm == "am" && hour == 12) hour = 0;
            target = now.Date.AddHours(hour).AddMinutes(minute);
            if (Regex.IsMatch(text, @"завтра|\btomorrow\b"))
                target = target.AddDays(1);
            else if (target <= now)
            {
                if (Regex.IsMatch(text, @"сегодня|\btoday\b"))
                {
                    response = I18n.T(language, "timePast");
                    return false;
                }
                target = target.AddDays(1);
            }
        }
        else
        {
            // Convert words into numbers, including common half-hour expressions.
            text = Regex.Replace(text, @"полтора\s+часа", "1.5 час");
            text = Regex.Replace(text, @"полчаса|пол\s+часа|\bhalf\s+an?\s+hour\b", "0.5 hour");
            text = Regex.Replace(text, @"\b(an?|the)\s+(?=hours?|minutes?|seconds?\b)", "1 ");
            text = Regex.Replace(text, @"\bчерез\s+час\b", "через 1 час");
            text = Regex.Replace(text, @"\bчерез\s+минуту\b", "через 1 минуту");
            text = Regex.Replace(text, @"\b(" + string.Join("|", NumberWords.Keys.OrderByDescending(k => k.Length)) + @")\b",
                m => NumberWords[m.Value]);

            double seconds = 0;
            const string pattern = @"(?<n>\d+(?:[.,]\d+)?)\s*(?<u>час(?:ов|а)?|ч|минут(?:у|ы|а)?|мин|м|секунд(?:у|ы|а)?|сек|с|hours?|hrs?|h|minutes?|mins?|m|seconds?|secs?|s)\b";
            foreach (Match match in Regex.Matches(text, pattern))
            {
                var n = double.Parse(match.Groups["n"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                var unit = match.Groups["u"].Value;
                seconds += n * (
                    unit.StartsWith("час") || unit is "ч" or "h" || unit.StartsWith("hour") || unit.StartsWith("hr")
                        ? 3600 :
                    unit.StartsWith("м") || unit is "m" || unit.StartsWith("min")
                        ? 60 : 1);
            }
            if (seconds == 0)
            {
                var shorthand = Regex.Match(text, @"(?:через|\bin\b)\s+(\d+)\b");
                if (shorthand.Success) seconds = int.Parse(shorthand.Groups[1].Value, CultureInfo.InvariantCulture) * 60;
            }
            if (seconds < 10 || seconds > 30 * 24 * 3600)
            {
                response = I18n.T(language, seconds == 0 ? "prompt" : "range");
                return false;
            }
            target = now.AddSeconds(seconds);
        }

        command = new PowerCommand(action, target);
        return true;
    }
}

internal readonly record struct Stroke(uint Modifiers, uint Key);
internal static class KeySyntax
{
    private const uint Ctrl = 2, Alt = 1, Shift = 4, NoRepeat = 0x4000;
    public static bool Parse(string shortcut, out Stroke[] strokes)
    {
        var list = new List<Stroke>();
        foreach (var item in shortcut.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            uint mods = 0, key = 0;
            foreach (var part in item.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var t = part.ToLowerInvariant();
                if (t is "ctrl" or "control") mods |= Ctrl;
                else if (t == "alt") mods |= Alt;
                else if (t == "shift") mods |= Shift;
                else if (key == 0 && t.Length == 1 && char.IsAsciiLetterOrDigit(t[0])) key = char.ToUpperInvariant(t[0]);
                else if (key == 0 && t.Length >= 2 && t[0] == 'f' &&
                    int.TryParse(t[1..], out int f) && f >= 1 && f <= 12) key = (uint)(0x70 + f - 1);
                else { strokes = Array.Empty<Stroke>(); return false; }
            }
            if (mods == 0 || key == 0) { strokes = Array.Empty<Stroke>(); return false; }
            list.Add(new Stroke(mods | NoRepeat, key));
        }
        strokes = list.ToArray();
        return strokes.Length is >= 1 and <= 2;
    }
}

internal sealed class LampikWindow : Window
{
    const int WmHotkey = 0x0312, ShowId = 1001, CancelId = 1002, ChordId = 1003;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer chordTimer = new() { Interval = TimeSpan.FromMilliseconds(2500) };
    readonly Dictionary<int, (KeyPreset preset, Stroke[] strokes)> registered = new();
    readonly HashSet<int> registeredIds = new();
    Preferences prefs;
    Forms.NotifyIcon? tray;
    IntPtr handle;
    KeyPreset? pending;
    DateTime? target;
    PowerAction action;
    bool warned, exiting, mini;
    string message = "";
    TextBox? input;
    TextBlock? messageLabel, countdownLabel;
    Ellipse? halo;
    readonly string settingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lampik", "settings.json");

    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint key);
    [DllImport("user32.dll", SetLastError = true)] static extern bool UnregisterHotKey(IntPtr h, int id);

    public LampikWindow()
    {
        try { prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsFile)) ?? new Preferences(); }
        catch { prefs = new Preferences(); }
        prefs.Hotkeys ??= Preferences.Defaults();
        prefs.Language = I18n.Language(prefs.Language);
        message = T("question");
        Width = 300; Height = 324; MinWidth = MaxWidth = 300;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = prefs.AlwaysOnTop;
        Title = T("title");
        Rebuild();
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(HotkeyHook);
            RegisterHotkeys();
        };
        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 15;
            Top = wa.Bottom - Height - 15;
            input?.Focus();
        };
        Closing += OnClosing;
        timer.Tick += (_, _) => Tick();
        timer.Start();
        chordTimer.Tick += (_, _) => StopChord();
        InitTray();
        Refresh();
    }

    string T(string key, params object[] args) => I18n.T(prefs.Language, key, args);

    Brush Brush(string dark, string light) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(prefs.DarkTheme ? dark : light));

    static TextBlock Text(string value, double size = 14, FontWeight? weight = null) =>
        new() { Text = value, FontSize = size, FontWeight = weight ?? FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

    Button Button(string text, Action click, string tooltip = "")
    {
        var b = new Button
        {
            Content = text, Width = 31, Height = 29, Margin = new Thickness(3, 0, 0, 0),
            FontSize = 14, ToolTip = tooltip, Foreground = Brush("#eaeef6", "#344155"),
            Background = Brush("#273345", "#eef0f5"), BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        b.Click += (_, _) => click();
        return b;
    }

    void Rebuild()
    {
        Height = mini ? 148 : 324;
        var frame = new Border
        {
            Background = Brush("#101722", "#fafbfe"), BorderBrush = Brush("#2d3849", "#e1e6ee"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(19), Padding = new Thickness(12)
        };
        var stack = new StackPanel();
        frame.Child = stack;
        Content = frame;

        var header = new DockPanel { Height = 31 };
        header.MouseLeftButtonDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        var title = Text("●  " + T("appName"), 14, FontWeights.Bold);
        title.Foreground = Brush("#ffdb90", "#414a62");
        DockPanel.SetDock(title, Dock.Left);
        header.Children.Add(title);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        actions.Children.Add(Button(mini ? "▴" : "▾", () => { mini = !mini; Rebuild(); Refresh(); }, T("mini")));
        actions.Children.Add(Button("⚙", OpenSettings, T("settings")));
        actions.Children.Add(Button("×", HideToTray, T("hide")));
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        stack.Children.Add(header);

        if (!mini)
        {
            var picture = new Canvas { Width = 268, Height = 149, Margin = new Thickness(0, 3, 0, 0) };
            halo = new Ellipse { Width = 228, Height = 145, Opacity = 0.95,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(160, 255, 198, 102), Color.FromArgb(0, 255, 190, 80))
                { RadiusX = .5, RadiusY = .5 } };
            Canvas.SetLeft(halo, 20);
            picture.Children.Add(halo);
            var bulb = new Ellipse { Width = 102, Height = 102,
                Stroke = Brush("#f3ca79", "#9b8b6c"), StrokeThickness = 2,
                Fill = new LinearGradientBrush(Color.FromRgb(255, 246, 192),
                    Color.FromRgb(255, 202, 110), 90) };
            Canvas.SetLeft(bulb, 83); Canvas.SetTop(bulb, 12);
            picture.Children.Add(bulb);
            var shine = new Ellipse { Width = 20, Height = 10, Fill = Brushes.White, Opacity = .44 };
            Canvas.SetLeft(shine, 102); Canvas.SetTop(shine, 27); picture.Children.Add(shine);
            var eyeBrush = new SolidColorBrush(Color.FromRgb(60, 49, 45));
            foreach (int x in new[] { 113, 146 })
            {
                var eye = new Ellipse { Width = 8, Height = 11, Fill = eyeBrush };
                Canvas.SetLeft(eye, x); Canvas.SetTop(eye, 62); picture.Children.Add(eye);
            }
            var mouth = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 127,81 Q 134,90 141,81"),
                Stroke = eyeBrush, StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round
            };
            picture.Children.Add(mouth);
            var basePlate = new Border { Width = 49, Height = 22, CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(184, 190, 197)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(110, 119, 127)), BorderThickness = new Thickness(2) };
            Canvas.SetLeft(basePlate, 109); Canvas.SetTop(basePlate, 105);
            picture.Children.Add(basePlate);
            stack.Children.Add(picture);
            var breathing = new DoubleAnimation(.60, 1, TimeSpan.FromSeconds(2.4))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            halo.BeginAnimation(OpacityProperty, breathing);

            countdownLabel = Text("— : —", 22, FontWeights.Bold);
            countdownLabel.Foreground = Brush("#f5d392", "#5f6178");
            countdownLabel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            countdownLabel.Margin = new Thickness(0, -1, 0, 2);
            stack.Children.Add(countdownLabel);
        }
        else
        {
            countdownLabel = Text("— : —", 18, FontWeights.Bold);
            countdownLabel.Foreground = Brush("#f5d392", "#5f6178");
            countdownLabel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            countdownLabel.Margin = new Thickness(0, 6, 0, 4);
            stack.Children.Add(countdownLabel);
        }

        messageLabel = Text(message, mini ? 11 : 12);
        messageLabel.Foreground = Brush("#a8b6c8", "#646f83");
        messageLabel.TextAlignment = TextAlignment.Center;
        messageLabel.TextWrapping = TextWrapping.Wrap;
        messageLabel.TextTrimming = TextTrimming.None;
        messageLabel.Height = mini ? 29 : 36;
        messageLabel.Margin = new Thickness(0, 0, 0, 6);
        stack.Children.Add(messageLabel);

        if (!mini)
        {
            var bar = new Grid { Height = 38 };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            input = new TextBox
            {
                FontSize = 12, Height = 35, Padding = new Thickness(10, 8, 8, 0),
                Text = "", ToolTip = T("example"),
                Foreground = Brush("#f0f3fa", "#2b3445"),
                Background = Brush("#1d2938", "#eef1f6"),
                BorderBrush = Brush("#303f50", "#d5dce7"), BorderThickness = new Thickness(1)
            };
            input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Submit(); } };
            bar.Children.Add(input);
            var send = Button("↗", Submit, T("send"));
            send.Width = 37; send.Height = 35; send.Margin = new Thickness(6, 0, 0, 0);
            send.Background = Brush("#e8bb70", "#7570e7");
            send.Foreground = Brush("#222a35", "#ffffff");
            Grid.SetColumn(send, 1); bar.Children.Add(send);
            stack.Children.Add(bar);
        }
        else
        {
            var cancel = Button(T("cancelTimer"), () => CancelTimer(true));
            cancel.Width = 252; cancel.Height = 27; cancel.FontSize = 11;
            cancel.Margin = new Thickness(0);
            stack.Children.Add(cancel);
            input = null;
        }
        Refresh();
    }

    void Refresh()
    {
        if (messageLabel is not null) messageLabel.Text = message;
        if (countdownLabel is not null)
            countdownLabel.Text = target is null ? (mini ? T("idle") : "— : —")
                : TimeLeft(target.Value);
        if (tray is not null)
            tray.Text = target is null ? T("title")
                : T("appName") + " — " + TimeLeft(target.Value);
    }

    string TimeLeft(DateTime deadline)
    {
        var left = deadline - DateTime.Now;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        return left.TotalDays >= 1
            ? $"{(int)left.TotalDays}{(prefs.Language == "en" ? "d" : "д")} {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}"
            : $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
    }

    void Submit()
    {
        var text = input?.Text.Trim() ?? "";
        if (text.Length == 0) return;
        if (input is not null) input.Clear();
        if (!Commands.Parse(text, DateTime.Now, prefs.Language, out var command, out var reply))
        { message = reply; Refresh(); return; }
        if (reply == "cancel") { CancelTimer(true); return; }
        if (reply == "help")
        { message = T("help"); Refresh(); return; }
        if (reply == "status")
        { message = target is null ? T("noTimer") : T("statusScheduled", target.Value.ToString("HH:mm")); Refresh(); return; }
        if (command is not null) Schedule(command.Action, command.Target);
    }

    string Label(PowerAction a) => a switch
    {
        PowerAction.Restart => T("restart"), PowerAction.Sleep => T("sleep"),
        PowerAction.Hibernate => T("hibernate"), _ => T("shutdown")
    };

    void Schedule(PowerAction a, DateTime deadline)
    {
        action = a; target = deadline; warned = false;
        message = T("scheduled", Label(a), deadline.ToString("HH:mm:ss"));
        Refresh();
        WarnSoon();
    }

    void Tick()
    {
        if (target is DateTime deadline && DateTime.Now >= deadline)
        {
            target = null;
            if (DateTime.Now - deadline < TimeSpan.FromMinutes(2)) ExecutePowerAction(action);
            else message = T("late");
        }
        WarnSoon();
        Refresh();
    }

    void WarnSoon()
    {
        if (warned || target is not DateTime deadline || deadline - DateTime.Now > TimeSpan.FromMinutes(1)) return;
        warned = true;
        tray?.ShowBalloonTip(7000, T("soonTitle"),
            T("soonBody", Label(action)), Forms.ToolTipIcon.Warning);
    }

    void CancelTimer(bool tell)
    {
        bool active = target is not null;
        target = null; warned = false;
        if (tell) message = active ? T("cancelled") : T("cancelNoTimer");
        Refresh();
    }

    void ExecutePowerAction(PowerAction a)
    {
        try
        {
            if (a == PowerAction.Shutdown || a == PowerAction.Restart)
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = a == PowerAction.Shutdown ? "/s /t 0" : "/r /t 0",
                    UseShellExecute = false, CreateNoWindow = true
                });
                if (p is null) throw new InvalidOperationException(T("windowsRejected"));
            }
            else if (!Forms.Application.SetSuspendState(
                a == PowerAction.Sleep ? Forms.PowerState.Suspend : Forms.PowerState.Hibernate, false, false))
                throw new InvalidOperationException(T("powerUnavailable"));
            message = T("commandSent");
        }
        catch (Exception ex)
        {
            message = T("powerError", ex.Message);
            Restore();
            System.Windows.MessageBox.Show(ex.Message, T("winError"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void InitTray()
    {
        tray = new Forms.NotifyIcon { Text = T("title"),
            Icon = System.Drawing.SystemIcons.Information, Visible = true };
        UpdateTrayMenu();
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(Restore);
    }

    void UpdateTrayMenu()
    {
        if (tray is null) return;
        var old = tray.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(T("open"), null, (_, _) => Dispatcher.Invoke(Restore));
        menu.Items.Add(T("cancelTimer"), null, (_, _) => Dispatcher.Invoke(() => CancelTimer(true)));
        menu.Items.Add(T("settings"), null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("exit"), null, (_, _) => Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip = menu;
        old?.Dispose();
    }

    void Restore()
    {
        Show(); WindowState = WindowState.Normal; Activate(); input?.Focus();
    }

    void HideToTray()
    {
        Hide();
        if (target is not null)
            tray?.ShowBalloonTip(2400, T("trayActiveTitle"), T("trayActiveBody"), Forms.ToolTipIcon.Info);
    }

    void Quit()
    {
        if (target is not null && System.Windows.MessageBox.Show(
            T("quitConfirm"), T("appName"),
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        exiting = true; Close();
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; HideToTray(); return; }
        timer.Stop(); StopChord();
        foreach (int id in registeredIds) UnregisterHotKey(handle, id);
        tray?.Dispose();
    }

    void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
        File.WriteAllText(settingsFile, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
    }

    void OpenSettings()
    {
        Restore();
        var dialog = new Window
        {
            Title = T("settings") + " — " + T("appName"), Width = 410, Height = 505, Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush("#141d29", "#f5f6f9"),
            Foreground = Brush("#f5f6f8", "#222b39"),
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false
        };
        var root = new StackPanel { Margin = new Thickness(18) };
        dialog.Content = root;
        root.Children.Add(Text(T("language"), 16, FontWeights.Bold));
        var languages = new ComboBox
        {
            Width = 160, Height = 30, Margin = new Thickness(0, 7, 0, 14),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            ItemsSource = new[] { "Русский", "English" },
            SelectedIndex = prefs.Language == "en" ? 1 : 0
        };
        root.Children.Add(languages);
        root.Children.Add(Text(T("appearance"), 17, FontWeights.Bold));
        var dark = new CheckBox { Content = T("dark"),
            IsChecked = prefs.DarkTheme, Foreground = dialog.Foreground, Margin = new Thickness(0, 9, 0, 7) };
        root.Children.Add(dark);
        var top = new CheckBox { Content = T("onTop"),
            IsChecked = prefs.AlwaysOnTop, Foreground = dialog.Foreground, Margin = new Thickness(0, 0, 0, 12) };
        root.Children.Add(top);
        root.Children.Add(Text(T("hotkeys"), 17, FontWeights.Bold));
        var rows = new List<(TextBox shortcut, TextBox minutes, CheckBox enabled)>();
        var presetList = prefs.Hotkeys.Take(5).ToList();
        void AddRow(KeyPreset preset)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var enabled = new CheckBox { IsChecked = preset.Enabled, VerticalAlignment = VerticalAlignment.Center,
                Width = 25, Foreground = dialog.Foreground };
            var key = new TextBox { Width = 215, Text = preset.Shortcut, FontSize = 12,
                Padding = new Thickness(5) };
            var min = new TextBox { Width = 70, Text = preset.Minutes.ToString(CultureInfo.InvariantCulture),
                FontSize = 12, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(5),
                ToolTip = T("minutes") };
            line.Children.Add(enabled); line.Children.Add(key); line.Children.Add(min);
            rows.Add((key, min, enabled)); root.Children.Add(line);
        }
        foreach (var preset in presetList) AddRow(preset);
        var add = new Button { Content = T("addHotkey"), Width = 115, Margin = new Thickness(0, 9, 0, 6) };
        add.Click += (_, _) => { if (rows.Count < 5) { AddRow(new KeyPreset()); dialog.Height += 35; } };
        root.Children.Add(add);
        var info = Text(T("hotkeyHint"), 11);
        info.Foreground = dialog.Foreground; info.TextWrapping = TextWrapping.Wrap; info.TextTrimming = TextTrimming.None;
        root.Children.Add(info);
        var save = new Button { Content = T("save"), Margin = new Thickness(0, 13, 0, 0), Height = 32 };
        save.Click += (_, _) =>
        {
            var newPresets = new List<KeyPreset>();
            foreach (var row in rows)
            {
                if (!int.TryParse(row.minutes.Text, out int mins) || mins < 1 || mins > 43200
                    || !KeySyntax.Parse(row.shortcut.Text, out _))
                {
                    System.Windows.MessageBox.Show(T("invalidHotkey"),
                        T("appName"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                newPresets.Add(new KeyPreset { Shortcut = row.shortcut.Text, Minutes = mins,
                    Enabled = row.enabled.IsChecked == true });
            }
            bool languageChanged = prefs.Language != (languages.SelectedIndex == 1 ? "en" : "ru");
            prefs.Language = languages.SelectedIndex == 1 ? "en" : "ru";
            prefs.DarkTheme = dark.IsChecked == true;
            prefs.AlwaysOnTop = top.IsChecked == true;
            prefs.Hotkeys = newPresets;
            if (languageChanged)
                message = target is DateTime deadline ? T("scheduled", Label(action), deadline.ToString("HH:mm:ss")) : T("question");
            try { SaveSettings(); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, T("saveError"), MessageBoxButton.OK);
                return;
            }
            Topmost = prefs.AlwaysOnTop; Title = T("title");
            RegisterHotkeys(); UpdateTrayMenu(); Rebuild();
            dialog.DialogResult = true;
        };
        root.Children.Add(save);
        dialog.ShowDialog();
    }

    void RegisterHotkeys()
    {
        if (handle == IntPtr.Zero) return;
        StopChord();
        foreach (int id in registeredIds) UnregisterHotKey(handle, id);
        registeredIds.Clear(); registered.Clear();

        void Fixed(int id, string shortcut)
        {
            if (KeySyntax.Parse(shortcut, out var s) && RegisterHotKey(handle, id,
                s[0].Modifiers, s[0].Key)) registeredIds.Add(id);
        }
        Fixed(ShowId, "Ctrl+Alt+L");
        Fixed(CancelId, "Ctrl+Alt+X");
        for (int i = 0; i < prefs.Hotkeys.Count; i++)
        {
            var p = prefs.Hotkeys[i];
            if (!p.Enabled || !KeySyntax.Parse(p.Shortcut, out var s)) continue;
            int id = 2000 + i;
            if (RegisterHotKey(handle, id, s[0].Modifiers, s[0].Key))
            {
                registeredIds.Add(id); registered[id] = (p, s);
            }
        }
    }

    void StartChord(KeyPreset preset, Stroke second)
    {
        StopChord();
        if (!RegisterHotKey(handle, ChordId, second.Modifiers, second.Key))
        {
            message = T("chordBusy"); Refresh();
            return;
        }
        pending = preset; chordTimer.Start();
    }

    void StopChord()
    {
        chordTimer.Stop();
        if (pending is not null && handle != IntPtr.Zero) UnregisterHotKey(handle, ChordId);
        pending = null;
    }

    IntPtr HotkeyHook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        int id = wparam.ToInt32();
        if (id == ShowId)
        {
            if (IsVisible && IsActive) HideToTray(); else Restore();
        }
        else if (id == CancelId) CancelTimer(true);
        else if (id == ChordId && pending is not null)
        {
            var selected = pending; StopChord(); Schedule(PowerAction.Shutdown, DateTime.Now.AddMinutes(selected.Minutes));
        }
        else if (registered.TryGetValue(id, out var entry))
        {
            if (entry.strokes.Length == 2) StartChord(entry.preset, entry.strokes[1]);
            else Schedule(PowerAction.Shutdown, DateTime.Now.AddMinutes(entry.preset.Minutes));
        }
        else return IntPtr.Zero;
        handled = true; return IntPtr.Zero;
    }
}
