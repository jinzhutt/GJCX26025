using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressureTestApp.Communication.Api;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Services;
using PressureTestApp.Data.Repositories;
using PressureTestApp.UI.Views;

namespace PressureTestApp.UI.ViewModels;

/// <summary>任务查看 / 任务回传（上传入口）弹窗模式。</summary>
public enum TaskViewDialogMode
{
    TaskViewOnly,
    TaskUpload
}

/// <summary>主界面确认选中的在线任务明细。</summary>
public sealed record OnlineTaskSelection(
    int LocalTaskRowId,
    int LocalDetailId,
    string? HoldTaskId,
    string? HoldItemId,
    string? DetailId,
    string? StepName,
    string? StepContent,
    string? ProjectNumber,
    string? TrainNo,
    string? CarCode,
    string? ResultStandard);

/// <summary>任务查看搜索结果行（表格绑定）。</summary>
public partial class TaskSearchResultRow : ObservableObject
{
    public TaskSearchResultRow(TaskDetailRowDto row)
    {
        LocalDetailId = row.LocalDetailId;
        LocalTaskRowId = row.LocalTaskRowId;
        var t = row.Main;
        var d = row.Detail;

        ModelCode = t.projectNumber ?? "";
        AssignedTrainNumber = t.trainCode ?? "";
        AssignedVehicleNumber = t.carCode ?? "";
        TrainSetNumber = t.trainNo ?? "";
        DetailId = d.detailId;
        ConstructionTaskId = t.holdTaskId;
        WithstandSubTaskId = t.holdItemId ?? "";
        StepName = d.stepName ?? "";
        OperationArea = d.stepBom ?? "";
        OperationContent = d.stepContent ?? "";
        AmbientTempC = d.testTemperature ?? "";
        AmbientHumidityPct = d.testHumidity ?? "";
        AppliedVoltage = d.applyVoltage ?? "";
        TestResultValue = d.testValue ?? "";
        Remark = d.remark ?? "";
        OperatorName = d.operatePersonName ?? "";
        OperatorJobId = d.operatePerson ?? "";
        OperationTime = d.operateTime ?? "";
        SelfCheckTime = d.operateTime ?? "";
        SelfCheckResult = d.operateResult ?? "";
        ResultDefaultContent = d.resultContent ?? "";
        ResultRangeStandard = d.resultStandard ?? "";
        ResultUnit = d.resultUnit ?? "";
        AssistName1 = d.auxiliariesName1 ?? "";
        AssistJob1 = d.auxiliariesCode1 ?? "";
        AssistName2 = d.auxiliariesName2 ?? "";
        AssistJob2 = d.auxiliariesCode2 ?? "";
        AssistName3 = d.auxiliariesName3 ?? "";
        AssistJob3 = d.auxiliariesCode3 ?? "";
        AssistName4 = d.auxiliariesName4 ?? "";
        AssistJob4 = d.auxiliariesCode4 ?? "";
        MutualInspectorName = d.mutualPersonName ?? "";
        MutualInspectorJobId = d.mutualPerson ?? "";
        MutualInspectionTime = d.mutualTime ?? "";
        MutualInspectionResult = d.mutualResult ?? "";
        QaInspectorName = d.qualityPersonName ?? "";
        QaInspectorJobId = d.qualityPerson ?? "";
        QaInspectionTime = d.qualityTime ?? "";
        QaInspectionResult = d.qualityResult ?? "";
    }

    public int LocalDetailId { get; }
    public int LocalTaskRowId { get; }

    [ObservableProperty] private bool _isSelected;

    public string ModelCode { get; }
    public string AssignedTrainNumber { get; }
    public string AssignedVehicleNumber { get; }
    public string TrainSetNumber { get; }
    public string DetailId { get; }
    public string ConstructionTaskId { get; }
    public string WithstandSubTaskId { get; }
    public string StepName { get; }
    public string OperationArea { get; }
    public string OperationContent { get; }
    public string AmbientTempC { get; }
    public string AmbientHumidityPct { get; }
    public string AppliedVoltage { get; }
    public string TestResultValue { get; }
    public string Remark { get; }
    public string OperatorName { get; }
    public string OperatorJobId { get; }
    public string OperationTime { get; }
    public string SelfCheckTime { get; }
    public string SelfCheckResult { get; }
    public string ResultDefaultContent { get; }
    public string ResultRangeStandard { get; }
    public string ResultUnit { get; }
    public string AssistName1 { get; }
    public string AssistJob1 { get; }
    public string AssistName2 { get; }
    public string AssistJob2 { get; }
    public string AssistName3 { get; }
    public string AssistJob3 { get; }
    public string AssistName4 { get; }
    public string AssistJob4 { get; }
    public string MutualInspectorName { get; }
    public string MutualInspectorJobId { get; }
    public string MutualInspectionTime { get; }
    public string MutualInspectionResult { get; }
    public string QaInspectorName { get; }
    public string QaInspectorJobId { get; }
    public string QaInspectionTime { get; }
    public string QaInspectionResult { get; }
}

