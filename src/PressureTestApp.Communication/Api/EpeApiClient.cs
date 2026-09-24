using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using RestSharp;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// EPE 数字化电气施工生产线系统 API 客户端（与旧版 RestClientHelper + ProductionSystemConfig.ini 一致：每个接口为完整 URL）。
/// </summary>
public class EpeApiClient : IDisposable
{
    private const int ProcedureType = 7;
    private const int TimeoutSeconds = 30;

    private readonly ProductionSystemConfig _production;

    /// <summary>当前设备认证编码（认证后保存，请求时作为 deviceNumber）</summary>
    public string? DeviceNumber { get; set; }

    public EpeApiClient(ProductionSystemConfig production)
    {
        _production = production;
    }

    public void Dispose() { }

    public async Task<DeviceAuthorizeResponse> DeviceAuthorizeAsync(string deviceMac)
    {
        return await ExecuteFormAsync<DeviceAuthorizeResponse>(
            _production.Empower, Method.Post, ("deviceMac", deviceMac));
    }

    public async Task<ApiResponse> HeartbeatAsync(string personCode)
    {
        return await ExecuteFormAsync<ApiResponse>(
            _production.Heartbeat, Method.Post,
            ("deviceNumber", DeviceNumber ?? ""),
            ("personCode", personCode));
    }

    public async Task<ApiResponse<PersonDto>> FindPersonListAsync(string personCode)
    {
        return await ExecuteFormAsync<ApiResponse<PersonDto>>(
            _production.Personnel, Method.Post,
            ("deviceNumber", DeviceNumber ?? ""),
            ("personCode", personCode));
    }

    public async Task<ModifyPasswordResponse> ModifyPasswordAsync(string personCode, string oldPwd, string newPwd)
    {
        return await ExecuteFormAsync<ModifyPasswordResponse>(
            _production.ChangePassword, Method.Post,
            ("deviceNumber", DeviceNumber ?? ""),
            ("personCode", personCode),
            ("oldPwd", oldPwd),
            ("newPwd", newPwd));
    }

    public async Task<ApiResponse<ProjectDto>> FindAllProjectListAsync(string personCode)
    {
        return await ExecuteFormAsync<ApiResponse<ProjectDto>>(
            _production.VehicleData, Method.Post,
            ("deviceNumber", DeviceNumber ?? ""),
            ("personCode", personCode));
    }

    public async Task<ApiResponse<TrainDto>> FindAllTrainListAsync(string personCode, int? projectId = null)
    {
        var parameters = new List<(string, object)>
        {
            ("deviceNumber", DeviceNumber ?? ""),
            ("personCode", personCode)
        };
        if (projectId.HasValue)
            parameters.Add(("projectId", projectId.Value));

        return await ExecuteFormAsync<ApiResponse<TrainDto>>(
            _production.TrainData, Method.Post, parameters.ToArray());
    }

    public async Task<ApiResponse<HoldTaskDto>> FindHoldTaskListAsync(string personCode)
    {
        var resp = await ExecuteFormAsync<ApiResponse<HoldTaskDto>>(
            _production.GetMainTask, Method.Get,
            ("procedureType", ProcedureType),
            ("deviceNumber", DeviceNumber ?? ""),
            ("personCode", personCode));
        NlogHelper.Info($"任务列表返回：{resp.Total} 条");
        return resp;
    }

    public async Task<ApiResponse<HoldTaskDetailDto>> FindHoldTaskDetailAsync(
        string personCode, string holdTaskId, string holdItemId)
    {
        var resp = await ExecuteFormAsync<ApiResponse<HoldTaskDetailDto>>(
            _production.GetHoldTask, Method.Get,
            ("procedureType", ProcedureType),
            ("deviceNumber", DeviceNumber ?? ""),
            ("personCode", personCode),
            ("holdTaskId", holdTaskId),
            ("holdItemId", holdItemId));
        NlogHelper.Info($"任务 {holdTaskId} 明细返回：{resp.Total} 条");
        return resp;
    }

    public async Task<ApiResponse> PullHoldTaskAsync(UploadResultRequest data)
    {
        if (string.IsNullOrWhiteSpace(_production.ResultFeedback))
            return new ApiResponse { State = "0", Msg = "耐压任务结果回传接口未配置" };

        try
        {
            var options = new RestClientOptions(_production.ResultFeedback.Trim())
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
            using var client = new RestClient(options);
            var req = new RestRequest("", Method.Post);
            req.AddJsonBody(new
            {
                procedureType = ProcedureType,
                deviceNumber = data.DeviceNumber,
                holdTaskId = data.HoldTaskId,
                holdItemId = data.HoldItemId,
                list = data.List
            });

            var response = await client.ExecuteAsync<ApiResponse>(req);
            if (response.Data == null)
                return new ApiResponse { State = "0", Msg = "返回为空" };

            if (response.Data.IsSuccess)
                NlogHelper.Info($"任务 {data.HoldTaskId} 上传成功，{data.List?.Count} 条明细");
            else
                NlogHelper.Warn($"任务上传失败：{response.Data.Msg}");

            return response.Data;
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "任务上传异常");
            return new ApiResponse { State = "0", Msg = ex.Message };
        }
    }

    private async Task<T> ExecuteFormAsync<T>(
        string? url,
        Method method,
        params (string name, object value)[] parameters) where T : ApiResponse, new()
    {
        if (string.IsNullOrWhiteSpace(url))
            return new T { State = "0", Msg = "接口地址未配置" };

        try
        {
            var options = new RestClientOptions(url.Trim())
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
            using var client = new RestClient(options);
            var req = new RestRequest("", method);
            foreach (var (name, value) in parameters)
                req.AddParameter(name, value?.ToString() ?? "");

            var response = await client.ExecuteAsync<T>(req);
            if (response.Data == null)
            {
                NlogHelper.Warn($"接口返回为空：{url}");
                return new T { State = "0", Msg = "返回为空" };
            }

            if (typeof(T) == typeof(DeviceAuthorizeResponse) && response.Data.IsSuccess)
            {
                var auth = response.Data as DeviceAuthorizeResponse;
                if (auth != null && !string.IsNullOrEmpty(auth.DeviceNumber))
                {
                    DeviceNumber = auth.DeviceNumber;
                    NlogHelper.Info($"设备授权成功 → {DeviceNumber}");
                }
            }

            return response.Data;
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, $"接口请求异常：{url}");
            return new T { State = "0", Msg = ex.Message };
        }
    }
}
