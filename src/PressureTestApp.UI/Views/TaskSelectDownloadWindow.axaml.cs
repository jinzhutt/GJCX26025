using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class TaskSelectDownloadWindow : Window
{
    public TaskSelectDownloadWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TaskDownloadSelectViewModel vm)
        {
            vm.RequestClose = () =>
            {
                if (IsVisible)
                    Close();
            };
            vm.HostWindow = this;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        if (DataContext is TaskDownloadSelectViewModel { IsBusy: true })
            return;
        if (DataContext is TaskDownloadSelectViewModel vm)
            vm.CancelCommand.Execute(null);
        else
            Close();
        e.Handled = true;
    }
}
