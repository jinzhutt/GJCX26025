using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

/// <summary>MES 4.3 待传队列仓储（断网缓存 / 恢复补传）。</summary>
public sealed class MesPendingUploadRepository
{
    private readonly KingbaseContext _ctx;

    public MesPendingUploadRepository(KingbaseContext ctx) => _ctx = ctx;

    /// <summary>
    /// 写入或更新待传：同一 LocalDetailId（&gt;0）只保留一条最新 payload。
    /// </summary>
    public async Task<long> UpsertPendingAsync(MesWorkDataPendingEntity entity)
    {
        MesWorkDataPendingEntity? existing = null;
        if (entity.LocalDetailId > 0)
        {
            existing = await _ctx.Sql.Select<MesWorkDataPendingEntity>()
                .Where(x => x.Status == 0 && x.LocalDetailId == entity.LocalDetailId)
                .FirstAsync();
        }

        if (existing == null)
            return await _ctx.Sql.Insert(entity).ExecuteIdentityAsync();

        existing.TaskId = entity.TaskId;
        existing.PartCode = entity.PartCode;
        existing.PayloadJson = entity.PayloadJson;
        existing.CreatedAt = entity.CreatedAt;
        existing.LastError = null;
        await _ctx.Sql.Update<MesWorkDataPendingEntity>().SetSource(existing).ExecuteAffrowsAsync();
        return existing.Id;
    }

    public Task<List<MesWorkDataPendingEntity>> ListPendingOldestFirstAsync(int take = 50) =>
        _ctx.Sql.Select<MesWorkDataPendingEntity>()
            .Where(x => x.Status == 0)
            .OrderBy(x => x.CreatedAt)
            .OrderBy(x => x.Id)
            .Take(take)
            .ToListAsync();

    public async Task<int> CountPendingAsync()
    {
        var n = await _ctx.Sql.Select<MesWorkDataPendingEntity>().Where(x => x.Status == 0).CountAsync();
        return (int)n;
    }

    public async Task MarkUploadedAsync(long id)
    {
        await _ctx.Sql.Update<MesWorkDataPendingEntity>()
            .Set(x => x.Status, 1)
            .Set(x => x.LastAttemptAt, DateTime.Now)
            .Set(x => x.LastError, (string?)null)
            .Where(x => x.Id == id)
            .ExecuteAffrowsAsync();
    }

    public async Task MarkUploadedByLocalDetailIdAsync(int localDetailId)
    {
        if (localDetailId <= 0) return;
        await _ctx.Sql.Update<MesWorkDataPendingEntity>()
            .Set(x => x.Status, 1)
            .Set(x => x.LastAttemptAt, DateTime.Now)
            .Set(x => x.LastError, (string?)null)
            .Where(x => x.Status == 0 && x.LocalDetailId == localDetailId)
            .ExecuteAffrowsAsync();
    }

    public async Task MarkAttemptFailedAsync(long id, string error)
    {
        var row = await _ctx.Sql.Select<MesWorkDataPendingEntity>().Where(x => x.Id == id).FirstAsync();
        if (row == null) return;

        row.AttemptCount += 1;
        row.LastAttemptAt = DateTime.Now;
        row.LastError = error.Length > 500 ? error[..500] : error;
        await _ctx.Sql.Update<MesWorkDataPendingEntity>().SetSource(row).ExecuteAffrowsAsync();
    }
}
