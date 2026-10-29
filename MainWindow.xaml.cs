using System.Windows;
using VibeVocalStudio.ViewModels;

namespace VibeVocalStudio;

public partial class MainWindow : Window
{
    public MainWindow(StudioViewModel? model = null)
    {
        InitializeComponent();
        DataContext = model ?? new StudioViewModel();
        Closed += (_, _) => ((StudioViewModel)DataContext).Dispose();
    }
}
