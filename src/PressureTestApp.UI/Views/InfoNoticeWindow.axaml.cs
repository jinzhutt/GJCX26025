using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PressureTestApp.UI.Views;

public partial class InfoNoticeWindow : Window
{
    public InfoNoticeWindow()
    {
        InitializeComponent();
    }

    public InfoNoticeWindow(string message, string titleBar = "提示") : this()
    {
        Title = titleBar;
        TitleText.Text = titleBar;
        BodyText.Text = message;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
