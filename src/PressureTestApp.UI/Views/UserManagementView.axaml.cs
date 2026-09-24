using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PressureTestApp.UI.ViewModels;
using System.Collections.Generic;

namespace PressureTestApp.UI.Views;

public partial class UserManagementView : UserControl
{
    private readonly Dictionary<TextBox, string> _umFieldFocusValues = new();

    public UserManagementView()
    {
        InitializeComponent();
    }

    private void OnUmParamGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox tb)
            _umFieldFocusValues[tb] = tb.Text ?? "";
    }

    private void OnUmParamLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not string tag) return;
        if (DataContext is not UserManagementViewModel vm) return;
        var now = tb.Text ?? "";
        if (_umFieldFocusValues.TryGetValue(tb, out var was) && was != now)
            vm.NotifyParameterFieldEdited(tag);
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
        {
            vm.HostWindow = TopLevel.GetTopLevel(this) as Window;
            vm.RefreshCommand.Execute(null);
        }
    }

    private void OnUsersDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm && vm.ChangeUserCommand.CanExecute(null))
            vm.ChangeUserCommand.Execute(null);
    }
}
