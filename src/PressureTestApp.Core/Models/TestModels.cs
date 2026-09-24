

namespace PressureTestApp.Core.Models;

/// <summary>
/// 试验任务（统一的领域模型，对接在线下发/离线手动/Excel 导入三种来源）
/// </summary>
public class TestTask
{
    public string TaskId { get; set; } = "";
    public string? SubTaskId { get; set; }
    public string CarType { get; set; } = "";
    public string TrainNo { get; set; } = "";
    public string CarNo { get; set; } = "";
    public string? Process { get; set; }
    public string TestItem { get; set; } = "";

    /// <summary>4 路回路电压（kV）</summary>
    public double[] LoopVoltages { get; set; } = new double[4];
    /// <summary>试验时间（秒），默认 60</summary>
    public int DurationSeconds { get; set; } = 60;
    /// <summary>保护电流（mA），默认 500</summary>
    public double ProtectCurrentMa { get; set; } = 500;

    /// <summary>任务来源</summary>
    public TaskSource Source { get; set; }

    /// <summary>状态</summary>
    public TestResult Result { get; set; } = TestResult.Pending;

    /// <summary>开始时间</summary>
    public DateTime? StartedAt { get; set; }
    /// <summary>结束时间</summary>
    public DateTime? FinishedAt { get; set; }
}

/// <summary>
/// 任务来源
/// </summary>
public enum TaskSource
{
    /// <summary>在线下发</summary>
    Online = 0,
    /// <summary>离线手动</summary>
    Offline = 1,
    /// <summary>Excel 模板导入</summary>
    Excel = 2
}

/// <summary>
/// 单回路试验数据
/// </summary>
public class LoopTestData
{
    public int LoopIndex { get; set; }
    public double TargetVoltage { get; set; }
    public double TargetCurrent { get; set; }
    public int DurationSeconds { get; set; }

    public double MaxVoltage { get; set; }
    public double MaxCurrent { get; set; }
    public TestResult Result { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>采样数据（电压、电流随时间）</summary>
    public List<SamplePoint> Samples { get; set; } = new();
}

/// <summary>
/// 单次采样点
/// </summary>
public record SamplePoint(DateTime Time, double Voltage, double Current);

/// <summary>
/// 实时数据快照（用于 UI 显示）
/// </summary>
public class RealtimeData
{
    public double VoltageKv { get; set; }
    public double CurrentMa { get; set; }
    public int RemainSeconds { get; set; }
    public int TotalSeconds { get; set; }
    public int ActiveLoopIndex { get; set; }
    public TestResult Status { get; set; }
    public double EnvironmentTempC { get; set; }
    public double EnvironmentHumidity { get; set; }
}
public class holdItem
{
    /// <summary>
    /// 耐压子任务id
    /// </summary>
    public string holdItemId { get; set; }

    /// <summary>
    /// 耐压项目名称
    /// </summary>
    public string itemName { get; set; }

    /// <summary>
    /// 自检进度
    /// </summary>
    public string operateProcess { get; set; }

    /// <summary>
    /// 互检进度
    /// </summary>
    public string mutualProcess { get; set; }

    /// <summary>
    /// 质检进度
    /// </summary>
    public string qualityProcess { get; set; }
}

