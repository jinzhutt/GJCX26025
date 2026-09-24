using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

/// <summary>
/// ��һ��ʽ��ѹ��һ�£���д TestBed.db �� MainTaskTable + HoldTaskTable��
/// </summary>
public class LegacyTaskRepository
{
    private readonly KingbaseContext _ctx;

    public LegacyTaskRepository(KingbaseContext ctx) => _ctx = ctx;

    public async Task<bool> ModifyOrAddMainTaskAsync(MainTaskTableEntity model)
    {
        var existing = await _ctx.Sql.Select<MainTaskTableEntity>()
            .Where(m => m.holdTaskId == model.holdTaskId && m.holdItemId == model.holdItemId)
            .FirstAsync();

        if (existing != null)
        {
            model.ID = existing.ID;
            return await _ctx.Sql.Update<MainTaskTableEntity>()
                .SetSource(model)
                .IgnoreColumns(m => m.ID)
                .ExecuteAffrowsAsync() > 0;
        }

        return await _ctx.Sql.Insert(model).ExecuteAffrowsAsync() > 0;
    }

    public async Task<bool> ModifyOrAddHoldTaskAsync(HoldTaskTableEntity model)
    {
        var existing = await _ctx.Sql.Select<HoldTaskTableEntity>()
            .Where(m => m.holdTaskId == model.holdTaskId
                        && m.holdItemId == model.holdItemId
                        && m.detailId == model.detailId)
            .FirstAsync();

        if (existing != null)
        {
            model.ID = existing.ID;
            model.CreateTime ??= existing.CreateTime;
            return await _ctx.Sql.Update<HoldTaskTableEntity>()
                .SetSource(model)
                .IgnoreColumns(m => m.ID)
                .ExecuteAffrowsAsync() > 0;
        }

        model.CreateTime ??= DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return await _ctx.Sql.Insert(model).ExecuteAffrowsAsync() > 0;
    }

    /// <summary>任务查看：未回传任务（isComplete = 0）。</summary>
    public Task<List<TaskDetailRowDto>> SearchForViewAsync(
        string? projectNumber, string? carCode, string? trainNo, string? restrictDetailId = null) =>
        SearchJoinedAsync(projectNumber, carCode, trainNo, restrictDetailId);

    /// <summary>
    /// 任务回传：与「任务查看」同一数据源（下载后即可搜到），勾选后一键回传。
    /// </summary>
    public Task<List<TaskDetailRowDto>> SearchForUploadAsync(
        string? projectNumber, string? carCode, string? trainNo) =>
        SearchJoinedAsync(projectNumber, carCode, trainNo, null);

    private async Task<List<TaskDetailRowDto>> SearchJoinedAsync(
        string? projectNumber,
        string? carCode,
        string? trainNo,
        string? restrictDetailId)
    {
        // 与任务查看一致：只查未回传（isComplete=0）。回传成功后会置 2，不再出现。
        // 同时匹配 holdTaskId + holdItemId，避免同一明细因主表重复被联出多行。
        var q = _ctx.Sql.Select<MainTaskTableEntity, HoldTaskTableEntity>()
            .InnerJoin((m, h) => m.holdTaskId == h.holdTaskId && m.holdItemId == h.holdItemId)
            .Where((m, h) => h.isComplete == 0);

        if (!string.IsNullOrWhiteSpace(projectNumber))
            q = q.Where((m, h) => m.projectNumber == projectNumber);
        if (!string.IsNullOrWhiteSpace(carCode))
            q = q.Where((m, h) => m.carCode == carCode);
        if (!string.IsNullOrWhiteSpace(trainNo))
            q = q.Where((m, h) => m.trainNo == trainNo);
        if (!string.IsNullOrWhiteSpace(restrictDetailId))
            q = q.Where((m, h) => h.detailId == restrictDetailId);

        var list = await q
            .OrderByDescending((m, h) => h.ID)
            .Take(100)
            .ToListAsync((m, h) => new TaskDetailRowDto
            {
                LocalDetailId = h.ID,
                LocalTaskRowId = m.ID,
                Main = m,
                Detail = h
            });

        // 按明细主键去重，防止界面勾一条却对应多行
        return list
            .GroupBy(x => x.LocalDetailId)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<(List<string> Projects, List<string> Cars, List<string> Trains)> GetFilterOptionsAsync()
    {
        var mains = await _ctx.Sql.Select<MainTaskTableEntity>().ToListAsync();
        static List<string> Distinct(IEnumerable<string?> src) =>
            src.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct().OrderBy(x => x).ToList();

        return (
            Distinct(mains.Select(m => m.projectNumber)),
            Distinct(mains.Select(m => m.carCode)),
            Distinct(mains.Select(m => m.trainNo))
        );
    }

    public Task<List<HoldTaskTableEntity>> GetHoldStepsAsync(string holdTaskId, string? holdItemId) =>
        _ctx.Sql.Select<HoldTaskTableEntity>()
            .Where(h => h.holdTaskId == holdTaskId && h.holdItemId == holdItemId)
            .OrderBy(h => h.stepNo)
            .ToListAsync();

    public async Task<HoldTaskTableEntity?> GetHoldByIdAsync(int id)
    {
        var list = await _ctx.Sql.Select<HoldTaskTableEntity>().Where(h => h.ID == id).Take(1).ToListAsync();
        return list.FirstOrDefault();
    }

    public async Task<MainTaskTableEntity?> GetMainByKeysAsync(string holdTaskId, string? holdItemId)
    {
        var list = await _ctx.Sql.Select<MainTaskTableEntity>()
            .Where(m => m.holdTaskId == holdTaskId && m.holdItemId == holdItemId)
            .Take(1)
            .ToListAsync();
        return list.FirstOrDefault();
    }

    public async Task<MainTaskTableEntity?> GetMainByIdAsync(int id)
    {
        var list = await _ctx.Sql.Select<MainTaskTableEntity>().Where(m => m.ID == id).Take(1).ToListAsync();
        return list.FirstOrDefault();
    }

    public Task<List<HoldTaskTableEntity>> GetCompletedHoldRowsAsync(string holdTaskId, string? holdItemId) =>
        _ctx.Sql.Select<HoldTaskTableEntity>()
            .Where(h => h.holdTaskId == holdTaskId && h.holdItemId == holdItemId && h.isComplete == 1)
            .ToListAsync();

    /// <summary>任务重做：恢复为 isComplete=0</summary>
    public Task ResetForRedoAsync(int holdRowId) =>
        _ctx.Sql.Update<HoldTaskTableEntity>()
            .Where(h => h.ID == holdRowId)
            .Set(h => h.isComplete, 0)
            .ExecuteAffrowsAsync();

    /// <summary>作业完成待回传：isComplete = 1</summary>
    public Task MarkReadyForUploadAsync(int holdRowId) =>
        _ctx.Sql.Update<HoldTaskTableEntity>()
            .Where(h => h.ID == holdRowId)
            .Set(h => h.isComplete, 1)
            .ExecuteAffrowsAsync();

    public Task MarkUploadedAsync(int holdRowId) =>
        _ctx.Sql.Update<HoldTaskTableEntity>()
            .Where(h => h.ID == holdRowId)
            .Set(h => h.isComplete, 2)
            .ExecuteAffrowsAsync();
}
