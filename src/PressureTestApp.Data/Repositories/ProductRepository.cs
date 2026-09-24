using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

public class ProductRepository// 产品仓储（用于管理产品信息，支持增删改查）
{
    private readonly KingbaseContext _ctx;

    public ProductRepository(KingbaseContext ctx) => _ctx = ctx;

    public Task<List<ProductEntity>> GetAllAsync() =>
        _ctx.Sql.Select<ProductEntity>()
            .Where(p => p.Enabled)
            .OrderBy(p => p.Code)
            .ToListAsync();

    public async Task<ProductEntity?> FindByCodeAsync(string code)
    {
        var list = await _ctx.Sql.Select<ProductEntity>()
            .Where(p => p.Code == code && p.Enabled)
            .Take(1)
            .ToListAsync();
        return list.FirstOrDefault();
    }

    public async Task<ProductEntity?> GetByIdAsync(int id)
    {
        var list = await _ctx.Sql.Select<ProductEntity>()
            .Where(p => p.Id == id && p.Enabled)
            .ToListAsync();
        return list.Count > 0 ? list[0] : null;
    }

    public Task<int> InsertAsync(ProductEntity p)
    {
        p.CreatedAt = DateTime.Now;
        return _ctx.Sql.Insert(p).ExecuteAffrowsAsync();
    }

    public Task<int> UpdateAsync(ProductEntity p) =>
        _ctx.Sql.Update<ProductEntity>().SetSource(p).ExecuteAffrowsAsync();

    public Task<int> DeleteAsync(int id) =>
        _ctx.Sql.Update<ProductEntity>()
            .Where(p => p.Id == id)
            .Set(p => p.Enabled, false)
            .ExecuteAffrowsAsync();
}
