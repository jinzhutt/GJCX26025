using PressureTestApp.Communication.Api;

namespace PressureTestApp.Communication.Models;

/// <summary>与 GJCS MainTaskResultModel 一致：findHoldTaskList 接口返回外壳。</summary>
public sealed class MainTaskListResult
{
    public string? Msg { get; set; }
    public string? Total { get; set; }
    public string? State { get; set; }
    public List<HoldTaskDto>? List { get; set; }

    public bool IsSuccess => State == "1";
}

/// <summary>与 GJCS MainTaskModel 一致：勾选后用于落库与拉取子任务明细。</summary>
public sealed class MainTaskDownloadModel
{
    public string HoldTaskId { get; set; } = "";
    public string? ProjectTypeName { get; set; }
    public string? ProjectNumber { get; set; }
    public string? TrainNo { get; set; }
    public string? TrainCode { get; set; }
    public string? CarCode { get; set; }
    public string? DepId { get; set; }
    public string? DebugType { get; set; }
    public string? HoldItemId { get; set; }
    public string? ItemName { get; set; }
    public string? OperateProcess { get; set; }
    public string? MutualProcess { get; set; }
    public string? QualityProcess { get; set; }

    public List<HoldItemDto>? HoldItems { get; set; }
}

/// <summary>与 GJCS TaskDisplayModel 一致：任务下载表格展示行。</summary>
public sealed class TaskDownloadDisplayRow
{
    public bool IsSelected { get; set; }

    public string HoldTaskId { get; set; } = "";
    public string? ProjectTypeName { get; set; }
    public string? ProjectNumber { get; set; }
    public string? TrainNo { get; set; }
    public string? TrainCode { get; set; }
    public string? CarCode { get; set; }
    public string? DepId { get; set; }
    public string? DebugType { get; set; }
    public string? HoldItemId { get; set; }
    public string? ItemName { get; set; }
    public string? OperateProcess { get; set; }
    public string? MutualProcess { get; set; }
    public string? QualityProcess { get; set; }

    public List<HoldItemDto>? OriginalHoldItems { get; set; }
}
