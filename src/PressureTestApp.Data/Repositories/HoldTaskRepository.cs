using PressureTestApp.Core.Models;
using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

/// <summary>
/// 耐压任务仓储（在线 + Excel 导入共用）
/// </summary>
public class HoldTaskRepository
{
    private readonly KingbaseContext _ctx;

    public HoldTaskRepository(KingbaseContext ctx) => _ctx = ctx;

    /// <summary>批量保存任务及其明细（事务）</summary>
    public async Task<int> SaveTaskWithDetailsAsync(HoldTaskEntity task, IEnumerable<HoldTaskDetailEntity> details)
    {
        var existing = await _ctx.Sql.Select<HoldTaskEntity>()
            .Where(t => t.HoldTaskId == task.HoldTaskId && t.HoldItemId == task.HoldItemId)
            .FirstAsync();

        int taskRowId;
        if (existing != null)
        {
            taskRowId = existing.Id;
            task.Id = existing.Id;
            task.DownloadedAt = existing.DownloadedAt;
            await _ctx.Sql.Update<HoldTaskEntity>()
                .SetSource(task)
                .IgnoreColumns(t => t.Id)
                .ExecuteAffrowsAsync();
        }
        else
        {
            task.DownloadedAt = DateTime.Now;
            taskRowId = (int)await _ctx.Sql.Insert(task).ExecuteIdentityAsync();
        }

        foreach (var d in details)
        {
            d.HoldTaskRowId = taskRowId;
            var exists = await _ctx.Sql.Select<HoldTaskDetailEntity>()
                .Where(x => x.HoldTaskRowId == taskRowId && x.DetailId == d.DetailId)
                .AnyAsync();
            if (!exists)
                await _ctx.Sql.Insert(d).ExecuteAffrowsAsync();
        }

        return taskRowId;
    }

    public Task<List<HoldTaskEntity>> GetPendingTasksAsync(TaskSource source) =>
        _ctx.Sql.Select<HoldTaskEntity>()
            .Where(t => t.Source == source && !t.IsCompleted)
            .OrderBy(t => t.DownloadedAt)
            .ToListAsync();

    public Task<List<HoldTaskDetailEntity>> GetDetailsAsync(int taskRowId) =>
        _ctx.Sql.Select<HoldTaskDetailEntity>()
            .Where(d => d.HoldTaskRowId == taskRowId)
            .OrderBy(d => d.StepNo)
            .ToListAsync();

    public async Task<HoldTaskDetailEntity?> GetDetailByIdAsync(int detailId)
    {
        var list = await _ctx.Sql.Select<HoldTaskDetailEntity>()
            .Where(d => d.Id == detailId)
            .Take(1)
            .ToListAsync();
        return list.FirstOrDefault();
    }

    public Task<int> UpdateDetailResultAsync(HoldTaskDetailEntity detail) =>
        _ctx.Sql.Update<HoldTaskDetailEntity>()
            .SetSource(detail)
            .ExecuteAffrowsAsync();

    public Task<int> MarkTaskCompletedAsync(int taskRowId) =>
        _ctx.Sql.Update<HoldTaskEntity>()
            .Where(t => t.Id == taskRowId)
            .Set(t => t.IsCompleted, true)
            .Set(t => t.CompletedAt, DateTime.Now)
            .ExecuteAffrowsAsync();

    public Task<int> MarkTaskUploadedAsync(int taskRowId) =>
        _ctx.Sql.Update<HoldTaskEntity>()
            .Where(t => t.Id == taskRowId)
            .Set(t => t.IsUploaded, true)
            .Set(t => t.UploadedAt, DateTime.Now)
            .ExecuteAffrowsAsync();

    public Task<List<HoldTaskEntity>> GetUploadablesAsync() =>
        _ctx.Sql.Select<HoldTaskEntity>()
            .Where(t => t.Source == TaskSource.Online && t.IsCompleted && !t.IsUploaded)
            .ToListAsync();

    public async Task<HoldTaskEntity?> GetTaskByRowIdAsync(int rowId)
    {
        var list = await _ctx.Sql.Select<HoldTaskEntity>()
            .Where(t => t.Id == rowId)
            .Take(1)
            .ToListAsync();
        return list.FirstOrDefault();
    }

    public Task MarkDetailUploadedAsync(int localDetailId) =>
        _ctx.Sql.Update<HoldTaskDetailEntity>()
            .Where(d => d.Id == localDetailId)
            .Set(d => d.IsUploaded, true)
            .ExecuteAffrowsAsync();

    public async Task TryMarkTaskUploadedIfAllDetailsDoneAsync(int localTaskRowId)
    {
        var details = await GetDetailsAsync(localTaskRowId);
        if (details.Count == 0) return;
        if (details.Any(d => d.IsTested && !d.IsUploaded)) return;
        if (details.All(d => !d.IsTested)) return;
        await MarkTaskUploadedAsync(localTaskRowId);
    }
}

/// <summary>离线模式独立记录仓储</summary>
public class OfflineRecordRepository
{
    private readonly KingbaseContext _ctx;
    public OfflineRecordRepository(KingbaseContext ctx) => _ctx = ctx;

    public Task<int> SaveAsync(OfflineRecordEntity rec) =>
        _ctx.Sql.Insert(rec).ExecuteAffrowsAsync();

    public Task<List<OfflineRecordEntity>> QueryAsync(DateTime? from, DateTime? to, int pageIndex = 1, int pageSize = 50)
    {
        var q = _ctx.Sql.Select<OfflineRecordEntity>();
        if (from.HasValue) q = q.Where(r => r.StartedAt >= from.Value);
        if (to.HasValue) q = q.Where(r => r.StartedAt <= to.Value);
        return q.OrderByDescending(r => r.StartedAt).Page(pageIndex, pageSize).ToListAsync();
    }
}

/// <summary>车型/车列同步仓储</summary>
public class ProjectTrainRepository
{
    private readonly KingbaseContext _ctx;
    public ProjectTrainRepository(KingbaseContext ctx) => _ctx = ctx;

    public async Task SyncProjectsAsync(IEnumerable<ProjectEntity> projects)
    {
        foreach (var p in projects)
        {
            var existing = await _ctx.Sql.Select<ProjectEntity>().Where(x => x.ProjectId == p.ProjectId).FirstAsync();
            p.SyncedAt = DateTime.Now;
            if (existing == null)
                await _ctx.Sql.Insert(p).ExecuteAffrowsAsync();
            else
                await _ctx.Sql.Update<ProjectEntity>().SetSource(p).ExecuteAffrowsAsync();
        }
    }

    public async Task SyncTrainsAsync(IEnumerable<TrainEntity> trains)
    {
        foreach (var t in trains)
        {
            var existing = await _ctx.Sql.Select<TrainEntity>().Where(x => x.Id == t.Id).FirstAsync();
            t.SyncedAt = DateTime.Now;
            if (existing == null)
                await _ctx.Sql.Insert(t).ExecuteAffrowsAsync();
            else
                await _ctx.Sql.Update<TrainEntity>().SetSource(t).ExecuteAffrowsAsync();
        }
    }

    public Task<List<ProjectEntity>> GetAllProjectsAsync() =>
        _ctx.Sql.Select<ProjectEntity>().OrderBy(p => p.ProjectNumber).ToListAsync();

    public Task<List<TrainEntity>> GetTrainsByProjectAsync(int projectId) =>
        _ctx.Sql.Select<TrainEntity>().Where(t => t.ProjectId == projectId).ToListAsync();
}
