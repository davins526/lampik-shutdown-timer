using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;

namespace Lampik;

/// <summary>Compact settings dialog with independent visual hierarchy and RU/EN text.</summary>
internal sealed class LampikSettingsWindow : Window
{
    private readonly bool dark;
    private readonly Preferences original;
    private readonly List<(TextBox Shortcut, TextBox Minutes, ToggleButton Enabled)> hotkeyInputs = new();
    private readonly StackPanel hotkeyList = new();
    private bool languageEn;
    private bool darkEnabled;
    private bool alwaysOnTop;
    private bool startWithWindows;
    private Button? ruButton;
    private Button? enButton;
    private ToggleButton? darkToggle;
    private ToggleButton? topToggle;
    private TextBlock? error;
    public Preferences Result { get; private set; }

    private string T(string key, params object[] args)
        => I18n.T(languageEn ? "en" : "ru", key, args);

    private static TextBlock Caption(bool darkTheme, string text, double size, bool bold = false)
    {
        var t = GlassUI.Label(darkTheme, text, size, bold);
        t.TextTrimming = TextTrimming.CharacterEllipsis;
        return t;
    }

    public LampikSettingsWindow(Preferences current)
    {
        original = current;
        dark = current.DarkTheme;
        languageEn = current.Language == "en";
        darkEnabled = current.DarkTheme;
        alwaysOnTop = current.AlwaysOnTop;
        startWithWindows = current.StartWithWindows;
        Result = current;
        Title = T("settings") + " — Lampik";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.Manual;
        Width = 430;
        Height = 610;
        MinWidth = MaxWidth = 430;
        MinHeight = MaxHeight = 610;
        FontFamily = GlassUI.Font;
        try
        {
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri("pack://application:,,,/Assets/lampik.ico"));
        }
        catch (Exception) { /* Visual UI is independent of the icon resource. */ }

        var surface = GlassUI.WindowSurface(dark, 23);
        var root = new Grid { ClipToBounds = true };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.Child = root;
        Content = surface;

