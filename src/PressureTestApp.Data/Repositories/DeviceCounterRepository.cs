using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

public class DeviceCounterRepository// 设备计数器仓储（用于记录设备的使用次数、测试次数等，支持增量更新和重置）
{
    private readonly KingbaseContext _ctx;

    public DeviceCounterRepository(KingbaseContext ctx) => _ctx = ctx;

    public Task<List<DeviceCounterEntity>> GetAllAsync() =>
        _ctx.Sql.Select<DeviceCounterEntity>().OrderBy(c => c.Id).ToListAsync();

    public Task<long> GetValueAsync(string key) =>
        _ctx.Sql.Select<DeviceCounterEntity>()
            .Where(c => c.CounterKey == key)
            .FirstAsync(c => c.Value);

    public async Task<int> IncrementAsync(string key, int delta = 1)
    {
        var existing = await _ctx.Sql.Select<DeviceCounterEntity>()
            .Where(c => c.CounterKey == key)
            .FirstAsync();

        if (existing == null)
        {
            await _ctx.Sql.Insert(new DeviceCounterEntity
            {
                CounterKey = key,
                Value = delta,
                UpdatedAt = DateTime.Now
            }).ExecuteAffrowsAsync();
            return 1;
        }

        return await _ctx.Sql.Update<DeviceCounterEntity>()
            .Where(c => c.CounterKey == key)
            .Set(c => c.Value + delta)
            .Set(c => c.UpdatedAt, DateTime.Now)
            .ExecuteAffrowsAsync();
    }

    public Task<int> ResetAsync(string key) =>
        _ctx.Sql.Update<DeviceCounterEntity>()
            .Where(c => c.CounterKey == key)
            .Set(c => c.Value, 0L)
            .Set(c => c.LastResetAt, DateTime.Now)
            .ExecuteAffrowsAsync();
}
