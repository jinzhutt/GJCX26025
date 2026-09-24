using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Communication.Api;
using PressureTestApp.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PressureTestApp.UI.ViewModels;

/// <summary>
/// MES 4.1–4.6 / 曲线回传相关 API（与主文件 partial 合并）。
/// 轴承退卸：顶层 base64Image = 曲线 PNG Base64；4.6 本机状态查询。
/// </summary>
public partial class MainViewModel
{
    private MesApiClient? _mesApiLazy;
    private MesPendingUploadService? _mesPendingLazy;
    private DeviceWorkReadyState? _workReadyLazy;
    private bool _mesPowerOnReported;
    private DateTime? _onlineTestStartTimeCompat;
    /// <summary>协议 4.4：每分钟定时上报。</summary>
    private System.Timers.Timer? _mesStatusHeartbeatTimer;
    private int _mesStatusUploading;

    private MesApiClient MesApi =>
        _mesApiLazy ??= ServiceProviderServiceExtensions
            .GetRequiredService<MesApiClient>(App.Services);

    private MesPendingUploadService MesPending =>
        _mesPendingLazy ??= ServiceProviderServiceExtensions
            .GetRequiredService<MesPendingUploadService>(App.Services);

    private DeviceWorkReadyState WorkReady =>
        _workReadyLazy ??= ServiceProviderServiceExtensions
            .GetRequiredService<DeviceWorkReadyState>(App.Services);

    // —— 信息录入：操作员（人脸/刷脸获取） ——
    public string InfoEntryOperatorText
    {
        get => _infoEntryOperatorText;
        set
        {
            if (_infoEntryOperatorText == value) return;
            _infoEntryOperatorText = value ?? "";
            OnPropertyChanged(nameof(InfoEntryOperatorText));
        }
    }
    private string _infoEntryOperatorText = "";

    public CommunityToolkit.Mvvm.Input.IRelayCommand FetchOperatorCommand =>
        _fetchOperatorCommand ??= new CommunityToolkit.Mvvm.Input.RelayCommand(FetchOperator);
    private CommunityToolkit.Mvvm.Input.IRelayCommand? _fetchOperatorCommand;

