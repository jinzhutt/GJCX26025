using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace PressureTestApp.UI.Views;

public partial class SerialPortWarningDialog : Window
{
    public SerialPortWarningDialog() : this("未检测到可用串口！", false)
    {
    }

    public SerialPortWarningDialog(string message) : this(message, false)
    {
    }

    /// <param name="informationStyle">true：蓝色信息图标（如「已断开连接」）；false：黄色警告图标。</param>
    public SerialPortWarningDialog(string message, bool informationStyle)
    {
        InitializeComponent();
        RequestedThemeVariant = ThemeVariant.Light;
        MessageText.Text = message;
        WarningIcon.IsVisible = !informationStyle;
        InfoIconPanel.IsVisible = informationStyle;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
