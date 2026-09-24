using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class VehicleModelSelectWindow : Window
{
    public VehicleModelSelectWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is VehicleModelSelectViewModel vm)
            await vm.LoadAsync();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
