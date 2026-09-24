using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace PressureTestApp.UI.Views;

public partial class GenericDialogWindow : Window
{
    public GenericDialogWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // 设置弹窗消息
    public void SetMessage(string message)
    {
        var messageText = this.FindControl<TextBlock>("MessageText");
        if (messageText != null)
            messageText.Text = message;

    }

    // 处理 OK 按钮点击
    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        Close(true); // 返回 true 表示用户点击了 OK
    }

    // 处理 Cancel 按钮点击
    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        Close(false); // 返回 false 表示用户点击了 Cancel
    }

}