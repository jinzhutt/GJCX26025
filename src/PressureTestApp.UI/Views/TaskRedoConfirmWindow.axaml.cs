using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class TaskRedoConfirmWindow : Window
{
    public bool Confirmed { get; private set; }

    public TaskRedoConfirmWindow()
    {
        InitializeComponent();
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public TaskRedoConfirmWindow(TaskSearchResultRow row)
        : this(row, "请确认任务是否重做？", "请确认任务是否重做？")
    {
    }

    /// <param name="row">当前行数据。</param>
    /// <param name="questionText">主标题问句（窗内大字）。</param>
    /// <param name="windowTitle">窗口标题栏文字。</param>
    public TaskRedoConfirmWindow(TaskSearchResultRow row, string questionText, string windowTitle) : this()
    {
        Title = windowTitle;
        QuestionTitleText.Text = questionText;
        StepLine.Text = $"步骤名称：{row.StepName}";
        OpAreaLine.Text = $"操作步骤：{row.OperationArea}";
        OpContentLine.Text = $"操作内容：{row.OperationContent}";
    }

    public static async Task<bool> ShowAsync(Window owner, TaskSearchResultRow row)
    {
        var w = new TaskRedoConfirmWindow(row);
        await w.ShowDialog(owner);
        return w.Confirmed;
    }

    /// <summary>任务查看·「选择」确认（与重做确认同版式，问句不同）。</summary>
    public static async Task<bool> ShowTaskSelectAsync(Window owner, TaskSearchResultRow row)
    {
        const string q = "请确认任务是否选择正确？";
        var w = new TaskRedoConfirmWindow(row, q, q);
        await w.ShowDialog(owner);
        return w.Confirmed;
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
}
