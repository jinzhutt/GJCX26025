using System.Net;
using System.Text.Json;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using RestSharp;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// 轴承退卸站 MES 临时联调：当前测 4.4 状态 / 4.5 故障 / 4.3 回传。
/// 前缀、设备ID、接口路径一律读 ProductionSystemConfig.ini。
/// </summary>
public static class MesInterfaceTest
{
    private static readonly TimeSpan Timeout = MesReachability.HttpTimeout;

    private sealed class MesTestConfig
    {
        public required string BaseUrl { get; init; }
        public required string StationCode { get; init; }
        public required string DeviceId { get; init; }
        public required string UploadWorkDataPath { get; init; }
        public required string UploadDeviceStatusPath { get; init; }
        public required string UploadDeviceFaultPath { get; init; }
    }

    private static MesTestConfig ReadMesConfig()
    {
        var cfg = new ProductionSystemConfig();
        var baseUrl = (cfg.MesBaseUrl ?? "").Trim().TrimEnd('/');
        var station = (cfg.MesStationCode ?? "").Trim();
        var deviceId = (cfg.MesDeviceId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("未配置 MES 接口前缀（ProductionSystemConfig.ini · MES接口前缀）。");
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new InvalidOperationException("未配置设备ID（ProductionSystemConfig.ini · MES设备ID）。");

        return new MesTestConfig
        {
            BaseUrl = baseUrl,
            StationCode = station,
            DeviceId = deviceId,
            UploadWorkDataPath = NormalizePath(cfg.MesUploadWorkDataPath, "/integration/uploadWorkData"),
            UploadDeviceStatusPath = NormalizePath(cfg.MesUploadDeviceStatusPath, "/integration/uploadDeviceStatus"),
            UploadDeviceFaultPath = NormalizePath(cfg.MesUploadDeviceFaultPath, "/integration/uploadDeviceFault")
        };
    }

    /// <summary>
    /// 联调入口：依次测 4.4 设备状态、4.5 设备故障、4.3 数据回传。
    /// base64Image：有右键保存的曲线 Base64 则传入；未保存传 null/空。
    /// </summary>
    public static async Task<(MesTestResult Status, MesTestResult Fault, MesTestResult Upload)>
        RunStatusFaultUploadAsync(string? base64Image = null, CancellationToken cancellationToken = default)
    {
        MesTestConfig mes;
        try
        {
            mes = ReadMesConfig();
        }
        catch (Exception ex)
        {
            var fail = Log(new MesTestResult("MES 配置", false, 0, null, ex.Message, ""));
            return (fail, fail, fail);
        }

        var (ok, error) = await MesReachability.TryReachAsync(mes.BaseUrl, cancellationToken: cancellationToken);
        if (!ok)
        {
            var fail = Log(new MesTestResult("MES 连通性", false, 0, null, error ?? "无法连接 MES", ""));
            var skipFault = Log(MesTestResult.Skipped("4.5 设备故障上报", "MES 未连通，已跳过"));
            var skipUpload = Log(MesTestResult.Skipped("4.3 设备作业数据回传", "MES 未连通，已跳过"));
            return (fail, skipFault, skipUpload);
        }

        NlogHelper.Info($"MES 联调：4.4/4.5/4.3，base={mes.BaseUrl}，deviceId={mes.DeviceId}");
        var status = await TestUploadDeviceStatusAsync(cancellationToken);
        var fault = await TestUploadDeviceFaultAsync(cancellationToken);
        var upload = await TestUploadWorkDataOnlyAsync(
            skipReachCheck: true,
            base64Image: base64Image,
            cancellationToken: cancellationToken);
        return (status, fault, upload);
    }

    /// <summary>4.4 设备状态上报（联调模拟「待机」）。</summary>
    public static async Task<MesTestResult> TestUploadDeviceStatusAsync(
        CancellationToken cancellationToken = default)
    {
        MesTestConfig mes;
        try
        {
            mes = ReadMesConfig();
        }
        catch (Exception ex)
        {
            return Log(new MesTestResult("MES 配置", false, 0, null, ex.Message, ""));
        }

        var body = new
        {
            deviceId = mes.DeviceId,
            status = "待机",
            occurTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            remark = "MES 接口联调：设备状态上报"
        };

        var jsonBody = JsonSerializer.Serialize(body, MesJson.WriteOptions);
        var request = new RestRequest(mes.UploadDeviceStatusPath, Method.Post)
            .AddHeader("Content-Type", "application/json;charset=UTF-8")
            .AddHeader("Accept", "application/json")
            .AddMesJsonBody(jsonBody);

        return await ExecuteAsync(mes.BaseUrl, "4.4 设备状态上报", request, cancellationToken, jsonBody);
    }

    /// <summary>4.5 设备故障上报（联调模拟故障说明）。</summary>
    public static async Task<MesTestResult> TestUploadDeviceFaultAsync(
        CancellationToken cancellationToken = default)
    {
        MesTestConfig mes;
        try
        {
            mes = ReadMesConfig();
        }
        catch (Exception ex)
        {
            return Log(new MesTestResult("MES 配置", false, 0, null, ex.Message, ""));
        }

        var body = new
        {
            deviceId = mes.DeviceId,
            occurTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            errorMessage = $"联调模拟故障：感应器故障，无法作业（{DateTime.Now:HH:mm:ss}）"
        };

        var jsonBody = JsonSerializer.Serialize(body, MesJson.WriteOptions);
        var request = new RestRequest(mes.UploadDeviceFaultPath, Method.Post)
            .AddHeader("Content-Type", "application/json;charset=UTF-8")
            .AddHeader("Accept", "application/json")
            .AddMesJsonBody(jsonBody);

        return await ExecuteAsync(mes.BaseUrl, "4.5 设备故障上报", request, cancellationToken, jsonBody);
    }

    /// <summary>仅测试 4.3 设备作业数据回传。</summary>
    public static async Task<MesTestResult> TestUploadWorkDataOnlyAsync(
        bool skipReachCheck = false,
        string? base64Image = null,
        CancellationToken cancellationToken = default)
    {
        MesTestConfig mes;
        try
        {
            mes = ReadMesConfig();
        }
        catch (Exception ex)
        {
            return Log(new MesTestResult("MES 配置", false, 0, null, ex.Message, ""));
        }

        if (!skipReachCheck)
        {
            var (ok, error) = await MesReachability.TryReachAsync(mes.BaseUrl, cancellationToken: cancellationToken);
            if (!ok)
            {
                return Log(new MesTestResult(
                    "MES 连通性",
                    false,
                    0,
                    null,
                    error ?? "无法连接 MES",
                    ""));
            }
        }

        var taskId = $"TEST-BEARING-{DateTime.Now:yyyyMMddHHmmss}";
        const string partCode = "ZC-PART-MOCK-001";
        NlogHelper.Info(
            $"MES 联调回传：taskId={taskId}，partCode={partCode}，base={mes.BaseUrl}，path={mes.UploadWorkDataPath}");
        return await TestUploadWorkDataAsync(taskId, partCode, base64Image, cancellationToken);
    }

    /// <summary>4.3 设备作业数据回传（轴承退卸：顶层 base64Image）。</summary>
    public static Task<MesTestResult> TestUploadWorkDataAsync(
        string taskId,
        string partCode,
        string? base64Image = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("taskId 不能为空", nameof(taskId));
        if (string.IsNullOrWhiteSpace(partCode))
            throw new ArgumentException("partCode 不能为空", nameof(partCode));

        MesTestConfig mes;
        try
        {
            mes = ReadMesConfig();
        }
        catch (Exception ex)
        {
            return Task.FromResult(Log(new MesTestResult("MES 配置", false, 0, null, ex.Message, "")));
        }

        var image = (base64Image ?? "").Trim();
        var body = new Dictionary<string, object?>
        {
            ["taskId"] = taskId.Trim(),
            ["partCode"] = partCode.Trim(),
            ["deviceId"] = mes.DeviceId,
            // 客户要求：回传时开始/结束时间为空
            ["startTime"] = "",
            ["endTime"] = "",
            ["worker"] = ResolveMesTestWorker(),
            ["testResult"] = "成功",
            ["remark"] = "MES 接口联调：轴承退卸模拟回传",
            ["data"] = new Dictionary<string, string>(),
            ["base64Image"] = image
        };

        var jsonBody = JsonSerializer.Serialize(body, MesJson.WriteOptions);
        NlogHelper.Info(
            $"MES 4.3 联调请求 JSON（base64Image长度={(string.IsNullOrEmpty(image) ? 0 : image.Length)}）：" +
            TruncateForLog(jsonBody, 500));

        var request = new RestRequest(mes.UploadWorkDataPath, Method.Post)
            .AddHeader("Content-Type", "application/json;charset=UTF-8")
            .AddHeader("Accept", "application/json")
            .AddMesJsonBody(jsonBody);

        return ExecuteAsync(mes.BaseUrl, "4.3 设备作业数据回传", request, cancellationToken, jsonBody);
    }

    private static string TruncateForLog(string text, int max) =>
        string.IsNullOrEmpty(text) ? "(空)"
            : text.Length <= max ? text
            : text[..max] + "…";

    private static string ResolveMesTestWorker()
    {
        var name = VarHelper.CurrentUserName?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return name!;
        NlogHelper.Warn("MES 联调：未登录，worker 使用占位「未登录操作员」");
        return "未登录操作员";
    }

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

    private static async Task<MesTestResult> ExecuteAsync(
        string baseUrl,
        string interfaceName,
        RestRequest request,
        CancellationToken cancellationToken,
        string? requestBodyJson = null)
    {
        try
        {
            using var client = new RestClient(new RestClientOptions(baseUrl) { Timeout = Timeout });
            var response = await client.ExecuteAsync(request, cancellationToken);
            var raw = response.Content ?? "";

            if (response.ResponseStatus != ResponseStatus.Completed)
                return Log(new MesTestResult(interfaceName, false, response.StatusCode, null,
                    response.ErrorMessage ?? response.ResponseStatus.ToString(), raw, requestBodyJson));

            if (response.StatusCode != HttpStatusCode.OK)
                return Log(new MesTestResult(interfaceName, false, response.StatusCode, null,
                    $"HTTP 状态码错误：{(int)response.StatusCode}", raw, requestBodyJson));

            try
            {
                using var json = JsonDocument.Parse(raw);
                var root = json.RootElement;
                var code = root.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var parsedCode)
                    ? parsedCode
                    : (int?)null;
                var msg = root.TryGetProperty("msg", out var msgElement)
                    ? msgElement.GetString() ?? ""
                    : root.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString() ?? ""
                        : "响应中没有 msg 或 message 字段";

                return Log(new MesTestResult(interfaceName, code == 200, response.StatusCode, code, msg, raw, requestBodyJson));
            }
            catch (JsonException ex)
            {
                return Log(new MesTestResult(interfaceName, false, response.StatusCode, null,
                    $"响应不是有效 JSON：{ex.Message}", raw, requestBodyJson));
            }
        }
        catch (Exception ex)
        {
            return Log(new MesTestResult(interfaceName, false, 0, null, ex.Message, "", requestBodyJson));
        }
    }

