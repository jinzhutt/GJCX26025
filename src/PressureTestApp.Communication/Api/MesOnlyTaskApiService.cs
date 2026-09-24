using PressureTestApp.Communication.Models;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Services;
using PressureTestApp.Data.Repositories;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// 现场仅对接 MES：实现 ITaskApiService，不再调用任何 EPE 接口。
/// 任务下载走 <see cref="TaskDownloadService"/>（MES 4.1）；作业回传走主界面 MES 4.3。
/// </summary>
public sealed class MesOnlyTaskApiService : ITaskApiService
{
    private readonly ProductionSystemConfig _config;
    private readonly TaskDownloadService _taskDownload;
    private readonly LegacyTaskRepository _taskRepo;

    public MesOnlyTaskApiService(
        ProductionSystemConfig config,
        TaskDownloadService taskDownload,
        LegacyTaskRepository taskRepo)
    {
        _config = config;
        _taskDownload = taskDownload;
        _taskRepo = taskRepo;

        var id = (config.MesDeviceId ?? "").Trim();
        if (!string.IsNullOrEmpty(id))
        {
            VarHelper.DeviceId = id;
            VarHelper.DeviceAuthorized = true;
        }
    }

    public string? DeviceNumber =>
        string.IsNullOrWhiteSpace(_config.MesDeviceId) ? null : _config.MesDeviceId.Trim();

    public Task<DeviceAuthResult> AuthorizeDeviceAsync(string deviceMac)
    {
        _ = deviceMac;
        return Task.FromResult(DeviceAuthResult.Fail(
            "现场仅对接 MES，无需 EPE 设备认证。请在 ProductionSystemConfig.ini 配置 MES设备ID。"));
    }

    public Task<int> SyncPersonsAsync(string personCode)
    {
        _ = personCode;
        NlogHelper.Info("MES-only：跳过 EPE 人员同步");
        return Task.FromResult(0);
    }

    public Task<(int projectCount, int trainCount)> SyncProjectsAndTrainsAsync(string personCode)
    {
        _ = personCode;
        NlogHelper.Info("MES-only：跳过 EPE 车型/车列同步");
        return Task.FromResult((0, 0));
    }

    public async Task<List<RemoteTaskBrief>> FetchTaskListAsync(string personCode)
    {
        var result = await _taskDownload.GetMainTaskListAsync(personCode);
        var list = new List<RemoteTaskBrief>();
        if (result?.List == null)
            return list;

        foreach (var t in result.List)
        {
            if (t.HoldItems == null || t.HoldItems.Count == 0)
            {
                list.Add(new RemoteTaskBrief
                {
                    HoldTaskId = t.HoldTaskId ?? "",
                    ProjectNumber = t.ProjectNumber,
                    TrainNo = t.TrainNo,
                    CarCode = t.CarCode,
                    DebugType = t.DebugType,
                    ItemName = t.DebugType,
                    HoldItemId = t.CarCode ?? t.HoldTaskId
                });
                continue;
            }

            foreach (var item in t.HoldItems)
            {
                list.Add(new RemoteTaskBrief
                {
                    HoldTaskId = t.HoldTaskId ?? "",
                    HoldItemId = item.HoldItemId,
                    ProjectTypeName = t.ProjectTypeName,
                    ProjectNumber = t.ProjectNumber,
                    TrainCode = t.TrainCode,
                    TrainNo = t.TrainNo,
                    CarCode = t.CarCode,
                    DebugType = t.DebugType,
                    ItemName = item.ItemName ?? t.DebugType,
                    DepId = t.DepId,
                    OperateProcess = item.OperateProcess,
                    MutualProcess = item.MutualProcess,
                    QualityProcess = item.QualityProcess
                });
            }
        }

        return list;
    }

    public async Task<int> DownloadTaskDetailsAsync(string personCode, IEnumerable<RemoteTaskBrief> selected)
    {
        var models = selected.Select(s => new MainTaskDownloadModel
        {
            HoldTaskId = s.HoldTaskId,
            HoldItemId = s.HoldItemId,
            ProjectTypeName = s.ProjectTypeName,
            ProjectNumber = s.ProjectNumber,
            TrainCode = s.TrainCode,
            TrainNo = s.TrainNo,
            CarCode = s.CarCode,
            DepId = s.DepId,
            DebugType = s.DebugType,
            ItemName = s.ItemName,
            OperateProcess = s.OperateProcess,
            MutualProcess = s.MutualProcess,
            QualityProcess = s.QualityProcess,
            HoldItems = string.IsNullOrWhiteSpace(s.HoldItemId)
                ? null
                :
                [
                    new HoldItemDto
                    {
                        HoldItemId = s.HoldItemId,
                        ItemName = s.ItemName,
                        OperateProcess = s.OperateProcess,
                        MutualProcess = s.MutualProcess,
                        QualityProcess = s.QualityProcess
                    }
                ]
        }).ToList();

        await _taskDownload.GetMainTaskSelectiveAsync(personCode, models);
        return models.Count;
    }

    public Task<bool> UploadTaskResultAsync(int localTaskRowId, string personCode)
    {
        _ = localTaskRowId;
        _ = personCode;
        NlogHelper.Warn("MES-only：请使用主界面「任务上传」走 MES 4.3 回传，不再支持 EPE pullHoldTask");
        return Task.FromResult(false);
    }

    public Task<bool> UploadDetailResultAsync(int localDetailId, string personCode)
    {
        _ = localDetailId;
        _ = personCode;
        NlogHelper.Warn("MES-only：请使用主界面「任务上传」走 MES 4.3 回传，不再支持 EPE pullHoldTask");
        return Task.FromResult(false);
    }
}
