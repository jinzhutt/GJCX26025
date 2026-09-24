using Avalonia.Controls;
using Avalonia.Input;

namespace PressureTestApp.UI.Views;

public partial class TaskUploadWindow : Window
{
    public TaskUploadWindow()
    {
        InitializeComponent();
    }
   
   

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