    private static MesTestResult Log(MesTestResult result)
    {
        if (result.Success)
            NlogHelper.Info($"MES联调 {result.InterfaceName} 成功：code={result.Code}，msg={result.Message}");
        else
            NlogHelper.Warn($"MES联调 {result.InterfaceName} 失败：code={result.Code}，msg={result.Message}");
        return result;
    }
}

/// <summary>单个 MES 接口的联调结果。</summary>
public sealed class MesTestResult
{
    public MesTestResult(
        string interfaceName,
        bool success,
        HttpStatusCode httpStatus,
        int? code,
        string message,
        string rawResponse,
        string? requestBodyJson = null)
    {
        InterfaceName = interfaceName;
        Success = success;
        HttpStatus = httpStatus;
        Code = code;
        Message = message ?? "";
        RawResponse = rawResponse ?? "";
        RequestBodyJson = requestBodyJson ?? "";
    }

    public string InterfaceName { get; }
    public bool Success { get; }
    public HttpStatusCode HttpStatus { get; }
    public int? Code { get; }
    public string Message { get; }
    public string RawResponse { get; }
    /// <summary>实际发出的请求 JSON（联调核对用；不是 Dictionary.ToString）。</summary>
    public string RequestBodyJson { get; }

    public static MesTestResult Skipped(string interfaceName, string reason) =>
        new(interfaceName, false, 0, null, reason, "");
}