/// <summary>任务查看 / 任务回传弹窗 ViewModel。</summary>
public partial class TaskViewViewModel : ObservableObject
{
    private readonly LegacyTaskRepository _taskRepo;
    private readonly ITaskApiService _api;
    private readonly MesApiClient _mesApi;
    private readonly MesPendingUploadService _mesPending;
    private TaskViewDialogMode _mode = TaskViewDialogMode.TaskViewOnly;

    public ObservableCollection<string> ModelCodeItems { get; } = new();
    public ObservableCollection<string> AssignedVehicleItems { get; } = new();
    public ObservableCollection<string> TrainColumnItems { get; } = new();
    public ObservableCollection<TaskSearchResultRow> SearchResults { get; } = new();

    [ObservableProperty] private string? _selectedModelCode;
    [ObservableProperty] private string? _selectedAssignedVehicle;
    [ObservableProperty] private string? _selectedTrainColumn;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private bool _isAllSelected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private TaskSearchResultRow? _selectedResultRow;

    /// <summary>主界面，用于打包 MES 4.3 回传（含曲线）。</summary>
    public MainViewModel? HostMain { get; set; }

    public OnlineTaskSelection? ConfirmedSelection { get; private set; }

    public Action? RequestClose { get; set; }

    public string WindowTitle => _mode == TaskViewDialogMode.TaskUpload ? "任务回传" : "任务查看";
    public bool ShowFooterActions => _mode == TaskViewDialogMode.TaskUpload;
    /// <summary>任务查看仅展示协议 4.1 字段；任务回传保留完整明细列。</summary>
    public bool ShowProtocolColumnsOnly => _mode == TaskViewDialogMode.TaskViewOnly;
    public bool ShowLegacyDetailColumns => _mode == TaskViewDialogMode.TaskUpload;
    /// <summary>仅「任务查看」显示右侧操作列（选择）；任务回传隐藏「任务重做」。</summary>
    public bool ShowOperationRail => _mode == TaskViewDialogMode.TaskViewOnly;
    public string ModelFilterLabel => ShowProtocolColumnsOnly ? "车型:" : "车型编码:";
    public string AssignedFilterLabel => ShowProtocolColumnsOnly ? "工件编号:"
        : _mode == TaskViewDialogMode.TaskUpload ? "配属编号:" : "配属辆号:";
    public string TrainFilterLabel => ShowProtocolColumnsOnly ? "车号:" : "车列号:";
    public string OperationButtonText => "选择";
    public const int TaskResultFrozenLeadingColumnCount = 5;
    public int TaskResultFrozenColumnCount => ShowProtocolColumnsOnly ? 1 : TaskResultFrozenLeadingColumnCount;

    public TaskViewViewModel(
        LegacyTaskRepository taskRepo,
        ITaskApiService api,
        MesApiClient mesApi,
        MesPendingUploadService mesPending)
    {
        _taskRepo = taskRepo;
        _api = api;
        _mesApi = mesApi;
        _mesPending = mesPending;
    }

