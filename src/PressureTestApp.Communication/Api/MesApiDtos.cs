    using System.Text.Json.Serialization;

namespace PressureTestApp.Communication.Api;

/// <summary>MES 通用响应外壳（协议 2.2；实际环境可能用 message 或 msg）。</summary>
public sealed class MesApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }

    public string StatusText =>
        !string.IsNullOrWhiteSpace(Message) ? Message!
        : !string.IsNullOrWhiteSpace(Msg) ? Msg!
        : "";

    public bool IsOk => Code == 200 || Success;
}

/// <summary>协议 4.1 返回结果中的作业工件信息。</summary>
public sealed class MesWorkpieceInfoDto
{
    [JsonPropertyName("trainType")]
    public string? TrainType { get; set; }

    [JsonPropertyName("trainNo")]
    public string? TrainNo { get; set; }

    /// <summary>部分示例写作 codeValue，优先用 partCode。</summary>
    [JsonPropertyName("partCode")]
    public string? PartCode { get; set; }

    [JsonPropertyName("codeValue")]
    public string? CodeValue { get; set; }

    [JsonPropertyName("partType")]
    public string? PartType { get; set; }

    [JsonPropertyName("taskId")]
    public string? TaskId { get; set; }

    public string ResolvedPartCode =>
        !string.IsNullOrWhiteSpace(PartCode) ? PartCode!
        : CodeValue ?? "";
}

/// <summary>协议 4.3 设备作业数据回传请求。</summary>
public sealed class MesWorkDataUploadRequest
{
    public string TaskId { get; set; } = "";
    public string PartCode { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string Worker { get; set; } = "";
    public string TestResult { get; set; } = "成功";
    public string Remark { get; set; } = "";
    /// <summary>曲线图 PNG Base64，写入协议顶层字段 base64Image（不再放 data、不用路径）。</summary>
    public string Base64Image { get; set; } = "";
}

/// <summary>协议 4.4 设备状态上报请求。</summary>
public sealed class MesDeviceStatusUploadRequest
{
    public string DeviceId { get; set; } = "";
    public string Status { get; set; } = "";
    public string OccurTime { get; set; } = "";
    public string Remark { get; set; } = "";
}

/// <summary>协议 4.5 设备故障上报请求（仅有故障时上报）。</summary>
public sealed class MesDeviceFaultUploadRequest
{
    public string DeviceId { get; set; } = "";
    public string OccurTime { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}
