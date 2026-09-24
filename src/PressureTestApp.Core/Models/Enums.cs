namespace PressureTestApp.Core.Models;

/// <summary>
/// 工作模式
/// </summary>
public enum WorkMode
{
    /// <summary>在线模式：连接厂房 WiFi，实时同步任务</summary>
    Online = 0,

    /// <summary>离线模式：独立运行，自定义产品参数</summary>
    Offline = 1,

    /// <summary>离线导入：通过 Excel 模板批量执行</summary>
    ExcelImport = 2
}

/// <summary>
/// 试验结果
/// </summary>
public enum TestResult
{
    /// <summary>未开始</summary>
    Pending = 0,
    /// <summary>进行中</summary>
    Running = 1,
    /// <summary>通过</summary>
    Passed = 2,
    /// <summary>击穿/失败</summary>
    Breakdown = 3,
    /// <summary>用户停止</summary>
    Stopped = 4,
    /// <summary>异常</summary>
    Error = 5
}

/// <summary>
/// 用户角色
/// </summary>
public enum UserRole
{
    /// <summary>普通操作员</summary>
    Operator = 0,
    /// <summary>辅助员</summary>
    Assistant = 1,
    /// <summary>互检员</summary>
    Inspector = 2,
    /// <summary>质检员</summary>
    QualityChecker = 3,
    /// <summary>管理员</summary>
    Admin = 99
}
