using System.Windows;
using System.Windows.Media;

namespace VibeVocalStudio.Views;

public sealed class LevelMeter : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(nameof(Level), typeof(double), typeof(LevelMeter), new FrameworkPropertyMetadata(-60d, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly Brush Inactive = new SolidColorBrush(Color.FromRgb(222, 230, 237));
    private static readonly Brush Normal = new SolidColorBrush(Color.FromRgb(22, 135, 120));
    private static readonly Brush Warning = new SolidColorBrush(Color.FromRgb(192, 126, 22));
    private static readonly Brush Peak = new SolidColorBrush(Color.FromRgb(199, 62, 66));
    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        const int segments = 48;
        var width = Math.Max(0, (ActualWidth - (segments - 1) * 2) / segments);
        for (var segment = 0; segment < segments; segment++)
        {
            var db = -60 + (segment + 1) * 60d / segments;
            var brush = Level < db ? Inactive : db >= -3 ? Peak : db >= -12 ? Warning : Normal;
            context.DrawRoundedRectangle(brush, null, new Rect(segment * (width + 2), 0, width, ActualHeight), 1, 1);
        }
    }
}
