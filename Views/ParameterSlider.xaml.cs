using System.Windows;
using System.Windows.Controls;

namespace VibeVocalStudio.Views;

public partial class ParameterSlider : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ParameterSlider), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(ParameterSlider), new PropertyMetadata(""));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ParameterSlider), new PropertyMetadata("dB", RefreshDisplay));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(ParameterSlider), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, RefreshDisplay));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ParameterSlider), new PropertyMetadata(-12d));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ParameterSlider), new PropertyMetadata(12d));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(ParameterSlider), new PropertyMetadata(0.5d));
    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(nameof(DisplayValue), typeof(string), typeof(ParameterSlider), new PropertyMetadata("0 dB"));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string DisplayValue { get => (string)GetValue(DisplayValueProperty); private set => SetValue(DisplayValueProperty, value); }

    public ParameterSlider() => InitializeComponent();

    private static void RefreshDisplay(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (ParameterSlider)sender;
        control.DisplayValue = control.Unit == ":1" ? $"{control.Value:0.#}:1" : $"{control.Value:+0.#;-0.#;0} {control.Unit}";
    }
}