    private void FetchOperator()
    {
        var name = VarHelper.CurrentUserName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            AppendTestProcessLog("获取操作员失败：当前未登录");
            return;
        }
        InfoEntryOperatorText = name;
        AppendTestProcessLog($"已获取操作员：{name}");
    }

    // —— 右键保存位移-压力曲线（回传顶层 base64Image） ——
    /// <summary>待回传曲线 PNG Base64。</summary>
    public string PendingCurveBase64 { get; private set; } = "";
    /// <summary>本地备份文件路径（仅提示用，不回传）。</summary>
    public string PendingCurveImagePath { get; private set; } = "";
    public bool HasPendingHvCurve { get; private set; }

    public string CurveSaveHint { get; private set; } = "右键曲线可保存图片，保存后随数据回传 Base64";
    public IBrush CurveSaveHintBrush { get; private set; } = Brushes.Gray;

    public void NotifyProcessInterfaceLoaded()
    {
        if (_mesPowerOnReported) return;
        _mesPowerOnReported = true;
        WorkReady.Configure(MesApi.DeviceId);
        RefreshWorkReadyFromStatus("开");
        AppendTestProcessLog("工艺界面已加载，上报 MES 开机状态（开）…");
        _ = ReportMesDeviceStatusCompatAsync("开", "登录后工艺界面加载（开机）");
        StartMesStatusHeartbeat();
    }

    public async Task NotifyMesShutdownAsync()
    {
        StopMesStatusHeartbeat();
        RefreshWorkReadyFromStatus("关机");
        AppendTestProcessLog("退出系统，上报 MES 关机状态…");
        await ReportMesDeviceStatusCompatAsync("关机", "退出系统");
    }

    /// <summary>协议 4.4：状态变化立刻上报（开始/结束作业、急停等）。</summary>
    public void NotifyMesDeviceStatusChanged(string? forceStatus = null, string remark = "")
    {
        var status = string.IsNullOrWhiteSpace(forceStatus)
            ? ResolveMesDeviceStatus()
            : forceStatus.Trim();
        RefreshWorkReadyFromStatus(status);
        _ = ReportMesDeviceStatusCompatAsync(status, remark);
    }

    /// <summary>按当前 PLC/作业状态解析协议 4.4 status。</summary>
    private string ResolveMesDeviceStatus()
    {
        if (PlcDiMdi16)
            return "急停";
        if (Status == Core.Models.TestResult.Running)
            return "运行";
        return "待机";
    }

    /// <summary>同步协议 4.6 本机查询用的可作业状态。</summary>
    private void RefreshWorkReadyFromStatus(string status)
    {
        WorkReady.Configure(MesApi.DeviceId);
        var canWork = status is "待机" or "开";
        WorkReady.Update(canWork, status);
    }

    private void StartMesStatusHeartbeat()
    {
        if (_mesStatusHeartbeatTimer != null)
            return;

        _mesStatusHeartbeatTimer = new System.Timers.Timer(60_000) { AutoReset = true };
        _mesStatusHeartbeatTimer.Elapsed += (_, _) =>
        {
            try
            {
                var status = ResolveMesDeviceStatus();
                RefreshWorkReadyFromStatus(status);
                _ = ReportMesDeviceStatusCompatAsync(status, "定时上报(1分钟/次)");
            }
            catch (Exception ex)
            {
                NlogHelper.Warn(ex, "MES 4.4 定时上报异常");
            }
        };
        _mesStatusHeartbeatTimer.Start();
        NlogHelper.Info("MES 4.4 状态定时上报已启动：间隔 60s");
        AppendTestProcessLog("MES 4.4 状态定时上报已启动（1分钟/次）");
    }

    private void StopMesStatusHeartbeat()
    {
        if (_mesStatusHeartbeatTimer == null)
            return;
        _mesStatusHeartbeatTimer.Stop();
        _mesStatusHeartbeatTimer.Dispose();
        _mesStatusHeartbeatTimer = null;
    }

    public void StopMesStatusReporting() => StopMesStatusHeartbeat();

    private async Task ReportMesDeviceStatusCompatAsync(string status, string remark)
    {
        if (Interlocked.CompareExchange(ref _mesStatusUploading, 1, 0) != 0)
            return;

        try
        {
            var resp = await MesApi.UploadDeviceStatusAsync(new MesDeviceStatusUploadRequest
            {
                Status = status,
                Remark = remark,
                OccurTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
            AppendTestProcessLog(resp.IsOk
                ? $"MES 状态上报成功：{status}"
                : $"MES 状态上报失败：{resp.StatusText}");
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "MES 4.4 状态上报异常");
            AppendTestProcessLog($"MES 状态上报异常：{ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _mesStatusUploading, 0);
        }
    }

    /// <summary>协议 4.5：设备故障上报（有故障时调用）。</summary>
    public async Task ReportMesDeviceFaultAsync(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            AppendTestProcessLog("无故障说明，跳过 MES 故障上报");
            return;
        }

        try
        {
            var resp = await MesApi.UploadDeviceFaultAsync(new MesDeviceFaultUploadRequest
            {
                ErrorMessage = errorMessage.Trim(),
                OccurTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
            AppendTestProcessLog(resp.IsOk
                ? $"MES 故障上报成功：{errorMessage}"
                : $"MES 故障上报失败：{resp.StatusText}");
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "MES 4.5 故障上报异常");
            AppendTestProcessLog($"MES 故障上报异常：{ex.Message}");
        }
    }

    public string ResolveMesWorkerName()
    {
        if (!string.IsNullOrWhiteSpace(InfoEntryOperatorText))
            return InfoEntryOperatorText.Trim();
        if (!string.IsNullOrWhiteSpace(VarHelper.CurrentUserName))
            return VarHelper.CurrentUserName.Trim();
        if (!string.IsNullOrWhiteSpace(UserName))
            return UserName.Trim();
        return "";
    }

    public void MarkOnlineTestStarted()
    {
        _onlineTestStartTimeCompat = DateTime.Now;
        NotifyMesDeviceStatusChanged("运行", "开始退卸作业");
    }

    public void MarkOnlineTestEnded()
    {
        NotifyMesDeviceStatusChanged("待机", "结束退卸作业");
    }

    public MesWorkDataUploadRequest BuildMesWorkDataUploadRequest(TaskSearchResultRow row)
    {
        var partCode = !string.IsNullOrWhiteSpace(row.AssignedVehicleNumber)
            ? row.AssignedVehicleNumber
            : (row.DetailId ?? "");
        var taskId = !string.IsNullOrWhiteSpace(row.ConstructionTaskId)
            ? row.ConstructionTaskId
            : (CurrentDetail?.HoldTaskId ?? "");
        return BuildMesWorkDataUploadRequest(taskId, partCode, row.StepName, row.LocalDetailId);
    }

    public MesWorkDataUploadRequest BuildMesWorkDataUploadRequest(
        string taskId,
        string partCode,
        string? stepName,
        int localDetailId = 0)
    {
        // 仅当客户右键「保存曲线」后才带 Base64；未保存则回传空字符串
        var base64 = HasPendingHvCurve ? PendingCurveBase64 : "";
        var worker = ResolveMesWorkerName();
        if (string.IsNullOrWhiteSpace(worker))
            throw new InvalidOperationException("操作员为空：请先登录，或在信息录入中填写操作员后再上传。");

        _ = localDetailId;
        var testOk = Status is not Core.Models.TestResult.Breakdown and not Core.Models.TestResult.Error;

        return new MesWorkDataUploadRequest
        {
            TaskId = taskId?.Trim() ?? "",
            PartCode = partCode?.Trim() ?? "",
            DeviceId = MesApi.DeviceId,
            // 客户要求：回传时开始/结束时间为空
            StartTime = "",
            EndTime = "",
            Worker = worker,
            TestResult = testOk ? "成功" : "失败",
            Remark = string.IsNullOrWhiteSpace(stepName) ? "轴承退卸作业回传" : $"轴承退卸：{stepName}",
            Base64Image = base64
        };
    }

    public void SetPendingCurveBase64(string? base64, string? localFilePath = null)
    {
        PendingCurveBase64 = (base64 ?? "").Trim();
        PendingCurveImagePath = (localFilePath ?? "").Trim();
        HasPendingHvCurve = PendingCurveBase64.Length > 0;
        if (HasPendingHvCurve)
        {
            var name = string.IsNullOrEmpty(PendingCurveImagePath)
                ? $"Base64({PendingCurveBase64.Length}字符)"
                : Path.GetFileName(PendingCurveImagePath);
            CurveSaveHint = $"曲线已保存：{name}";
            CurveSaveHintBrush = Brushes.LimeGreen;
            OnPropertyChanged(nameof(CurveSaveHint));
            OnPropertyChanged(nameof(CurveSaveHintBrush));
            OnPropertyChanged(nameof(HasPendingHvCurve));
            OnPropertyChanged(nameof(PendingCurveBase64));
            OnPropertyChanged(nameof(PendingCurveImagePath));
            AppendTestProcessLog($"压力曲线已保存，回传 base64Image 长度={PendingCurveBase64.Length}");
        }
        else
        {
            ClearPendingHvCurve();
        }
    }

    public void ClearPendingHvCurve(bool silent = false)
    {
        var had = HasPendingHvCurve || !string.IsNullOrEmpty(PendingCurveBase64);
        PendingCurveBase64 = "";
        PendingCurveImagePath = "";
        HasPendingHvCurve = false;
        CurveSaveHint = "右键曲线可保存图片，保存后随数据回传 Base64";
        CurveSaveHintBrush = Brushes.Gray;
        OnPropertyChanged(nameof(CurveSaveHint));
        OnPropertyChanged(nameof(CurveSaveHintBrush));
        OnPropertyChanged(nameof(HasPendingHvCurve));
        OnPropertyChanged(nameof(PendingCurveBase64));
        OnPropertyChanged(nameof(PendingCurveImagePath));
        if (had && !silent)
            AppendTestProcessLog("已清除待回传压力曲线（base64Image 将为空）");
    }
}
