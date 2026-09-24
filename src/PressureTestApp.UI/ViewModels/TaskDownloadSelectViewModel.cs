using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressureTestApp.Communication;
using PressureTestApp.Communication.Models;
using PressureTestApp.Core.Helpers;
using PressureTestApp.UI.Views;

namespace PressureTestApp.UI.ViewModels;

/// <summary>
/// 与 GJCS FrmTaskSelectDownload 一致：接收已拉取的主任务列表，展开子项供勾选，确认后选择性下载。
/// </summary>
public partial class TaskDownloadSelectViewModel : ObservableObject
{
    private readonly TaskDownloadService _taskDownload;
    private MainTaskListResult? _taskData;

    public ObservableCollection<TaskDownloadDisplayRow> DisplayTasks { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isAllSelected;

    /// <summary>用户确认下载后填充（与 SelectedTasks 一致）。</summary>
    public List<MainTaskDownloadModel> SelectedTasks { get; private set; } = new();

    public bool DialogResultOk { get; private set; }

    public Action? RequestClose { get; set; }
    public Window? HostWindow { get; set; }

    public TaskDownloadSelectViewModel(TaskDownloadService taskDownload) => _taskDownload = taskDownload;

    /// <summary>构造时传入 API 已获取的数据（不在窗体内再次请求列表）。</summary>
    public void Initialize(MainTaskListResult taskData)
    {
        _taskData = taskData;
        LoadTaskList();
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        foreach (var row in DisplayTasks)
            row.IsSelected = value;
    }

    /// <summary>与 FrmTaskSelectDownload.LoadTaskList 一致。</summary>
    private void LoadTaskList()
    {
        try
        {
            DisplayTasks.Clear();
            SelectedTasks.Clear();
            DialogResultOk = false;

            if (_taskData?.List == null || _taskData.List.Count == 0)
            {
                StatusText = "没有可下载的任务！";
                _ = ShowNoticeAsync("没有可下载的任务！");
                return;
            }

            foreach (var row in TaskDownloadService.ExpandDisplayRows(_taskData))
                DisplayTasks.Add(row);

            if (DisplayTasks.Count == 0)
            {
                StatusText = "没有可下载的任务！";
                _ = ShowNoticeAsync("没有可下载的任务！");
                return;
            }

            StatusText = $"共 {DisplayTasks.Count} 个任务可选择";
            NlogHelper.Info($"加载任务列表成功，共 {DisplayTasks.Count} 个任务可选择");
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "加载任务列表失败");
            StatusText = $"加载任务列表失败：{ex.Message}";
            _ = ShowNoticeAsync($"加载任务列表失败：{ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (IsBusy)
            return;
        SelectedTasks.Clear();
        DialogResultOk = false;
        CloseDialog();
    }

    private bool CanCancel() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => CancelCommand.NotifyCanExecuteChanged();

    private void CloseDialog()
    {
        RequestClose?.Invoke();
        if (HostWindow is { IsVisible: true })
            HostWindow.Close();
    }

    [RelayCommand]
    private async Task ConfirmDownloadAsync()
    {
        try
        {
            SelectedTasks.Clear();

            if (DisplayTasks.Count == 0)
            {
                await ShowNoticeAsync("任务列表为空！");
                return;
            }

            var selectedItems = DisplayTasks.Where(t => t.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                await ShowNoticeAsync("请至少选择一个任务进行下载！");
                return;
            }

            var owner = HostWindow;
            if (owner != null)
            {
                var ok = await ConfirmDialogWindow.ShowConfirmAsync(
                    owner,
                    $"确定下载选中的 {selectedItems.Count} 个任务吗？",
                    "确认下载");
                if (!ok)
                    return;
            }

            var tasksToDownload = new List<MainTaskDownloadModel>();
            foreach (var item in selectedItems)
            {
                tasksToDownload.Add(new MainTaskDownloadModel
                {
                    HoldTaskId = item.HoldTaskId,
                    ProjectTypeName = item.ProjectTypeName,
                    ProjectNumber = item.ProjectNumber,
                    TrainNo = item.TrainNo,
                    TrainCode = item.TrainCode,
                    CarCode = item.CarCode,
                    DepId = item.DepId,
                    DebugType = item.DebugType,
                    HoldItemId = item.HoldItemId,
                    ItemName = item.ItemName,
                    OperateProcess = item.OperateProcess,
                    MutualProcess = item.MutualProcess,
                    QualityProcess = item.QualityProcess,
                    HoldItems = item.OriginalHoldItems
                });
            }

            NlogHelper.Info($"开始下载选中的 {selectedItems.Count} 个任务");

            if (string.IsNullOrWhiteSpace(VarHelper.CurrentUserId))
            {
                await ShowNoticeAsync("未登录，无法下载任务。");
                return;
            }

            IsBusy = true;
            StatusText = $"正在下载 {selectedItems.Count} 个任务…";

            var result = await _taskDownload.GetMainTaskSelectiveAsync(
                VarHelper.CurrentUserId!, tasksToDownload);

            if (result)
            {
                SelectedTasks = tasksToDownload;
                DialogResultOk = true;
                StatusText = $"下载完成，共 {SelectedTasks.Count} 个任务";
                NlogHelper.Info(StatusText);
                CloseDialog();
            }
            else
            {
                await ShowNoticeAsync("任务下载失败！");
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "确认下载错误");
            StatusText = $"确认下载错误：{ex.Message}";
            await ShowNoticeAsync($"确认下载错误：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowNoticeAsync(string message)
    {
        var owner = HostWindow;
        if (owner != null)
            await new InfoNoticeWindow(message).ShowDialog(owner);
    }
}
