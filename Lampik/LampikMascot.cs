using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Lampik;

internal enum MascotMood { Idle, Listening, Happy, Scheduled, Warning, Drowsy }

internal static class MascotState
{
    public static MascotMood Resolve(DateTime now, DateTime? target, bool composing,
        DateTime lastInteraction, DateTime happyUntil)
    {
        if (target is DateTime when && when - now <= TimeSpan.FromMinutes(1))
            return MascotMood.Warning;
        if (now < happyUntil) return MascotMood.Happy;
        if (composing) return MascotMood.Listening;
        if (target is not null) return MascotMood.Scheduled;
        if (now - lastInteraction >= TimeSpan.FromSeconds(50)) return MascotMood.Drowsy;
        return MascotMood.Idle;
    }
}

/// <summary>
/// A fully vector, locally animated mascot. Uses no video, external images or network.
/// Animations are stopped when the window is hidden/unloaded.
/// </summary>
internal sealed class LampikMascot : Viewbox
{
    private readonly Canvas illustration = new() { Width = 220, Height = 145 };
    private readonly Canvas bulbParts = new() { Width = 220, Height = 145 };
    private readonly Ellipse outerGlow;
    private readonly Ellipse innerGlow;
    private readonly Ellipse leftEye;
    private readonly Ellipse rightEye;
    private readonly System.Windows.Shapes.Path sleepyEyes;
    private readonly System.Windows.Shapes.Path mouth;
    private readonly TranslateTransform floatTransform = new();
    private readonly DispatcherTimer blinkTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private MascotMood current = MascotMood.Idle;
    private bool awake;

