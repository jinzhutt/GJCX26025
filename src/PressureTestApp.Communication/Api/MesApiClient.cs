using System.Text.Json;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using RestSharp;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// MES WebAPI 客户端（《设备与 MES 系统交互接口协议》4.1–4.5）。
/// 路径与前缀均来自 ProductionSystemConfig.ini。
/// </summary>
public sealed class MesApiClient : IDisposable
{
    private static readonly TimeSpan Timeout = MesReachability.HttpTimeout;
    private readonly ProductionSystemConfig _config;

    public MesApiClient(ProductionSystemConfig config) => _config = config;

    public void Dispose() { }

    public string DeviceId => (_config.MesDeviceId ?? "").Trim();
    public string StationCode => (_config.MesStationCode ?? "").Trim();
    public string BaseUrl => (_config.MesBaseUrl ?? "").Trim().TrimEnd('/');

    /// <summary>先 TCP 探测，不通则约 2 秒失败，避免长时间卡住遮罩。</summary>
    public Task<(bool Ok, string? Error)> CanReachAsync(CancellationToken cancellationToken = default) =>
        MesReachability.TryReachAsync(BaseUrl, cancellationToken: cancellationToken);

    /// <summary>探测失败直接抛错，供任务下载等入口快速返回。</summary>
    public async Task EnsureReachableAsync(CancellationToken cancellationToken = default)
    {
        EnsureMesConfig();
        var (ok, error) = await CanReachAsync(cancellationToken);
        if (!ok)
            throw new InvalidOperationException(error ?? "无法连接 MES。");
    }

    /// <summary>4.1 获取作业工件信息。</summary>
    public async Task<MesApiResponse<List<MesWorkpieceInfoDto>>> GetWorkpieceInfoAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureReachableAsync(cancellationToken);
        var path = NormalizePath(_config.MesGetWorkpieceInfoPath, "/integration/getWorkpieceInfo");
        using var client = CreateClient();
        var request = new RestRequest(path, Method.Get)
            .AddQueryParameter("stationCode", StationCode)
            .AddQueryParameter("deviceId", DeviceId)
            .AddHeader("Accept", "application/json");

