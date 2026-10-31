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
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
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
    System.Drawing.Icon? trayIcon;
    Stream? trayIconStream;
    IntPtr handle;
    KeyPreset? pending;
    DateTime? target;
    PowerAction action;
    bool warned, exiting, mini, quickMode;
    DateTime lastInteraction = DateTime.Now;
    DateTime happyUntil = DateTime.MinValue;
    LampikMascot? mascot;
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
        Width = 310; Height = 352; MinWidth = MaxWidth = 310;
        MinHeight = MaxHeight = 352;
        try
        {
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri("pack://application:,,,/Assets/lampik.ico"));
        }
        catch (Exception) { /* Window still works if the icon cannot be loaded. */ }
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
            Left = prefs.WindowX is double x && double.IsFinite(x)
                ? Math.Clamp(x, wa.Left, Math.Max(wa.Left, wa.Right - Width))
                : wa.Right - Width - 15;
            Top = prefs.WindowY is double y && double.IsFinite(y)
                ? Math.Clamp(y, wa.Top, Math.Max(wa.Top, wa.Bottom - Height))
                : wa.Bottom - Height - 15;
            FocusInput();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                HideToTray();
                e.Handled = true;
            }
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
        Height = mini ? 194 : 352;
        MinHeight = MaxHeight = Height;
        var surface = GlassUI.WindowSurface(prefs.DarkTheme, 23);
        surface.Padding = new Thickness(14, 12, 14, 12);
        var body = new StackPanel();
        surface.Child = body;
        Content = surface;
        var header = new Grid { Height = 36 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
                RememberPosition();
            }
        };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var brandBadge = new Border
        {
            Width = 32, Height = 32, Background = GlassUI.AccentBackground(),
            BorderBrush = GlassUI.AccentBorder(), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11), Margin = new Thickness(1, 0, 9, 0)
        };
        if (Icon is not null)
        {
            brandBadge.Child = new System.Windows.Controls.Image
            {
                Source = Icon, Width = 28, Height = 28, Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            brandBadge.Child = GlassUI.Label(prefs.DarkTheme, "✦", 21, true);
        }
        brand.Children.Add(brandBadge);
        var brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        brandText.Children.Add(GlassUI.Label(prefs.DarkTheme, T("appName"), 14, true));
        brandText.Children.Add(GlassUI.Label(prefs.DarkTheme, "POWER COMPANION", 8, false, true));
        brand.Children.Add(brandText);
        header.Children.Add(brand);

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        var compact = GlassUI.Button(prefs.DarkTheme, mini ? "▴" : "▾", ToggleMini, 29, 30);
        compact.ToolTip = T("mini"); controls.Children.Add(compact);
        var settingsButton = GlassUI.Button(prefs.DarkTheme, "⚙", OpenSettings, 29, 30);
        settingsButton.Margin = new Thickness(5, 0, 0, 0);
        settingsButton.ToolTip = T("settings"); controls.Children.Add(settingsButton);
        var close = GlassUI.Button(prefs.DarkTheme, "×", HideToTray, 29, 30);
        close.Margin = new Thickness(5, 0, 0, 0);
        close.ToolTip = T("hide"); close.FontSize = 19; controls.Children.Add(close);
        Grid.SetColumn(controls, 1); header.Children.Add(controls);
        body.Children.Add(header);

        if (!mini)
        {
            var stage = GlassUI.Surface(prefs.DarkTheme, 20);
            stage.Height = 142;
            stage.Margin = new Thickness(0, 10, 0, 7);
            stage.Background = prefs.DarkTheme
                ? GlassUI.Gradient("#202E46", "#111D30", 115)
                : GlassUI.Gradient("#F9FBFF", "#EDF3FC", 115);
            mascot = new LampikMascot { Width = 252, Height = 140 };
            stage.Child = mascot;
            body.Children.Add(stage);
        }
        else
        {
            var strip = GlassUI.Surface(prefs.DarkTheme, 17);
            strip.Height = 55;
            strip.Margin = new Thickness(0, 9, 0, 6);
            var grid = new Grid { Margin = new Thickness(6, 0, 11, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mascot = new LampikMascot { Width = 73, Height = 54 };
            grid.Children.Add(mascot);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            countdownLabel = GlassUI.Label(prefs.DarkTheme, "— : —", 19, true);
            countdownLabel.Foreground = GlassUI.Solid("#F9D797");
            info.Children.Add(countdownLabel);
            info.Children.Add(GlassUI.Label(prefs.DarkTheme, T("miniHint"), 10, false, true));
            Grid.SetColumn(info, 1); grid.Children.Add(info);
            strip.Child = grid;
            body.Children.Add(strip);
        }

        if (!mini)
        {
            var timerLine = new Grid { Height = 35, Margin = new Thickness(2, 0, 2, 0) };
            timerLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timerLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            countdownLabel = GlassUI.Label(prefs.DarkTheme, "— : —", 23, true);
            countdownLabel.Foreground = GlassUI.Solid("#FFDA95");
            timerLine.Children.Add(countdownLabel);
            var pill = GlassUI.Chip(prefs.DarkTheme, T("localChip"));
            pill.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(pill, 1); timerLine.Children.Add(pill);
            body.Children.Add(timerLine);
        }

        messageLabel = GlassUI.Label(prefs.DarkTheme, message, mini ? 11 : 12, false, true);
        messageLabel.TextAlignment = TextAlignment.Left;
        messageLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        messageLabel.TextWrapping = TextWrapping.Wrap;
        messageLabel.Height = mini ? 22 : 29;
        messageLabel.Margin = new Thickness(3, 1, 0, mini ? 5 : 7);
        body.Children.Add(messageLabel);

        var bar = GlassUI.Surface(prefs.DarkTheme, 15);
        bar.Height = 43;
        bar.Background = prefs.DarkTheme
            ? GlassUI.Gradient("#27394F", "#1A293D", 125)
            : GlassUI.Gradient("#FFFFFF", "#EFF4FB", 125);
        bar.Margin = new Thickness(0, 0, 0, 0);
        var entry = new Grid { Margin = new Thickness(10, 3, 4, 3) };
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var inputLayer = new Grid { Height = 35 };
        input = GlassUI.Input(prefs.DarkTheme, "", 12);
        input.Padding = new Thickness(5, 0, 3, 0);
        input.ToolTip = T("example");
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Submit(); }
        };
        var hint = GlassUI.Label(prefs.DarkTheme, T("inputPlaceholder"), 11, false, true);
        hint.Margin = new Thickness(5, 0, 0, 0);
        hint.IsHitTestVisible = false;
        input.TextChanged += (_, _) =>
        {
            hint.Visibility = string.IsNullOrEmpty(input.Text) ? Visibility.Visible : Visibility.Collapsed;
            lastInteraction = DateTime.Now;
            RefreshMascot();
        };
        inputLayer.Children.Add(input);
        inputLayer.Children.Add(hint);
        entry.Children.Add(inputLayer);
        var send = GlassUI.Button(prefs.DarkTheme, "↗", Submit, 35, 34, true, 12);
        send.FontSize = 18;
        send.ToolTip = T("send");
        Grid.SetColumn(send, 1); entry.Children.Add(send);
        bar.Child = entry;
        body.Children.Add(bar);
        Refresh();
    }

    void ToggleMini()
    {
        double previousBottom = Top + Height;
        mini = !mini;
        quickMode = false;
        Rebuild();
        var area = SystemParameters.WorkArea;
        Top = Math.Clamp(previousBottom - Height, area.Top, Math.Max(area.Top, area.Bottom - Height));
        FocusInput();
    }

    void RememberPosition()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top)) return;
        prefs.WindowX = Left;
        prefs.WindowY = Top;
        try { SaveSettings(); }
        catch (IOException) { /* Window remains movable when local settings are unavailable. */ }
        catch (UnauthorizedAccessException) { }
    }

    void FocusInput() => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (!IsVisible) return;
        input?.Focus();
        Keyboard.Focus(input);
    }), DispatcherPriority.Input);

    void ShowQuick()
    {
        // Ctrl+Alt+L is the single-action popup: show a small command bar at its last position.
        if (!mini)
        {
            double bottom = Top + Height;
            mini = true;
            Rebuild();
            var area = SystemParameters.WorkArea;
            Top = Math.Clamp(bottom - Height, area.Top, Math.Max(area.Top, area.Bottom - Height));
        }
        quickMode = true;
        Restore();
    }

    void RefreshMascot()
    {
        if (mascot is null) return;
        mascot.SetMood(MascotState.Resolve(DateTime.Now, target,
            input?.IsKeyboardFocusWithin == true && !string.IsNullOrWhiteSpace(input.Text),
            lastInteraction, happyUntil));
    }

    void Refresh()
    {
        if (messageLabel is not null) messageLabel.Text = message;
        if (countdownLabel is not null)
            countdownLabel.Text = target is null ? (mini ? T("idle") : "— : —")
                : TimeLeft(target.Value);
        if (tray is not null)
        {
            // NotifyIcon.Text cannot exceed 63 characters.
            var status = target is null ? T("title") : T("appName") + " — " + TimeLeft(target.Value);
            tray.Text = status.Length <= 63 ? status : status[..63];
        }
        RefreshMascot();
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
        if (command is not null)
        {
            Schedule(command.Action, command.Target);
            if (quickMode) HideToTray();
        }
    }

    string Label(PowerAction a) => a switch
    {
        PowerAction.Restart => T("restart"), PowerAction.Sleep => T("sleep"),
        PowerAction.Hibernate => T("hibernate"), _ => T("shutdown")
    };

    void Schedule(PowerAction a, DateTime deadline)
    {
        action = a; target = deadline; warned = false;
        happyUntil = DateTime.Now.AddSeconds(3);
        lastInteraction = DateTime.Now;
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
        try
        {
            var asset = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/lampik.ico"));
            if (asset is not null)
            {
                trayIconStream = asset.Stream;
                trayIcon = new System.Drawing.Icon(trayIconStream);
            }
        }
        catch (Exception) { /* Use the standard application icon if resource loading fails. */ }
        tray = new Forms.NotifyIcon
        {
            Text = T("title"),
            Icon = trayIcon ?? System.Drawing.SystemIcons.Application,
            Visible = true
        };
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
        Show();
        WindowState = WindowState.Normal;
        mascot?.ResumeAnimations();
        Activate();
        FocusInput();
    }

    void HideToTray()
    {
        quickMode = false;
        mascot?.PauseAnimations();
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
        trayIcon?.Dispose();
        trayIconStream?.Dispose();
    }

    void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
        File.WriteAllText(settingsFile, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
    }

    void OpenSettings()
    {
        Restore();
        var dialog = new LampikSettingsWindow(prefs) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var previousLanguage = prefs.Language;
        prefs = dialog.Result;
        prefs.Language = I18n.Language(prefs.Language);
        if (previousLanguage != prefs.Language)
        {
            message = target is DateTime active
                ? T("scheduled", Label(action), active.ToString("HH:mm:ss"))
                : T("question");
        }
        try { SaveSettings(); }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, T("saveError"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Topmost = prefs.AlwaysOnTop;
        Title = T("title");
        RegisterHotkeys();
        UpdateTrayMenu();
        Rebuild();
        FocusInput();
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
            if (IsVisible && IsActive) HideToTray(); else ShowQuick();
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
