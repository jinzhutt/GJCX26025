using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class DeviceAuthWindow : Window
{
    public DeviceAuthWindow()
    {
        InitializeComponent();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DeviceAuthViewModel vm)
        {
            vm.HostWindow = this;
            vm.RequestClose += () => Close(vm.DialogResultOk);
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
