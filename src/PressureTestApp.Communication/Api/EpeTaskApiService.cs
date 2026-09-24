using PressureTestApp.Communication;
using PressureTestApp.Communication.Api;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;
using PressureTestApp.Core.Services;
using PressureTestApp.Data.Entities;
using PressureTestApp.Data.Repositories;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// EPE API 服务实现 —— 对接接口文档 v1.2 的 9 个接口（除 3.10/3.11）
/// </summary>
public class EpeTaskApiService : ITaskApiService
{
    private readonly EpeApiClient _api;
    private readonly UserRepository _userRepo;
    private readonly ProjectTrainRepository _ptRepo;
    private readonly LegacyTaskRepository _taskRepo;
    private readonly TaskDownloadService _taskDownload;
    private readonly AuthenticationConfig _authConfig;

    public string? DeviceNumber => _api.DeviceNumber ?? _authConfig.Authentication;

    public EpeTaskApiService(
        EpeApiClient api,
        UserRepository userRepo,
        ProjectTrainRepository ptRepo,
        LegacyTaskRepository taskRepo,
        TaskDownloadService taskDownload,
        AuthenticationConfig authConfig)
    {
        _api = api;
        _userRepo = userRepo;
        _ptRepo = ptRepo;
        _taskRepo = taskRepo;
        _taskDownload = taskDownload;
        _authConfig = authConfig;

        if (!string.IsNullOrWhiteSpace(_authConfig.Authentication))
        {
            _api.DeviceNumber = _authConfig.Authentication.Trim();
            VarHelper.DeviceId = _authConfig.Authentication.Trim();
            VarHelper.DeviceAuthorized = true;
        }
    }

    public async Task<DeviceAuthResult> AuthorizeDeviceAsync(string deviceMac)
    {
        var resp = await _api.DeviceAuthorizeAsync(deviceMac);
        if (resp.IsSuccess && !string.IsNullOrEmpty(resp.DeviceNumber))
        {
            _api.DeviceNumber = resp.DeviceNumber;
            _authConfig.Authentication = resp.DeviceNumber;
            _authConfig.Save();
            VarHelper.DeviceId = resp.DeviceNumber;
            VarHelper.DeviceAuthorized = true;
            return DeviceAuthResult.Ok(resp.DeviceNumber);
        }

        VarHelper.DeviceAuthorized = false;
        return DeviceAuthResult.Fail(resp.Msg ?? "设备认证失败");
    }

    public async Task<int> SyncPersonsAsync(string personCode)
    {
        var resp = await _api.FindPersonListAsync(personCode);
        if (!resp.IsSuccess || resp.List == null) return 0;

        var users = resp.List.Where(p => !string.IsNullOrEmpty(p.PersonCode)).Select(p => new UserEntity
        {
            JobNumber = p.PersonCode!.Trim(),
            Name = p.PersonName ?? "",
            DepId = p.DepId,
            DepName = p.DepName,
            PasswordHash = p.Password ?? "",
            Permission = "操作员",
            Enable = 1,
            Sort = 0
        }).ToList();

        await _userRepo.SyncFromApiAsync(users);
        NlogHelper.Info($"人员同步完成：{users.Count} 人");
        return users.Count;
    }

    public async Task<(int projectCount, int trainCount)> SyncProjectsAndTrainsAsync(string personCode)
    {
        int pCount = 0, tCount = 0;

        var pResp = await _api.FindAllProjectListAsync(personCode);
        if (pResp.IsSuccess && pResp.List != null)
        {
            var ps = pResp.List.Select(x => new ProjectEntity
            {
                ProjectId = x.ProjectId,
                ProjectType = x.ProjectType,
                ProjectNumber = x.ProjectNumber,
                ProjectName = x.ProjectName,
                WbsCode = x.WbsCode,
                ProjectNumberAll = x.ProjectNumberAll,
                VehicleName = x.VehicleName
            }).ToList();
            await _ptRepo.SyncProjectsAsync(ps);
            pCount = ps.Count;
        }

        var tResp = await _api.FindAllTrainListAsync(personCode);
        if (tResp.IsSuccess && tResp.List != null)
        {
            var ts = tResp.List.Select(x => new TrainEntity
            {
                Id = x.Id,
                TrainNo = x.TrainNo,
                TrainCode = x.TrainCode,
                ProjectId = int.TryParse(x.ProjectId, out var pid) ? pid : 0
            }).ToList();
            await _ptRepo.SyncTrainsAsync(ts);
            tCount = ts.Count;
        }

        NlogHelper.Info($"车型车列同步完成：车型 {pCount} 个，车列 {tCount} 个");
        return (pCount, tCount);
    }

    public async Task<List<RemoteTaskBrief>> FetchTaskListAsync(string personCode)
    {
        var resp = await _api.FindHoldTaskListAsync(personCode);
        if (!resp.IsSuccess || resp.List == null) return new List<RemoteTaskBrief>();

        // 一个 holdTask 可能有多个 holdItems，按"任务×子项"展开成扁平行
        var briefs = new List<RemoteTaskBrief>();
        foreach (var t in resp.List)
        {
            if (t.HoldItems == null || t.HoldItems.Count == 0)
            {
                briefs.Add(new RemoteTaskBrief
                {
                    HoldTaskId = t.HoldTaskId ?? "",
                    ProjectTypeName = t.ProjectTypeName,
                    ProjectNumber = t.ProjectNumber,
                    TrainCode = t.TrainCode,
                    TrainNo = t.TrainNo,
                    CarCode = t.CarCode,
                    DebugType = t.DebugType
                });
            }
            else
            {
                foreach (var item in t.HoldItems)
                {
                    briefs.Add(new RemoteTaskBrief
                    {
                        HoldTaskId = t.HoldTaskId ?? "",
                        HoldItemId = item.HoldItemId,
                        ItemName = item.ItemName,
                        OperateProcess = item.OperateProcess,
                        MutualProcess = item.MutualProcess,
                        QualityProcess = item.QualityProcess,
                        DepId = t.DepId,
                        ProjectTypeName = t.ProjectTypeName,
                        ProjectNumber = t.ProjectNumber,
                        TrainCode = t.TrainCode,
                        TrainNo = t.TrainNo,
                        CarCode = t.CarCode,
                        DebugType = t.DebugType
                    });
                }
            }
        }
        return briefs;
    }