        NlogHelper.Info($"MES 4.1 请求：{BaseUrl}{path}?stationCode={StationCode}&deviceId={DeviceId}");
        return await ExecuteJsonAsync<List<MesWorkpieceInfoDto>>(client, request, "4.1", cancellationToken);
    }

    /// <summary>4.3 设备作业数据回传。</summary>
    public async Task<MesApiResponse<object>> UploadWorkDataAsync(
        MesWorkDataUploadRequest payload,
        CancellationToken cancellationToken = default)
    {
        await EnsureReachableAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload.TaskId))
            throw new ArgumentException("taskId 不能为空", nameof(payload));
        if (string.IsNullOrWhiteSpace(payload.PartCode))
            throw new ArgumentException("partCode 不能为空", nameof(payload));

        if (string.IsNullOrWhiteSpace(payload.DeviceId))
            payload.DeviceId = DeviceId;

        // 协议：曲线走顶层 base64Image（Base64）；data 不再放曲线
        var base64Image = (payload.Base64Image ?? "").Trim();

        var body = new Dictionary<string, object?>
        {
            ["taskId"] = payload.TaskId.Trim(),
            ["partCode"] = payload.PartCode.Trim(),
            ["deviceId"] = payload.DeviceId.Trim(),
            // 客户要求：回传时开始/结束时间为空
            ["startTime"] = "",
            ["endTime"] = "",
            ["worker"] = payload.Worker,
            ["testResult"] = payload.TestResult,
            ["remark"] = payload.Remark ?? "",
            ["data"] = new Dictionary<string, string>(),
            ["base64Image"] = base64Image
        };

        var path = NormalizePath(_config.MesUploadWorkDataPath, "/integration/uploadWorkData");
        var jsonBody = System.Text.Json.JsonSerializer.Serialize(body, MesJson.WriteOptions);
        using var client = CreateClient();
        var request = new RestRequest(path, Method.Post)
            .AddHeader("Content-Type", "application/json;charset=UTF-8")
            .AddHeader("Accept", "application/json")
            .AddMesJsonBody(jsonBody);

        NlogHelper.Info(
            $"MES 4.3 请求：{path}，taskId={payload.TaskId}，partCode={payload.PartCode}，worker={payload.Worker}，" +
            $"base64Image长度={(string.IsNullOrEmpty(base64Image) ? 0 : base64Image.Length)}");
        return await ExecuteJsonAsync<object>(client, request, "4.3", cancellationToken);
    }

    /// <summary>4.4 设备状态上报（试验开始/复位等）。</summary>
    public async Task<MesApiResponse<object>> UploadDeviceStatusAsync(
        MesDeviceStatusUploadRequest payload,
        CancellationToken cancellationToken = default)
    {
        EnsureMesConfig();
        if (string.IsNullOrWhiteSpace(payload.DeviceId))
            payload.DeviceId = DeviceId;
        if (string.IsNullOrWhiteSpace(payload.OccurTime))
            payload.OccurTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var body = new
        {
            deviceId = payload.DeviceId.Trim(),
            status = payload.Status,
            occurTime = payload.OccurTime,
            remark = payload.Remark ?? ""
        };

        var path = NormalizePath(_config.MesUploadDeviceStatusPath, "/integration/uploadDeviceStatus");
        var jsonBody = System.Text.Json.JsonSerializer.Serialize(body, MesJson.WriteOptions);
        using var client = CreateClient();
        var request = new RestRequest(path, Method.Post)
            .AddHeader("Content-Type", "application/json;charset=UTF-8")
            .AddHeader("Accept", "application/json")
            .AddMesJsonBody(jsonBody);

        NlogHelper.Info($"MES 4.4 请求：{path}，status={payload.Status}，occurTime={payload.OccurTime}");
        return await ExecuteJsonAsync<object>(client, request, "4.4", cancellationToken);
    }

    /// <summary>4.5 设备故障上报（仅有故障时调用，无故障不上报）。</summary>
    public async Task<MesApiResponse<object>> UploadDeviceFaultAsync(
        MesDeviceFaultUploadRequest payload,
        CancellationToken cancellationToken = default)
    {
        EnsureMesConfig();
        if (string.IsNullOrWhiteSpace(payload.ErrorMessage))
            throw new ArgumentException("无故障说明时不应调用故障上报", nameof(payload));

        if (string.IsNullOrWhiteSpace(payload.DeviceId))
            payload.DeviceId = DeviceId;
        if (string.IsNullOrWhiteSpace(payload.OccurTime))
            payload.OccurTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var body = new
        {
            deviceId = payload.DeviceId.Trim(),
            occurTime = payload.OccurTime,
            errorMessage = payload.ErrorMessage
        };

        var path = NormalizePath(_config.MesUploadDeviceFaultPath, "/integration/uploadDeviceFault");
        var jsonBody = System.Text.Json.JsonSerializer.Serialize(body, MesJson.WriteOptions);
        using var client = CreateClient();
        var request = new RestRequest(path, Method.Post)
            .AddHeader("Content-Type", "application/json;charset=UTF-8")
            .AddHeader("Accept", "application/json")
            .AddMesJsonBody(jsonBody);

        NlogHelper.Info($"MES 4.5 请求：{path}，errorMessage={payload.ErrorMessage}");
        return await ExecuteJsonAsync<object>(client, request, "4.5", cancellationToken);
    }

    private RestClient CreateClient() =>
        new(new RestClientOptions(BaseUrl) { Timeout = Timeout });

    private void EnsureMesConfig()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("未配置 MES 接口前缀（ProductionSystemConfig.ini · MES接口前缀）。");
        if (string.IsNullOrWhiteSpace(StationCode))
            throw new InvalidOperationException("未配置工位编码（ProductionSystemConfig.ini · MES工位编码）。");
        if (string.IsNullOrWhiteSpace(DeviceId))
            throw new InvalidOperationException("未配置设备ID（ProductionSystemConfig.ini · MES设备ID）。");
    }

    /// <summary>规范化相对路径：去空白、去尾部 ?、保证以 / 开头。</summary>
    private static string NormalizePath(string? configured, string fallback)
    {
        var p = (configured ?? "").Trim();
        if (string.IsNullOrWhiteSpace(p))
            p = fallback;
        p = p.TrimEnd('?', '&', '/');
        if (!p.StartsWith('/'))
            p = "/" + p;
        return p;
    }

    private static async Task<MesApiResponse<T>> ExecuteJsonAsync<T>(
        RestClient client,
        RestRequest request,
        string tag,
        CancellationToken cancellationToken)
    {
        var response = await client.ExecuteAsync(request, cancellationToken);
        var raw = response.Content ?? "";

        if (response.ResponseStatus != ResponseStatus.Completed)
            throw new InvalidOperationException(response.ErrorMessage ?? response.ResponseStatus.ToString());

        if ((int)response.StatusCode is < 200 or >= 300)
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}：{raw}");

        try
        {
            var parsed = JsonSerializer.Deserialize<MesApiResponse<T>>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed == null)
                throw new InvalidOperationException("MES 响应解析为空。");

            NlogHelper.Info($"MES {tag} 响应：code={parsed.Code}，msg={parsed.StatusText}，success={parsed.Success}");
            return parsed;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"MES 响应不是有效 JSON：{ex.Message}；原文={raw}", ex);
        }
    }
}
