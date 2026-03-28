using System;
using System.Windows;

namespace EchoX.UI
{
    /// <summary>
    /// Crisp segmented level meter — renders pill-shaped bars via OnRender,
    /// pixel-snapped so there is zero blur.
    /// </summary>
    public class LevelMeterControl : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(LevelMeterControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(LevelMeterControl),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BarWidthProperty =
            DependencyProperty.Register(nameof(BarWidth), typeof(double), typeof(LevelMeterControl),
                new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BarGapProperty =
            DependencyProperty.Register(nameof(BarGap), typeof(double), typeof(LevelMeterControl),
                new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ActiveBrushProperty =
            DependencyProperty.Register(nameof(ActiveBrush), typeof(System.Windows.Media.SolidColorBrush), typeof(LevelMeterControl),
                new FrameworkPropertyMetadata(
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x53, 0xC0, 0x28)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty InactiveBrushProperty =
            DependencyProperty.Register(nameof(InactiveBrush), typeof(System.Windows.Media.SolidColorBrush), typeof(LevelMeterControl),
                new FrameworkPropertyMetadata(
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x24, 0x27, 0x2C)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double BarWidth
        {
            get => (double)GetValue(BarWidthProperty);
            set => SetValue(BarWidthProperty, value);
        }

        public double BarGap
        {
            get => (double)GetValue(BarGapProperty);
            set => SetValue(BarGapProperty, value);
        }

        public System.Windows.Media.SolidColorBrush ActiveBrush
        {
            get => (System.Windows.Media.SolidColorBrush)GetValue(ActiveBrushProperty);
            set => SetValue(ActiveBrushProperty, value);
        }

        public System.Windows.Media.SolidColorBrush InactiveBrush
        {
            get => (System.Windows.Media.SolidColorBrush)GetValue(InactiveBrushProperty);
            set => SetValue(InactiveBrushProperty, value);
        }

        protected override void OnRender(System.Windows.Media.DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double barW   = Math.Floor(BarWidth);
            double gap    = Math.Floor(BarGap);
            double step   = barW + gap;
            double radius = barW / 2.0; // full pill caps

            int totalBars = (int)Math.Floor((w + gap) / step);
            if (totalBars <= 0) return;

            double fraction = Math.Max(0, Math.Min(1, Value / Math.Max(1, Maximum)));
            int activeBars  = (int)Math.Round(fraction * totalBars);

            System.Windows.Media.SolidColorBrush active   = ActiveBrush;
            System.Windows.Media.SolidColorBrush inactive = InactiveBrush;

            for (int i = 0; i < totalBars; i++)
            {
                double x = i * step;
                System.Windows.Media.SolidColorBrush brush = i < activeBars ? active : inactive;
                dc.DrawRoundedRectangle(brush, null, new Rect(x, 0, barW, h), radius, radius);
            }
        }
    }
}