        var header = new Grid { Height = 78, Margin = new Thickness(19, 8, 19, 2) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var mark = new Border
        {
            Width = 44, Height = 44,
            CornerRadius = new CornerRadius(15),
            Background = GlassUI.AccentBackground(),
            BorderThickness = new Thickness(1), BorderBrush = GlassUI.AccentBorder(),
            Margin = new Thickness(0, 0, 12, 0)
        };
        mark.Child = Icon is not null
            ? new System.Windows.Controls.Image
            {
                Source = Icon, Width = 36, Height = 36,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
            : new TextBlock
            {
                Text = "✦", FontSize = 25, FontWeight = FontWeights.Bold,
                Foreground = GlassUI.Solid("#53391F"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        brand.Children.Add(mark);
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(Caption(dark, T("settings"), 21, true));
        title.Children.Add(GlassUI.Label(dark, "L A M P I K   /   P R E F E R E N C E S", 9, false, true));
        brand.Children.Add(title);
        header.Children.Add(brand);

        var close = GlassUI.Button(dark, "×", () => { DialogResult = false; Close(); }, 34, 34);
        close.FontSize = 20; close.Margin = new Thickness(8, 0, 0, 0);
        close.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(close, 1); header.Children.Add(close);
        root.Children.Add(header);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(19, 4, 13, 8),
            Focusable = false
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        var sections = new StackPanel { Margin = new Thickness(0, 3, 6, 8) };
        scroll.Content = sections;

        var appearance = GlassUI.Surface(dark, 19);
        appearance.Padding = new Thickness(16, 14, 16, 15);
        sections.Children.Add(appearance);
        var appearanceBody = new StackPanel();
        appearance.Child = appearanceBody;
        appearanceBody.Children.Add(Caption(dark, T("appearance"), 15, true));
        appearanceBody.Children.Add(new Border { Height = 6 });

        var languageLabel = GlassUI.Label(dark, T("language"), 12, false, true);
        appearanceBody.Children.Add(languageLabel);
        var languageRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 11)
        };
        ruButton = GlassUI.Button(dark, "Русский", () => SelectLanguage(false), 163, 35, false, 12);
        enButton = GlassUI.Button(dark, "English", () => SelectLanguage(true), 163, 35, false, 12);
        enButton.Margin = new Thickness(8, 0, 0, 0);
        languageRow.Children.Add(ruButton);
        languageRow.Children.Add(enButton);
        appearanceBody.Children.Add(languageRow);
        UpdateLanguageButtons();

        var themeRow = new Grid { Height = 41 };
        themeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        themeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        themeRow.Children.Add(GlassUI.Label(dark, T("dark"), 12));
        darkToggle = GlassUI.Toggle(dark, "", darkEnabled, value =>
        {
            darkEnabled = value;
            if (darkToggle is not null) darkToggle.Content = value ? "ON" : "OFF";
        }, 65);
        darkToggle.Content = darkEnabled ? "ON" : "OFF";
        Grid.SetColumn(darkToggle, 1);
        themeRow.Children.Add(darkToggle);
        appearanceBody.Children.Add(themeRow);
        appearanceBody.Children.Add(GlassUI.Divider(dark));

        var topRow = new Grid { Height = 38 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.Children.Add(GlassUI.Label(dark, T("onTop"), 12));
        topToggle = GlassUI.Toggle(dark, "", alwaysOnTop, value =>
        {
            alwaysOnTop = value;
            if (topToggle is not null) topToggle.Content = value ? "ON" : "OFF";
        }, 65);
        topToggle.Content = alwaysOnTop ? "ON" : "OFF";
        Grid.SetColumn(topToggle, 1);
        topRow.Children.Add(topToggle);
        appearanceBody.Children.Add(topRow);
        appearanceBody.Children.Add(GlassUI.Divider(dark));
        var startupRow = new Grid { Height = 42 };
        startupRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        startupRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        startupRow.Children.Add(GlassUI.Label(dark, T("autostart"), 12));
        var startupToggle = GlassUI.Toggle(dark, "", startWithWindows,
            value => startWithWindows = value, 65);
        startupToggle.Content = startWithWindows ? "ON" : "OFF";
        startupToggle.Click += (_, _) =>
            startupToggle.Content = startupToggle.IsChecked == true ? "ON" : "OFF";
        Grid.SetColumn(startupToggle, 1);
        startupRow.Children.Add(startupToggle);
        appearanceBody.Children.Add(startupRow);

        var shortcuts = GlassUI.Surface(dark, 19);
        shortcuts.Margin = new Thickness(0, 13, 0, 0);
        shortcuts.Padding = new Thickness(16, 14, 16, 13);
        sections.Children.Add(shortcuts);
        var hotkeySection = new StackPanel();
        shortcuts.Child = hotkeySection;
        hotkeySection.Children.Add(Caption(dark, T("hotkeys"), 15, true));
        var helper = GlassUI.Label(dark, T("hotkeyHint"), 11, false, true);
        helper.Margin = new Thickness(0, 5, 0, 11);
        hotkeySection.Children.Add(helper);

        var columns = new Grid { Margin = new Thickness(0, 1, 0, 6) };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
        var enabledHeader = GlassUI.Label(dark, "ON", 10, false, true);
        columns.Children.Add(enabledHeader);
        var shortcutHeader = GlassUI.Label(dark, T("shortcutColumn"), 10, false, true);
        Grid.SetColumn(shortcutHeader, 1); columns.Children.Add(shortcutHeader);
        var durationHeader = GlassUI.Label(dark, T("minutes"), 10, false, true);
        Grid.SetColumn(durationHeader, 2); columns.Children.Add(durationHeader);
        hotkeySection.Children.Add(columns);
        hotkeySection.Children.Add(hotkeyList);
        foreach (var preset in original.Hotkeys.Take(5)) AddHotkey(preset);

        var add = GlassUI.Button(dark, T("addHotkey"), () =>
        {
            if (hotkeyInputs.Count < 5) AddHotkey(new KeyPreset { Shortcut = "Ctrl+Alt+9" });
        }, 141, 34, false, 11);
        add.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        add.Margin = new Thickness(0, 12, 0, 0);
        hotkeySection.Children.Add(add);

        error = GlassUI.Label(dark, "", 11, false, true);
        error.Foreground = GlassUI.Solid("#FFB49E");
        error.Margin = new Thickness(2, 6, 0, 0);
        error.Visibility = Visibility.Collapsed;
        sections.Children.Add(error);

        var footer = new Grid { Height = 75, Margin = new Thickness(19, 0, 19, 8) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var footnote = GlassUI.Label(dark, T("settingsHint"), 10, false, true);
        footnote.Margin = new Thickness(0, 0, 8, 0);
        footer.Children.Add(footnote);
        var cancel = GlassUI.Button(dark, T("settingsCancel"), () =>
        {
            DialogResult = false; Close();
        }, 88, 40, false, 13);
        cancel.Margin = new Thickness(0, 0, 9, 0);
        Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
        var save = GlassUI.Button(dark, T("save"), Save, 106, 40, true, 13);
        Grid.SetColumn(save, 2); footer.Children.Add(save);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { DialogResult = false; Close(); e.Handled = true; }
        };
    }

    private void AddHotkey(KeyPreset preset)
    {
        var row = new Grid { Height = 43, Margin = new Thickness(0, 0, 0, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
        var toggle = GlassUI.Toggle(dark, preset.Enabled ? "✓" : "–", preset.Enabled,
            _ => { }, 41);
        toggle.Content = preset.Enabled ? "✓" : "–";
        toggle.Click += (_, _) => toggle.Content = toggle.IsChecked == true ? "✓" : "–";
        toggle.Height = 36;
        row.Children.Add(toggle);
        var shortcut = GlassUI.Input(dark, preset.Shortcut, 12);
        var shortcutShell = GlassUI.InputSurface(dark, shortcut, 11);
        shortcutShell.Height = 36;
        shortcutShell.Margin = new Thickness(0, 0, 7, 0);
        Grid.SetColumn(shortcutShell, 1);
        row.Children.Add(shortcutShell);
        var minutes = GlassUI.Input(dark, preset.Minutes.ToString(CultureInfo.InvariantCulture), 12);
        minutes.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
        var minuteShell = GlassUI.InputSurface(dark, minutes, 11);
        minuteShell.Height = 36;
        Grid.SetColumn(minuteShell, 2);
        row.Children.Add(minuteShell);
        hotkeyList.Children.Add(row);
        hotkeyInputs.Add((shortcut, minutes, toggle));
    }

    private void SelectLanguage(bool english)
    {
        languageEn = english;
        UpdateLanguageButtons();
    }

    private void UpdateLanguageButtons()
    {
        if (ruButton is null || enButton is null) return;
        foreach (var (b, selected) in new[] { (ruButton, !languageEn), (enButton, languageEn) })
        {
            b.Background = selected ? GlassUI.AccentBackground() : GlassUI.ButtonBackground(dark);
            b.BorderBrush = selected ? GlassUI.AccentBorder() : GlassUI.PanelBorder(dark);
            b.Foreground = selected ? GlassUI.Solid("#392816") : GlassUI.TextBrush(dark);
        }
    }

    private void Save()
    {
        var hotkeys = new List<KeyPreset>();
        foreach (var row in hotkeyInputs)
        {
            if (!KeySyntax.Parse(row.Shortcut.Text.Trim(), out _)
                || !int.TryParse(row.Minutes.Text.Trim(), out var minutes)
                || minutes is < 1 or > 43200)
            {
                if (error is not null)
                {
                    error.Text = T("invalidHotkey");
                    error.Visibility = Visibility.Visible;
                }
                return;
            }
            hotkeys.Add(new KeyPreset
            {
                Shortcut = row.Shortcut.Text.Trim(),
                Minutes = minutes,
                Enabled = row.Enabled.IsChecked == true
            });
        }
        var seen = new HashSet<Stroke>();
        KeySyntax.Parse("Ctrl+Alt+L", out var fixedOpen);
        KeySyntax.Parse("Ctrl+Alt+X", out var fixedCancel);
        seen.Add(fixedOpen[0]);
        seen.Add(fixedCancel[0]);
        foreach (var preset in hotkeys.Where(p => p.Enabled))
        {
            KeySyntax.Parse(preset.Shortcut, out var seq);
            if (!seen.Add(seq[0]) || (seq.Length == 2 && seen.Contains(seq[1])))
            {
                if (error is not null)
                {
                    error.Text = T("hotkeyConflict");
                    error.Visibility = Visibility.Visible;
                }
                return;
            }
        }
        Result = new Preferences
        {
            Language = languageEn ? "en" : "ru",
            DarkTheme = darkEnabled,
            AlwaysOnTop = alwaysOnTop,
            StartWithWindows = startWithWindows,
            Hotkeys = hotkeys,
            WindowX = original.WindowX,
            WindowY = original.WindowY,
            PetX = original.PetX,
            PetY = original.PetY
        };
        DialogResult = true;
        Close();
    }
}
