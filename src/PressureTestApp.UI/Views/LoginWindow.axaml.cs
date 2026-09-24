using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Core.Helpers;
using PressureTestApp.UI.ViewModels;
using System;

namespace PressureTestApp.UI.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        PointerPressed += OnPointerPressed;
        Loaded += OnLoaded;

        DataContextChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.LoginSucceeded -= OnLoginSucceeded;
                vm.LoginSucceeded += OnLoginSucceeded;
            }
        };
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.HostWindow = this;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnLoginSucceeded()
    {
        try
        {
            var main = new MainWindow
            {
                DataContext = App.Services.GetRequiredService<MainViewModel>()
            };
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = main;
                main.Show();
            }
            Close();
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "打开主界面失败");
            if (DataContext is LoginViewModel vm)
                vm.ErrorMessage = $"打开主界面失败：{ex.Message}";
        }
    }
}
