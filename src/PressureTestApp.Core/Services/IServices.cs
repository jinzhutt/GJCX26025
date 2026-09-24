using PressureTestApp.Core.Models;

namespace PressureTestApp.Core.Services;

/// <summary>
/// PLC 通信抽象（替代原 OPC，实现用 S7NetPlus）
/// </summary>
public interface IPlcCommunicator : IDisposable
{
    bool IsConnected { get; }
    Task<bool> ConnectAsync();
    Task DisconnectAsync();

    Task<bool> ReadBoolAsync(string address);
    Task<short> ReadInt16Async(string address);
    Task<int> ReadInt32Async(string address);
    Task<float> ReadFloatAsync(string address);
    Task WriteBoolAsync(string address, bool value);
    Task WriteInt16Async(string address, short value);
    Task WriteFloatAsync(string address, float value);

    /// <summary>数据变化事件</summary>
    event EventHandler<PlcDataChangedEventArgs>? DataChanged;

    /// <summary>连接状态变化事件</summary>
    event EventHandler<bool>? ConnectionChanged;
}

public class PlcDataChangedEventArgs : EventArgs
{
    public string Address { get; set; } = "";
    public object? Value { get; set; }
}

/// <summary>
/// 串口 Modbus 仪器服务（预留两路高压表/温湿度仪等）
/// </summary>
public interface ISerialInstrumentService : IDisposable
{
    /// <summary>枚举系统当前可用串口</summary>
    string[] GetAvailablePorts();

    Task<bool> ConnectAsync(string instrumentId);
    Task DisconnectAsync(string instrumentId);
    bool IsConnected(string instrumentId);

    Task<Dictionary<string, double>> ReadAllRegistersAsync(string instrumentId);

    event EventHandler<InstrumentDataEventArgs>? DataReceived;
}

public class InstrumentDataEventArgs : EventArgs
{
    public string InstrumentId { get; set; } = "";
    public Dictionary<string, double> Values { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// 人脸识别服务（当前只是占位，硬件接入后切换实现）
/// </summary>
public interface IFaceAuthService
{
    /// <summary>当前环境是否支持</summary>
    bool IsAvailable { get; }

    /// <summary>启动一次人脸认证（占位实现直接抛 NotSupported）</summary>
    Task<FaceAuthResult> AuthenticateAsync(CancellationToken ct = default);

    /// <summary>注册某用户人脸</summary>
    Task<bool> RegisterAsync(int userId, string userName);

    /// <summary>移除某用户人脸</summary>
    Task<bool> RemoveAsync(int userId);
}

public class FaceAuthResult
{
    public bool Success { get; set; }
    public int? UserId { get; set; }
    public string? Message { get; set; }

    public static FaceAuthResult NotSupported() => new()
    {
        Success = false,
        Message = "当前设备未接入人脸识别硬件"
    };
}

/// <summary>
/// Excel 导入服务
/// </summary>
public interface IExcelImportService
{
    /// <summary>导入 Excel 模板，返回任务列表 + 错误信息</summary>
    Task<ExcelImportResult> ImportAsync(string filePath);

    /// <summary>生成空白模板文件供用户下载</summary>
    Task ExportTemplateAsync(string filePath);
}

public class ExcelImportResult
{
    public List<TestTask> Tasks { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool IsSuccess => Errors.Count == 0 && Tasks.Count > 0;
    public string? FileName { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 生产线 API 服务（任务下载/上传）
/// </summary>
public interface ITaskApiService
{
    /// <summary>设备认证（首次接入用 deviceMac）</summary>
    Task<DeviceAuthResult> AuthorizeDeviceAsync(string deviceMac);

    /// <summary>当前设备认证编码（认证后保存）</summary>
    string? DeviceNumber { get; }

    /// <summary>同步人员到本地数据库</summary>
    Task<int> SyncPersonsAsync(string personCode);

    /// <summary>同步车型车列到本地数据库</summary>
    Task<(int projectCount, int trainCount)> SyncProjectsAndTrainsAsync(string personCode);

    /// <summary>从 EPE 拉取所有任务（不下载明细）</summary>
    Task<List<RemoteTaskBrief>> FetchTaskListAsync(string personCode);

    /// <summary>下载选中任务的明细到本地数据库</summary>
    Task<int> DownloadTaskDetailsAsync(string personCode, IEnumerable<RemoteTaskBrief> selected);

    /// <summary>上传一个任务下所有完成的明细</summary>
    Task<bool> UploadTaskResultAsync(int localTaskRowId, string personCode);

    /// <summary>上传单条已完成的明细（任务回传勾选行）</summary>
    Task<bool> UploadDetailResultAsync(int localDetailId, string personCode);
}

/// <summary>设备 MAC 认证结果</summary>
public record DeviceAuthResult(bool Success, string? DeviceNumber, string Message)
{
    public static DeviceAuthResult Ok(string deviceNumber) =>
        new(true, deviceNumber, "设备认证成功！");

    public static DeviceAuthResult Fail(string message) =>
        new(false, null, string.IsNullOrWhiteSpace(message) ? "设备认证失败" : message);
}

/// <summary>
/// EPE 任务概要（findHoldTaskList 返回的一行 + 选中状态）
/// </summary>
public class RemoteTaskBrief
{
    public string HoldTaskId { get; set; } = "";
    public string? HoldItemId { get; set; }
    public string? ProjectTypeName { get; set; }
    public string? ProjectNumber { get; set; }
    public string? TrainCode { get; set; }
    public string? TrainNo { get; set; }
    public string? CarCode { get; set; }
    public string? DebugType { get; set; }
    public string? ItemName { get; set; }
    public string? DepId { get; set; }
    public string? OperateProcess { get; set; }
    public string? MutualProcess { get; set; }
    public string? QualityProcess { get; set; }
    public bool IsSelected { get; set; }
}

/// <summary>
/// 试验引擎（核心业务，控制 4 路顺序耐压流程）
/// </summary>
public interface ITestEngineService
{
    bool IsRunning { get; }

    /// <summary>开始试验</summary>
    Task StartAsync(TestTask task);

    /// <summary>停止试验</summary>
    Task StopAsync();

    /// <summary>实时数据更新事件（10Hz 推送）</summary>
    event EventHandler<RealtimeData>? RealtimeUpdated;

    /// <summary>单回路完成事件</summary>
    event EventHandler<LoopTestData>? LoopFinished;

    /// <summary>整体试验完成事件</summary>
    event EventHandler<TestTask>? TaskFinished;
}

/// <summary>
/// 用户认证服务
/// </summary>
public interface IAuthService
{
    /// <summary>密码登录</summary>
    Task<AuthResult> LoginByPasswordAsync(string userId, string password);

    /// <summary>修改密码</summary>
    Task<bool> ChangePasswordAsync(string userId, string oldPassword, string newPassword);

    /// <summary>注销当前会话</summary>
    void Logout();
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? UserName { get; set; }
    public UserRole Role { get; set; }
    public string? Message { get; set; }
}