    public async Task<int> DownloadTaskDetailsAsync(string personCode, IEnumerable<RemoteTaskBrief> selected)
    {
        var models = selected
            .Where(b => b.IsSelected && !string.IsNullOrEmpty(b.HoldItemId))
            .Select(b => new Models.MainTaskDownloadModel
            {
                HoldTaskId = b.HoldTaskId,
                HoldItemId = b.HoldItemId,
                ProjectTypeName = b.ProjectTypeName,
                ProjectNumber = b.ProjectNumber,
                TrainCode = b.TrainCode,
                TrainNo = b.TrainNo,
                CarCode = b.CarCode,
                DepId = b.DepId,
                DebugType = b.DebugType,
                ItemName = b.ItemName,
                OperateProcess = b.OperateProcess,
                MutualProcess = b.MutualProcess,
                QualityProcess = b.QualityProcess,
                HoldItems =
                [
                    new HoldItemDto
                    {
                        HoldItemId = b.HoldItemId,
                        ItemName = b.ItemName,
                        OperateProcess = b.OperateProcess,
                        MutualProcess = b.MutualProcess,
                        QualityProcess = b.QualityProcess
                    }
                ]
            })
            .ToList();

        if (models.Count == 0)
            return 0;

        await _taskDownload.GetMainTaskSelectiveAsync(personCode, models);
        NlogHelper.Info($"下载完成：{models.Count} 个任务");
        return models.Count;
    }

    public async Task<bool> UploadTaskResultAsync(int localTaskRowId, string personCode)
    {
        var main = await _taskRepo.GetMainByIdAsync(localTaskRowId);
        if (main == null) return false;

        var done = await _taskRepo.GetCompletedHoldRowsAsync(main.holdTaskId, main.holdItemId);
        if (done.Count == 0)
        {
            NlogHelper.Warn($"任务 {localTaskRowId} 没有任何待回传明细（isComplete=1），跳过上传");
            return false;
        }

        var req = new UploadResultRequest
        {
            DeviceNumber = _api.DeviceNumber,
            HoldTaskId = main.holdTaskId,
            HoldItemId = main.holdItemId,
            List = done.Select(MapUploadDetail).ToList()
        };

        var resp = await _api.PullHoldTaskAsync(req);
        if (!resp.IsSuccess) return false;

        foreach (var row in done)
            await _taskRepo.MarkUploadedAsync(row.ID);
        return true;
    }

    public async Task<bool> UploadDetailResultAsync(int localDetailId, string personCode)
    {
        var detail = await _taskRepo.GetHoldByIdAsync(localDetailId);
        if (detail is not { isComplete: 1 }) return false;

        var req = new UploadResultRequest
        {
            DeviceNumber = _api.DeviceNumber,
            HoldTaskId = detail.holdTaskId,
            HoldItemId = detail.holdItemId,
            List = [MapUploadDetail(detail)]
        };

        var resp = await _api.PullHoldTaskAsync(req);
        if (!resp.IsSuccess) return false;

        await _taskRepo.MarkUploadedAsync(localDetailId);
        return true;
    }

    private static UploadResultDetail MapUploadDetail(HoldTaskTableEntity d) => new()
    {
        DetailId = d.detailId,
        TestTemperature = d.testTemperature,
        TestHumidity = d.testHumidity,
        ApplyVoltage = d.applyVoltage,
        TestValue = d.testValue,
        TestProcessValues = d.testProcessValues,
        Remark = d.remark,
        OperatePerson = d.operatePerson,
        OperatePersonName = d.operatePersonName,
        OperateTime = d.operateTime,
        OperateResult = d.operateResult,
        AuxiliariesCode1 = d.auxiliariesCode1,
        AuxiliariesName1 = d.auxiliariesName1,
        AuxiliariesCode2 = d.auxiliariesCode2,
        AuxiliariesName2 = d.auxiliariesName2,
        AuxiliariesCode3 = d.auxiliariesCode3,
        AuxiliariesName3 = d.auxiliariesName3,
        AuxiliariesCode4 = d.auxiliariesCode4,
        AuxiliariesName4 = d.auxiliariesName4,
        MutualPerson = d.mutualPerson,
        MutualPersonName = d.mutualPersonName,
        MutualTime = d.mutualTime,
        MutualResult = d.mutualResult,
        QualityPerson = d.qualityPerson,
        QualityPersonName = d.qualityPersonName,
        QualityTime = d.qualityTime,
        QualityResult = d.qualityResult,
    };

    public async Task<(bool ok, string? newMd5, string? msg)> ChangeRemotePasswordAsync(
        string personCode, string oldPwd, string newPwd)
    {
        var resp = await _api.ModifyPasswordAsync(personCode, oldPwd, newPwd);
        return (resp.IsSuccess, resp.NewPwd, resp.Msg);
    }
}
