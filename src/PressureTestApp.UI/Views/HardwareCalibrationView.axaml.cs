using Avalonia.Controls;
using Avalonia.Interactivity;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class HardwareCalibrationView : UserControl
{
    public HardwareCalibrationView()
    {
        InitializeComponent();
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HardwareCalibrationViewModel vm)
        {
            vm.HostWindow = TopLevel.GetTopLevel(this) as Window;
            vm.StartRealTimeUpdate();
        }
    }

    private void OnViewUnloaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HardwareCalibrationViewModel vm)
        {
            vm.StopRealTimeUpdate();
        }
    }
}
