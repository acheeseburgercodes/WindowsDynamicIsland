using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DynamicIsland.UI;

public partial class AnimatedXIcon : UserControl
{
    public AnimatedXIcon()
    {
        InitializeComponent();
    }

    public void StartAnimation()
    {
        StopAnimation();
        FirstStroke.X2 = FirstStroke.X1;
        FirstStroke.Y2 = FirstStroke.Y1;
        FirstStroke.Opacity = 0;
        SecondStroke.X2 = SecondStroke.X1;
        SecondStroke.Y2 = SecondStroke.Y1;
        SecondStroke.Opacity = 0;

        DrawStroke(FirstStroke, 6, 18, TimeSpan.Zero);
        DrawStroke(SecondStroke, 18, 18, TimeSpan.FromMilliseconds(160));
    }

    public void StopAnimation()
    {
        ResetStroke(FirstStroke, 6, 18);
        ResetStroke(SecondStroke, 18, 18);
    }

    private static void DrawStroke(Line stroke, double x, double y, TimeSpan delay)
    {
        var duration = TimeSpan.FromMilliseconds(190);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        stroke.BeginAnimation(Line.X2Property, new DoubleAnimation(stroke.X1, x, duration)
        {
            BeginTime = delay,
            EasingFunction = ease
        });
        stroke.BeginAnimation(Line.Y2Property, new DoubleAnimation(stroke.Y1, y, duration)
        {
            BeginTime = delay,
            EasingFunction = ease
        });
        stroke.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            BeginTime = delay
        });
    }

    private static void ResetStroke(Line stroke, double x, double y)
    {
        stroke.BeginAnimation(Line.X2Property, null);
        stroke.BeginAnimation(Line.Y2Property, null);
        stroke.BeginAnimation(OpacityProperty, null);
        stroke.X2 = x;
        stroke.Y2 = y;
        stroke.Opacity = 1;
    }
}
