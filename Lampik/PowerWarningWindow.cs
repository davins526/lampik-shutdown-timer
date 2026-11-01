using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Orientation = System.Windows.Controls.Orientation;

namespace Lampik;

/// <summary>A visible, nonmodal 60-second countdown with large Cancel and Snooze controls.</summary>
internal sealed class PowerWarningWindow : Window
{
    private readonly Action cancel;
    private readonly Action snooze;
    private readonly string language;
    private readonly TextBlock counter;
    private bool dismissing;

    private string T(string key, params object[] args) => I18n.T(language, key, args);

    public PowerWarningWindow(string language, PowerAction action, Action onCancel, Action onSnooze)
    {
        this.language = language;
        cancel = onCancel;
        snooze = onSnooze;
        Width = 340;
        Height = 204;
        MinWidth = MaxWidth = Width;
        MinHeight = MaxHeight = Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Title = T("soonTitle");

        var frame = GlassUI.WindowSurface(true, 22);
        frame.Padding = new Thickness(17, 14, 17, 13);
        Content = frame;
        var layout = new StackPanel();
        frame.Child = layout;

        var heading = GlassUI.Label(true, T("warningHeading"), 17, true);
        heading.Foreground = GlassUI.Solid("#FFDCA5");
        layout.Children.Add(heading);

        var actionName = action switch
        {
            PowerAction.Restart => "restart",
            PowerAction.Sleep => "sleep",
            PowerAction.Hibernate => "hibernate",
            _ => "shutdown"
        };
        var subtitle = GlassUI.Label(true, T("warningAction", T(actionName)), 12, false, true);
        subtitle.Margin = new Thickness(0, 4, 0, 7);
        layout.Children.Add(subtitle);

        counter = GlassUI.Label(true, "01:00", 27, true);
        counter.HorizontalAlignment = HorizontalAlignment.Center;
        counter.Foreground = GlassUI.Solid("#FFE4BA");
        layout.Children.Add(counter);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 13, 0, 0)
        };
        var cancelButton = GlassUI.Button(true, T("cancelTimer"), CancelNow, 174, 46, false, 14);
        cancelButton.Background = GlassUI.Gradient("#F3848C", "#D74662", 135);
        cancelButton.BorderBrush = GlassUI.Solid("#FFA6AF");
        cancelButton.Foreground = Brushes.White;
        cancelButton.FontSize = 14;
        buttons.Children.Add(cancelButton);

        var snoozeButton = GlassUI.Button(true, T("snoozeTen"), SnoozeNow, 117, 46, true, 14);
        snoozeButton.FontSize = 13;
        snoozeButton.Margin = new Thickness(9, 0, 0, 0);
        buttons.Children.Add(snoozeButton);
        layout.Children.Add(buttons);

        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 22;
        Top = work.Bottom - Height - 22;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { CancelNow(); e.Handled = true; }
        };
        Closed += (_, _) => { if (!dismissing) cancel(); };
    }

    public void UpdateRemaining(TimeSpan remaining)
    {
        int seconds = (int)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
        counter.Text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    public void Dismiss()
    {
        if (dismissing) return;
        dismissing = true;
        Close();
    }

    private void CancelNow()
    {
        Dismiss();
        cancel();
    }

    private void SnoozeNow()
    {
        Dismiss();
        snooze();
    }
}