    public void Configure(TaskViewDialogMode mode)
    {
        _mode = mode;
        ConfirmedSelection = null;
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ShowFooterActions));
        OnPropertyChanged(nameof(ShowProtocolColumnsOnly));
        OnPropertyChanged(nameof(ShowLegacyDetailColumns));
        OnPropertyChanged(nameof(ShowOperationRail));
        OnPropertyChanged(nameof(ModelFilterLabel));
        OnPropertyChanged(nameof(AssignedFilterLabel));
        OnPropertyChanged(nameof(TrainFilterLabel));
        OnPropertyChanged(nameof(OperationButtonText));
        OnPropertyChanged(nameof(TaskResultFrozenColumnCount));
    }

    public async Task InitializeAsync()
    {
        var (projects, cars, trains) = await _taskRepo.GetFilterOptionsAsync();
        ModelCodeItems.Clear();
        AssignedVehicleItems.Clear();
        TrainColumnItems.Clear();
        foreach (var x in projects) ModelCodeItems.Add(x);
        foreach (var x in cars) AssignedVehicleItems.Add(x);
        foreach (var x in trains) TrainColumnItems.Add(x);
        await SearchAsync();
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        foreach (var r in SearchResults)
            r.IsSelected = value;
    }

    [RelayCommand]
    private Task Search() => SearchAsync();

    private async Task SearchAsync(string? restrictToDetailId = null)
    {
        IsBusy = true;
        try
        {
            var tag = _mode == TaskViewDialogMode.TaskUpload ? "任务回传" : "任务查看";
            NlogHelper.Info($"{tag}·搜索");

            IsAllSelected = false;
            SearchResults.Clear();
            SelectedResultRow = null;

            // 任务回传与任务查看共用同一查询（下载后的未回传任务）
            var rows = await _taskRepo.SearchForViewAsync(
                SelectedModelCode, SelectedAssignedVehicle, SelectedTrainColumn, restrictToDetailId);

            foreach (var row in rows)
                SearchResults.Add(new TaskSearchResultRow(row));

            HasData = SearchResults.Count > 0;
            StatusMessage = HasData
                ? $"共 {SearchResults.Count} 条"
                : "无匹配数据。请先「任务下载」，或清空车型/配属/车列筛选后再搜索。";
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "任务列表查询失败");
            StatusMessage = $"查询失败：{ex.Message}";
            HasData = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplySearchFromRow(TaskSearchResultRow row)
    {
        SelectedModelCode = row.ModelCode;
        SelectedAssignedVehicle = string.IsNullOrEmpty(row.AssignedVehicleNumber) ? null : row.AssignedVehicleNumber;
        SelectedTrainColumn = string.IsNullOrEmpty(row.TrainSetNumber) ? null : row.TrainSetNumber;
        _ = SearchAsync(row.DetailId);
    }

    public event EventHandler<TaskSearchResultRow>? NavigateToTaskViewFromRedo;
    public event EventHandler<TaskSearchResultRow>? RequestTaskSelectConfirm;

    public async Task FinishRedoTransferAsync(TaskSearchResultRow row)
    {
        await _taskRepo.ResetForRedoAsync(row.LocalDetailId);
        await SearchAsync();
        NlogHelper.Info($"任务重做完成：明细ID={row.DetailId} · {row.StepName}");
    }

    public void ApplyConfirmedTaskSelection(TaskSearchResultRow row)
    {
        SelectedResultRow = row;
        ConfirmedSelection = new OnlineTaskSelection(
            row.LocalTaskRowId,
            row.LocalDetailId,
            row.ConstructionTaskId,
            row.WithstandSubTaskId,
            row.DetailId,
            row.StepName,
            row.OperationContent,
            row.ModelCode,
            row.TrainSetNumber,
            row.AssignedVehicleNumber,
            row.ResultRangeStandard);
        NlogHelper.Info($"选择任务（已确认）：明细ID={row.DetailId} · {row.StepName}");
    }

    [RelayCommand]
    private void TaskOperation(TaskSearchResultRow? row)
    {
        if (row is null) return;
        // 任务回传已隐藏「任务重做」；仅任务查看走「选择」
        if (_mode == TaskViewDialogMode.TaskUpload)
            return;
        RequestTaskSelectConfirm?.Invoke(this, row);
    }

    [RelayCommand]
    private async Task OneClickDataReturnAsync()
    {
        var owner = ResolveDialogOwner();

        if (string.IsNullOrWhiteSpace(VarHelper.CurrentUserId))
        {
            StatusMessage = "未登录，无法上传。";
            await ShowUploadResultDialogAsync(owner, success: false, "回传失败", "未登录，无法上传。");
            return;
        }

        // 按明细去重：勾选一条只回传一次
        var picked = SearchResults
            .Where(r => r.IsSelected)
            .GroupBy(r => r.LocalDetailId)
            .Select(g => g.First())
            .ToList();
        if (picked.Count == 0)
        {
            StatusMessage = "请勾选需要回传的任务。";
            await ShowUploadResultDialogAsync(owner, success: false, "回传失败", "请勾选需要回传的任务。");
            return;
        }

        if (HostMain is null)
        {
            StatusMessage = "未绑定主界面，无法打包作业数据回传。";
            await ShowUploadResultDialogAsync(owner, success: false, "回传失败", "未绑定主界面，无法打包作业数据回传。");
            return;
        }

        IsBusy = true;
        StatusMessage = "正在通过 MES 回传（含曲线）…";
        try
        {
            var pickedIds = picked.Select(r => r.LocalDetailId).ToHashSet();
            try
            {
                // 跳过本次勾选明细的旧待传，避免「补传一次 + 本次再传一次」
                var (flushOk, flushFail, flushRem) = await _mesPending.FlushPendingAsync(pickedIds);
                if (flushOk > 0 || flushFail > 0)
                    StatusMessage = $"先补传历史待传：成功 {flushOk}，失败 {flushFail}，剩余 {Math.Max(flushRem, 0)}；继续本次回传…";
            }
            catch (Exception exFlush)
            {
                NlogHelper.Warn(exFlush, "回传前补传待传队列异常");
            }

            int ok = 0, fail = 0;
            string? lastFailMsg = null;
            foreach (var row in picked)
            {
                try
                {
                    var payload = HostMain.BuildMesWorkDataUploadRequest(row);
                    var base64 = payload.Base64Image ?? "";
                    var hasCurve = !string.IsNullOrWhiteSpace(base64);
                    NlogHelper.Info($"MES 4.3 准备回传：taskId={payload.TaskId}，worker={payload.Worker}，base64Image长度={(hasCurve ? base64.Length : 0)}");
                    HostMain.AppendTestProcessLog($"MES 4.3 回传：taskId={payload.TaskId}，{(hasCurve ? $"含 base64Image（{base64.Length}字符）" : "仅数据（未保存曲线）")}");
                    var resp = await _mesApi.UploadWorkDataAsync(payload);
                    if (resp.IsOk)
                    {
                        await _taskRepo.MarkUploadedAsync(row.LocalDetailId);
                        await _mesPending.MarkUploadedByLocalDetailIdAsync(row.LocalDetailId);
                        ok++;
                        NlogHelper.Info($"MES 4.3 回传成功：taskId={payload.TaskId}，msg={resp.StatusText}");
                    }
                    else
                    {
                        await _mesPending.SavePendingAsync(payload, row.LocalDetailId);
                        fail++;
                        lastFailMsg = string.IsNullOrWhiteSpace(resp.StatusText)
                            ? $"code={resp.Code}"
                            : resp.StatusText;
                        NlogHelper.Warn($"MES 4.3 回传失败（已入待传队列）：taskId={payload.TaskId}，code={resp.Code}，msg={resp.StatusText}");
                    }
                }
                catch (Exception exRow)
                {
                    fail++;
                    lastFailMsg = exRow.Message;
                    NlogHelper.Error(exRow, $"MES 4.3 回传异常：detailId={row.DetailId}");
                    try
                    {
                        var payload = HostMain.BuildMesWorkDataUploadRequest(row);
                        await _mesPending.SavePendingAsync(payload, row.LocalDetailId);
                    }
                    catch (Exception exSave)
                    {
                        NlogHelper.Warn(exSave, "回传失败后缓存待传也失败");
                    }
                }
            }

            var pendingLeft = await _mesPending.CountPendingAsync();
            if (fail == 0)
            {
                StatusMessage = $"MES 回传成功，共 {ok} 条。"
                    + (pendingLeft > 0 ? $" 另有历史待传 {pendingLeft} 条。" : "");
                NlogHelper.Info(StatusMessage);
                await ShowUploadResultDialogAsync(owner, success: true, "回传成功", StatusMessage);
            }
            else
            {
                StatusMessage =
                    $"MES 回传失败：成功 {ok} 条，失败 {fail} 条（失败已缓存，网络恢复后自动补传）。待传剩余 {pendingLeft}。"
                    + (string.IsNullOrWhiteSpace(lastFailMsg) ? "" : $"\r原因：{lastFailMsg}");
                NlogHelper.Info(StatusMessage);
                await ShowUploadResultDialogAsync(owner, success: false, "回传失败", StatusMessage);
            }

            await SearchAsync();
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "任务回传失败");
            StatusMessage = $"回传失败：{ex.Message}";
            await ShowUploadResultDialogAsync(owner, success: false, "回传失败", StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task ShowUploadResultDialogAsync(Window? owner, bool success, string title, string message)
    {
        _ = success;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var win = new InfoNoticeWindow(message, title);
                if (owner != null)
                    await win.ShowDialog(owner);
                else
                    win.Show();
            });
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "回传结果弹窗显示失败");
        }
    }

    private Window? ResolveDialogOwner()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var w in desktop.Windows)
            {
                if (w is TaskViewWindow { IsVisible: true })
                    return w;
            }

            if (desktop.MainWindow is Window main)
                return main;
        }

        return null;
    }

    [RelayCommand]
    private void ExitInterface() => RequestClose?.Invoke();
}
