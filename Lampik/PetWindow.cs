using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using Point = System.Windows.Point;

namespace Lampik;

// A native window region makes the empty desktop around the bulb click-through,
// including clicks destined for other processes (HTTRANSPARENT alone cannot do that).
internal sealed class PetWindow : Window
{
    readonly LampikMascot mascot = new() { Width = 220, Height = 145 };
    readonly Action open;
    readonly Action<double, double> savePosition;
    readonly ContextMenu menu = new();
    readonly MenuItem openItem = new(), cancelItem = new(), settingsItem = new(), exitItem = new();
    Point press;
    bool dragging;
    IntPtr hwnd;

    [DllImport("gdi32.dll")] static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int w, int h);
    [DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr dest, IntPtr first, IntPtr second, int mode);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

    public PetWindow(Preferences prefs, Action open, Action cancel, Action settings,
        Action quit, Action<double, double> savePosition)
    {
        this.open = open;
        this.savePosition = savePosition;
        Width = 220; Height = 145;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Title = "Lampik Pet";
        Content = mascot;
        Cursor = Cursors.Hand;
        openItem.Click += (_, _) => open();
        cancelItem.Click += (_, _) => cancel();
        settingsItem.Click += (_, _) => settings();
        exitItem.Click += (_, _) => quit();
        menu.Items.Add(openItem); menu.Items.Add(cancelItem); menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator()); menu.Items.Add(exitItem);
        ContextMenu = menu;
        MouseRightButtonUp += (_, e) =>
        {
            menu.PlacementTarget = this;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
            e.Handled = true;
        };
        UpdateLanguage(prefs.Language);
        var area = SystemParameters.WorkArea;
        Left = prefs.PetX is double x && double.IsFinite(x) ? x : area.Right - Width - 16;
        Top = prefs.PetY is double y && double.IsFinite(y) ? y : area.Bottom - Height - 16;
        ClampPosition();
        SourceInitialized += (_, _) => { hwnd = new WindowInteropHelper(this).Handle; ApplyRegion(); };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(ApplyRegion));
        MouseEnter += (_, _) => mascot.SetMood(MascotMood.Listening);
        MouseLeave += (_, _) => mascot.SetMood(mood);
        MouseLeftButtonDown += (_, e) =>
        {
            press = e.GetPosition(this);
            dragging = false;
        };
        MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || dragging) return;
            var delta = e.GetPosition(this) - press;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragging = true;
            DragMove();
            ClampPosition();
            savePosition(Left, Top);
        };
        MouseLeftButtonUp += (_, _) => { if (!dragging) this.open(); dragging = false; };
        IsVisibleChanged += (_, _) => { if (IsVisible) Resume(); else Pause(); };
        Closed += (_, _) => Pause();
    }

    MascotMood mood;
    public void SetMood(MascotMood value) { mood = value; mascot.SetMood(IsMouseOver ? MascotMood.Listening : value); }
    public void Pause() => mascot.PauseAnimations();
    public void Resume() => mascot.ResumeAnimations();
    public void UpdateLanguage(string language)
    {
        openItem.Header = I18n.T(language, "open");
        cancelItem.Header = I18n.T(language, "cancelTimer");
        settingsItem.Header = I18n.T(language, "settings");
        exitItem.Header = I18n.T(language, "exit");
    }

    void ClampPosition()
    {
        // Screen coordinates are device pixels; WPF positions are DIPs.
        var dpi = VisualTreeHelper.GetDpi(this);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(
            (int)((Left + Width / 2) * dpi.DpiScaleX), (int)((Top + Height / 2) * dpi.DpiScaleY)));
        var a = screen.WorkingArea;
        double l = a.Left / dpi.DpiScaleX, t = a.Top / dpi.DpiScaleY;
        Left = Math.Clamp(Left, l, Math.Max(l, a.Right / dpi.DpiScaleX - Width));
        Top = Math.Clamp(Top, t, Math.Max(t, a.Bottom / dpi.DpiScaleY - Height));
    }

    void ApplyRegion()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        int X(double x) => (int)Math.Round(x * dpi.DpiScaleX);
        int Y(double y) => (int)Math.Round(y * dpi.DpiScaleY);
        // Include a soft glow and the full floating animation range, without a rectangular input surface.
        var bulb = CreateEllipticRgn(X(55), Y(9), X(165), Y(116));
        var cap = CreateRoundRectRgn(X(83), Y(98), X(137), Y(132), X(12), Y(12));
        try
        {
            CombineRgn(bulb, bulb, cap, 2); // RGN_OR
            if (SetWindowRgn(hwnd, bulb, true) != 0) bulb = IntPtr.Zero; // Windows owns it now.
        }
        finally { if (bulb != IntPtr.Zero) DeleteObject(bulb); DeleteObject(cap); }
    }
}
