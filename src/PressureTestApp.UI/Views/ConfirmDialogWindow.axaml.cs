using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PressureTestApp.UI.Views;

public partial class ConfirmDialogWindow : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmDialogWindow()
    {
        InitializeComponent();
    }

    public ConfirmDialogWindow(string message, string titleBar = "确认删除") : this()
    {
        Title = titleBar;
        TitleText.Text = titleBar;
        BodyText.Text = message;
    }

    public static async Task<bool> ShowConfirmAsync(Window owner, string message, string title = "确认删除")
    {
        var dlg = new ConfirmDialogWindow(message, title);
        await dlg.ShowDialog(owner);
        return dlg.Confirmed;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
