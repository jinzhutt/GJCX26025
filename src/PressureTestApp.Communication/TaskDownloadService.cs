using PressureTestApp.Communication.Api;
using PressureTestApp.Communication.Models;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Data.Entities;
using PressureTestApp.Data.Repositories;

namespace PressureTestApp.Communication;

/// <summary>
/// 任务下载：按 MES 协议 4.1 获取作业工件信息，并写入 TestBed.db（MainTaskTable / HoldTaskTable）。
/// </summary>
public sealed class TaskDownloadService
{
    private readonly MesApiClient _mes;
    private readonly LegacyTaskRepository _taskRepo;
    private readonly DatabaseConfig _dbConfig;

    public TaskDownloadService(MesApiClient mes, LegacyTaskRepository taskRepo, DatabaseConfig dbConfig)
    {
        _mes = mes;
        _taskRepo = taskRepo;
        _dbConfig = dbConfig;
    }

    /// <summary>从 MES 拉取可下载任务列表（不落库，仅展示）。</summary>
    public async Task<MainTaskListResult?> GetMainTaskListAsync(string personCode)
    {
        try
        {
            _ = personCode; // MES 4.1 按工位/设备取数，不需要人员工号
            var resp = await _mes.GetWorkpieceInfoAsync();
            if (!resp.IsOk)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(resp.StatusText)
                    ? $"MES 返回失败 code={resp.Code}"
                    : resp.StatusText);

            var list = (resp.Result ?? new List<MesWorkpieceInfoDto>())
                .Where(x => !string.IsNullOrWhiteSpace(x.TaskId))
                .Select(MapToHoldTask)
                .ToList();

            NlogHelper.Info($"MES 主任务列表：{resp.StatusText}，code={resp.Code}，条数={list.Count}");

            return new MainTaskListResult
            {
                Msg = resp.StatusText,
                State = resp.IsOk ? "1" : "0",
                Total = list.Count.ToString(),
                List = list
            };
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "MES 主任务列表获取错误");
            throw;
        }
    }

    /// <summary>选择性下载：将勾选的 MES 任务写入本地库。</summary>
    public async Task<bool> GetMainTaskSelectiveAsync(string personCode, IReadOnlyList<MainTaskDownloadModel> selectedTasks)
    {
        if (selectedTasks == null || selectedTasks.Count == 0)
            throw new InvalidOperationException("请选择要下载的任务！");

        try
        {
            _ = personCode;
            var dbFile = _dbConfig.ResolveSqliteFilePath();
            NlogHelper.Info($"MES 任务下载落库 → {dbFile}（表 MainTaskTable、HoldTaskTable）");

            var createTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            foreach (var taskModel in selectedTasks)
            {
                NlogHelper.Info($"MES 任务落库：{createTime}，taskId={taskModel.HoldTaskId}，partCode={taskModel.CarCode}");

                await SaveMainTaskRowAsync(taskModel);
                await _taskRepo.ModifyOrAddHoldTaskAsync(new HoldTaskTableEntity
                {
                    holdTaskId = taskModel.HoldTaskId,
                    holdItemId = taskModel.HoldItemId ?? "",
                    detailId = string.IsNullOrWhiteSpace(taskModel.CarCode)
                        ? taskModel.HoldTaskId
                        : taskModel.CarCode!,
                    stepName = taskModel.ItemName,
                    stepBom = taskModel.ItemName,
                    stepContent = $"工件类型：{taskModel.ItemName}",
                    stepNo = "1",
                    vhecileNo = taskModel.TrainNo,
                    isComplete = 0,
                    CreateTime = createTime
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "MES 选择性任务下载错误");
            throw;
        }
    }

    private static HoldTaskDto MapToHoldTask(MesWorkpieceInfoDto w)
    {
        var partCode = w.ResolvedPartCode;
        var partType = w.PartType ?? "";
        return new HoldTaskDto
        {
            HoldTaskId = w.TaskId,
            ProjectNumber = w.TrainType,
            TrainNo = w.TrainNo,
            CarCode = partCode,
            DebugType = partType,
            HoldItems =
            [
                new HoldItemDto
                {
                    HoldItemId = string.IsNullOrWhiteSpace(partCode) ? (w.TaskId ?? "") : partCode,
                    ItemName = partType
                }
            ]
        };
    }

    private Task<bool> SaveMainTaskRowAsync(MainTaskDownloadModel taskModel) =>
        _taskRepo.ModifyOrAddMainTaskAsync(new MainTaskTableEntity
        {
            holdTaskId = taskModel.HoldTaskId,
            holdItemId = taskModel.HoldItemId,
            projectTypeName = taskModel.ProjectTypeName,
            projectNumber = taskModel.ProjectNumber,
            trainCode = taskModel.TrainCode,
            trainNo = taskModel.TrainNo,
            carCode = taskModel.CarCode,
            depId = taskModel.DepId,
            debugType = taskModel.DebugType,
            itemName = taskModel.ItemName,
            operateProcess = taskModel.OperateProcess,
            mutualProcess = taskModel.MutualProcess,
            qualityProcess = taskModel.QualityProcess
        });

    /// <summary>将 MES/列表结果展开为下载勾选表格行。</summary>
    public static List<TaskDownloadDisplayRow> ExpandDisplayRows(MainTaskListResult? taskData)
    {
        var rows = new List<TaskDownloadDisplayRow>();
        if (taskData?.List == null || taskData.List.Count == 0)
            return rows;

        foreach (var task in taskData.List)
        {
            if (task.HoldItems == null || task.HoldItems.Count == 0)
            {
                rows.Add(new TaskDownloadDisplayRow
                {
                    IsSelected = false,
                    HoldTaskId = task.HoldTaskId ?? "",
                    ProjectNumber = task.ProjectNumber,
                    TrainNo = task.TrainNo,
                    CarCode = task.CarCode,
                    DebugType = task.DebugType,
                    HoldItemId = task.CarCode ?? task.HoldTaskId ?? "",
                    ItemName = task.DebugType
                });
                continue;
            }

            foreach (var item in task.HoldItems)
            {
                rows.Add(new TaskDownloadDisplayRow
                {
                    IsSelected = false,
                    HoldTaskId = task.HoldTaskId ?? "",
                    ProjectTypeName = task.ProjectTypeName,
                    ProjectNumber = task.ProjectNumber,
                    TrainNo = task.TrainNo,
                    TrainCode = task.TrainCode,
                    CarCode = task.CarCode,
                    DepId = task.DepId,
                    DebugType = task.DebugType,
                    HoldItemId = item.HoldItemId,
                    ItemName = item.ItemName ?? task.DebugType,
                    OperateProcess = item.OperateProcess,
                    MutualProcess = item.MutualProcess,
                    QualityProcess = item.QualityProcess,
                    OriginalHoldItems = [item]
                });
            }
        }

        return rows;
    }
}
