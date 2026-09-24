using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace PressureTestApp.UI.Views;

/// <summary>任务重做成功系统消息（图二）。</summary>
public partial class TaskRedoSuccessWindow : Window
{
    public TaskRedoSuccessWindow()
    {
        InitializeComponent();
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public static async Task ShowAsync(Window owner)
    {
        var w = new TaskRedoSuccessWindow();
        await w.ShowDialog(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
