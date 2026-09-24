using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;
using PressureTestApp.Core.Services;
using PressureTestApp.Data.Repositories;

namespace PressureTestApp.UI.ViewModels;

/// <summary>
/// 任务上传窗口 ViewModel
/// 列出本地"已完成 + 未上传"的任务，按需勾选并 pullHoldTask
/// </summary>
public partial class TaskUploadViewModel : ObservableObject
{
    private readonly ITaskApiService _api;
    private readonly HoldTaskRepository _taskRepo;

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isUploading;
    [ObservableProperty] private bool _isAllSelected;

    [ObservableProperty] private double _uploadProgress;
    [ObservableProperty] private string? _currentUploadingId;

    public ObservableCollection<UploadableTaskRow> Tasks { get; } = new();

    public int SelectedCount => Tasks.Count(t => t.IsSelected);
    public string SelectedSummary => $"已选 {SelectedCount} / {Tasks.Count} 个，可上传";

    public TaskUploadViewModel()
    {
        _api      = App.Services.GetRequiredService<ITaskApiService>();
        _taskRepo = App.Services.GetRequiredService<HoldTaskRepository>();
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            // 完成且未上传的任务
            var completedAndPending = await _taskRepo.GetUploadablesAsync();

            // 完成但已上传的（也展示，让用户知道历史）
            var pendingTasks = await _taskRepo.GetPendingTasksAsync(TaskSource.Online);

            Tasks.Clear();
            foreach (var t in completedAndPending)
            {
                Tasks.Add(new UploadableTaskRow
                {
                    LocalId = t.Id,
                    HoldTaskId = t.HoldTaskId,
                    HoldItemId = t.HoldItemId ?? "—",
                    ProjectNumber = t.ProjectNumber ?? "—",
                    TrainCode = t.TrainCode ?? "—",
                    CarCode = t.CarCode ?? "—",
                    CompletedAt = t.CompletedAt,
                    IsUploaded = false,
                    IsSelected = true   // 默认全选
                });
            }
            // 已上传的也展示，灰色禁用
            // 这里简化：暂时不查已上传

            StatusMessage = $"待上传任务：{Tasks.Count} 个";
            IsAllSelected = Tasks.Count > 0;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedSummary));
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "加载待上传任务失败");
            StatusMessage = $"加载失败：{ex.Message}";
        }
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        foreach (var t in Tasks.Where(t => !t.IsUploaded)) t.IsSelected = value;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedSummary));
    }

    [RelayCommand]
    private async Task UploadSelectedAsync()
    {
        if (string.IsNullOrEmpty(VarHelper.CurrentUserId))
        {
            StatusMessage = "未登录，无法上传";
            return;
        }

        var toUpload = Tasks.Where(t => t.IsSelected && !t.IsUploaded).ToList();
        if (toUpload.Count == 0)
        {
            StatusMessage = "请先勾选要上传的任务";
            return;
        }

        IsUploading = true;
        UploadProgress = 0;
        int success = 0;

        try
        {
            for (int i = 0; i < toUpload.Count; i++)
            {
                var t = toUpload[i];
                CurrentUploadingId = $"{t.HoldTaskId}";
                StatusMessage = $"上传中 ({i + 1}/{toUpload.Count}) → {t.HoldTaskId}";

                var ok = await _api.UploadTaskResultAsync(t.LocalId, VarHelper.CurrentUserId);
                if (ok)
                {
                    t.IsUploaded = true;
                    success++;
                }

                UploadProgress = (i + 1) * 100.0 / toUpload.Count;
            }
            StatusMessage = $"上传完成：成功 {success} / {toUpload.Count} 个";
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "批量上传异常");
            StatusMessage = $"上传异常：{ex.Message}";
        }
        finally
        {
            IsUploading = false;
            CurrentUploadingId = null;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedSummary));
        }
    }
}

public partial class UploadableTaskRow : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isUploaded;

    public int LocalId { get; set; }
    public string HoldTaskId { get; set; } = "";
    public string HoldItemId { get; set; } = "";
    public string ProjectNumber { get; set; } = "";
    public string TrainCode { get; set; } = "";
    public string CarCode { get; set; } = "";
    public DateTime? CompletedAt { get; set; }

    public string CompletedAtText =>
        CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—";

    public string StatusText => IsUploaded ? "✓ 已上传" : "● 待上传";
}