    private static SolidColorBrush Paint(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    private static void Put(Canvas parent, UIElement child, double x, double y)
    {
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        parent.Children.Add(child);
    }

    public LampikMascot()
    {
        Stretch = Stretch.Uniform;
        StretchDirection = StretchDirection.DownOnly;
        SnapsToDevicePixels = true;
        Child = illustration;

        var gradient = new RadialGradientBrush();
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(192, 255, 195, 91), 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(48, 251, 165, 61), .48));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 174, 60), 1));
        outerGlow = new Ellipse { Width = 220, Height = 145, Fill = gradient };
        Put(illustration, outerGlow, 0, 0);

        var gradient2 = new RadialGradientBrush();
        gradient2.GradientStops.Add(new GradientStop(Color.FromArgb(155, 255, 236, 165), 0));
        gradient2.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 215, 112), 1));
        innerGlow = new Ellipse { Width = 130, Height = 119, Fill = gradient2 };
        Put(illustration, innerGlow, 45, 11);

        bulbParts.RenderTransform = floatTransform;
        illustration.Children.Add(bulbParts);

        var bulbBody = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 110,21 C 79,21 62,45 67,72 C 71,91 85,99 89,109 L 131,109 C 135,99 149,91 153,72 C 158,45 141,21 110,21 Z"),
            Fill = new LinearGradientBrush(
                Color.FromRgb(255, 251, 204), Color.FromRgb(255, 197, 106), 90),
            Stroke = Paint(225, 161, 88), StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        bulbParts.Children.Add(bulbBody);

        var highlight = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 87,39 Q 77,48 78,62"),
            Stroke = new SolidColorBrush(Color.FromArgb(188, 255, 255, 255)),
            StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        bulbParts.Children.Add(highlight);

        var blush = new SolidColorBrush(Color.FromArgb(120, 243, 145, 134));
        Put(bulbParts, new Ellipse { Width = 14, Height = 7, Fill = blush }, 82, 83);
        Put(bulbParts, new Ellipse { Width = 14, Height = 7, Fill = blush }, 124, 83);

        var ink = Paint(66, 48, 47);
        leftEye = new Ellipse { Width = 7, Height = 10, Fill = ink, RenderTransformOrigin = new Point(.5, .5),
            RenderTransform = new ScaleTransform() };
        rightEye = new Ellipse { Width = 7, Height = 10, Fill = ink, RenderTransformOrigin = new Point(.5, .5),
            RenderTransform = new ScaleTransform() };
        Put(bulbParts, leftEye, 92, 68);
        Put(bulbParts, rightEye, 121, 68);

        sleepyEyes = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 90,75 Q 96,68 102,75 M 118,75 Q 124,68 130,75"),
            Stroke = ink, StrokeThickness = 2.5,
            StrokeStartLineCap = PenLineCap.Round, Visibility = Visibility.Collapsed
        };
        bulbParts.Children.Add(sleepyEyes);

        mouth = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 105,85 Q 110,91 115,85"),
            Stroke = ink, StrokeThickness = 2.5,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
        bulbParts.Children.Add(mouth);

        var cap = new Border
        {
            Width = 46, Height = 21, CornerRadius = new CornerRadius(6),
            Background = Paint(186, 193, 200), BorderBrush = Paint(128, 135, 142),
            BorderThickness = new Thickness(2)
        };
        Put(bulbParts, cap, 87, 105);
        Put(bulbParts, new Rectangle { Width = 33, Height = 1.6, Fill = Paint(120, 128, 139) }, 93, 113);
        Put(bulbParts, new Rectangle { Width = 33, Height = 1.6, Fill = Paint(120, 128, 139) }, 93, 119);

        blinkTimer.Tick += (_, _) => Blink();
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
    }

    private void Start()
    {
        if (awake) return;
        awake = true;
        blinkTimer.Start();
        var floating = new DoubleAnimation(-2.5, 2.5, TimeSpan.FromSeconds(2.8))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
          EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        floatTransform.BeginAnimation(TranslateTransform.YProperty, floating);
        AnimateGlow();
    }

    private void Stop()
    {
        awake = false;
        blinkTimer.Stop();
        floatTransform.BeginAnimation(TranslateTransform.YProperty, null);
        outerGlow.BeginAnimation(OpacityProperty, null);
        innerGlow.BeginAnimation(OpacityProperty, null);
        foreach (var eye in new[] { leftEye, rightEye })
            if (eye.RenderTransform is ScaleTransform t)
                t.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private void Blink()
    {
        if (current == MascotMood.Drowsy) return;
        var closing = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(240) };
        closing.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        closing.KeyFrames.Add(new EasingDoubleKeyFrame(.06,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(105))));
        closing.KeyFrames.Add(new EasingDoubleKeyFrame(1,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
        foreach (var eye in new[] { leftEye, rightEye })
            if (eye.RenderTransform is ScaleTransform t)
                t.BeginAnimation(ScaleTransform.ScaleYProperty, closing);
    }

    private void AnimateGlow()
    {
        if (!awake) return;
        var slow = current is MascotMood.Warning ? 0.52 :
                   current is MascotMood.Happy ? 1.0 : 2.8;
        var min = current is MascotMood.Warning ? .53 :
                  current is MascotMood.Drowsy ? .19 : .43;
        var max = current is MascotMood.Warning ? 1.0 :
                  current is MascotMood.Drowsy ? .37 : .83;
        outerGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(min, max, TimeSpan.FromSeconds(slow))
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        innerGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(min * .7, max, TimeSpan.FromSeconds(slow * 1.18))
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever
        });
    }

    public void SetMood(MascotMood mood)
    {
        if (current == mood) return;
        current = mood;
        bool asleep = mood == MascotMood.Drowsy;
        sleepyEyes.Visibility = asleep ? Visibility.Visible : Visibility.Collapsed;
        leftEye.Visibility = asleep ? Visibility.Collapsed : Visibility.Visible;
        rightEye.Visibility = asleep ? Visibility.Collapsed : Visibility.Visible;
        mouth.Data = Geometry.Parse(mood switch
        {
            MascotMood.Happy => "M 102,83 Q 110,98 118,83",
            MascotMood.Listening => "M 107,85 A 3,4 0 1 0 113,85 A 3,4 0 1 0 107,85",
            MascotMood.Warning => "M 107,84 A 3,3 0 1 0 113,84 A 3,3 0 1 0 107,84",
            MascotMood.Drowsy => "M 107,85 Q 110,83 113,85",
            _ => "M 105,85 Q 110,91 115,85"
        });
        AnimateGlow();
    }
}
