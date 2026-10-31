using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Effects;
using System.Windows.Input;
using System.Windows.Media;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;

namespace Lampik;

/// <summary>Shared lightweight glass-inspired controls without external dependencies.</summary>
internal static class GlassUI
{
    internal static readonly FontFamily Font = new("Segoe UI");

    internal static Color Color(string value) =>
        (Color)System.Windows.Media.ColorConverter.ConvertFromString(value);

    internal static SolidColorBrush Solid(string value) => new(Color(value));

    internal static LinearGradientBrush Gradient(string a, string b, double angle = 108)
        => new(Color(a), Color(b), angle);

    internal static Brush MainBackground(bool dark) => dark
        ? Gradient("#192338", "#0A101E", 130)
        : Gradient("#FFFFFF", "#EAEFF9", 130);

    internal static Brush PanelBackground(bool dark) => dark
        ? Gradient("#2A3A52", "#1B273B", 132)
        : Gradient("#FFFFFF", "#F5F8FE", 132);

    internal static Brush PanelBorder(bool dark) => dark
        ? Gradient("#60718B", "#29364B", 140)
        : Gradient("#D0DBEA", "#E5EAF3", 140);

    internal static Brush ButtonBackground(bool dark) => dark
        ? Gradient("#34445F", "#23324A", 112)
        : Gradient("#F8FAFE", "#E6EDF7", 112);

    internal static Brush AccentBackground() => Gradient("#FFE09C", "#E9A965", 125);
    internal static Brush AccentBorder() => Gradient("#FFF4CF", "#C99053", 132);
    internal static Brush TextBrush(bool dark) => Solid(dark ? "#EEF3FF" : "#202D44");
    internal static Brush Muted(bool dark) => Solid(dark ? "#9FAFC7" : "#62758F");

    internal static Border Surface(bool dark, double radius = 17, bool shadow = false)
        => new()
        {
            Background = PanelBackground(dark),
            BorderBrush = PanelBorder(dark),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            SnapsToDevicePixels = true,
            Effect = shadow ? new DropShadowEffect
            {
                Color = Color("#000000"), Opacity = dark ? .21 : .09,
                BlurRadius = 18, ShadowDepth = 5
            } : null
        };

    internal static Border WindowSurface(bool dark, double radius = 23)
        => new()
        {
            Background = MainBackground(dark),
            BorderBrush = dark
                ? Gradient("#50617A", "#1A293E", 145)
                : Gradient("#D0DAEB", "#FFFFFF", 145),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Effect = new DropShadowEffect
            {
                Color = Color("#000000"), Opacity = dark ? .43 : .18,
                BlurRadius = 26, ShadowDepth = 10
            },
            SnapsToDevicePixels = true
        };

    internal static ControlTemplate RoundedTemplate(Type target, int radius)
    {
        var root = new FrameworkElementFactory(typeof(Border));
        root.Name = "shell";
        root.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        root.SetBinding(Border.BackgroundProperty, new Binding("Background")
        { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        root.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
        { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        root.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
        { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content")
        { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        content.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate")
        { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        root.AppendChild(content);
        var template = new ControlTemplate(target) { VisualTree = root };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, .88));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, .67));
        template.Triggers.Add(pressed);
        return template;
    }

    internal static Button Button(bool dark, string label, Action onClick,
        int width = 35, int height = 34, bool accent = false, int radius = 11)
    {
        var b = new Button
        {
            Width = width, Height = height, Content = label,
            FontFamily = Font, FontWeight = FontWeights.SemiBold,
            FontSize = 13, Cursor = Cursors.Hand, Focusable = false,
            Foreground = accent ? Solid("#33281B") : TextBrush(dark),
            Background = accent ? AccentBackground() : ButtonBackground(dark),
            BorderBrush = accent ? AccentBorder() : PanelBorder(dark),
            BorderThickness = new Thickness(1),
            Template = RoundedTemplate(typeof(Button), radius)
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    internal static TextBox Input(bool dark, string value = "", double fontSize = 12)
        => new()
        {
            Text = value,
            Height = 34, Padding = new Thickness(0, 0, 0, 0),
            FontFamily = Font, FontSize = fontSize,
            Foreground = TextBrush(dark), CaretBrush = Solid(dark ? "#FFD991" : "#8B613F"),
            SelectionBrush = Solid("#7F93AB"),
            BorderThickness = new Thickness(0), Background = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 15
        };

    internal static Border InputSurface(bool dark, TextBox input, double radius = 12)
    {
        var box = Surface(dark, radius);
        box.Padding = new Thickness(11, 0, 10, 0);
        box.Height = 39;
        box.Child = input;
        box.Background = dark
            ? Gradient("#111D2D", "#1B2940")
            : Gradient("#FFFFFF", "#F2F5FB");
        box.BorderBrush = dark ? Solid("#45536C") : Solid("#C9D6E7");
        input.GotKeyboardFocus += (_, _) => box.BorderBrush = AccentBorder();
        input.LostKeyboardFocus += (_, _) =>
            box.BorderBrush = dark ? Solid("#45536C") : Solid("#C9D6E7");
        return box;
    }

    internal static TextBlock Label(bool dark, string value, double size = 13,
        bool bold = false, bool muted = false)
        => new()
        {
            Text = value, FontFamily = Font, FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = muted ? Muted(dark) : TextBrush(dark),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

    internal static Border Chip(bool dark, string content)
    {
        var border = Surface(dark, 12);
        border.Padding = new Thickness(10, 6, 10, 6);
        border.Background = dark
            ? Gradient("#28364C", "#1B2C42")
            : Gradient("#F2F6FC", "#E5EDF8");
        border.Child = Label(dark, content, 11, false, true);
        return border;
    }

    internal static ToggleButton Toggle(bool dark, string label, bool selected, Action<bool> onClick,
        double width = 105)
    {
        ToggleButton? control = null;
        void Update()
        {
            if (control is null) return;
            bool active = control.IsChecked == true;
            control.Background = active ? AccentBackground() : ButtonBackground(dark);
            control.BorderBrush = active ? AccentBorder() : PanelBorder(dark);
            control.Foreground = active ? Solid("#372818") : TextBrush(dark);
        }
        control = new ToggleButton
        {
            Width = width, Height = 35, Content = label,
            IsChecked = selected,
            FontFamily = Font, FontSize = 12, FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand, Foreground = TextBrush(dark),
            BorderThickness = new Thickness(1),
            Template = RoundedTemplate(typeof(ToggleButton), 12)
        };
        control.Click += (_, _) =>
        {
            Update();
            onClick(control.IsChecked == true);
        };
        Update();
        return control;
    }

    internal static Border Divider(bool dark)
        => new()
        {
            Height = 1,
            Background = dark ? Solid("#37475B") : Solid("#DCE3EE"),
            Margin = new Thickness(0, 10, 0, 10)
        };
}
