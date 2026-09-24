using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class UserEditWindow : Window
{
    public UserEditWindow()
    {
        InitializeComponent();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is UserEditDialogViewModel vm)
            vm.SetDialogHost(this);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
