using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

/// <summary>型号管理（TestBed · Type 表）</summary>
public class VehicleTypeRepository
{
    private readonly KingbaseContext _ctx;

    public VehicleTypeRepository(KingbaseContext ctx) => _ctx = ctx;

    public Task<List<VehicleTypeEntity>> GetAllAsync() =>
        _ctx.Sql.Select<VehicleTypeEntity>()
            .Where(t => t.IsDeleted == null || t.IsDeleted == false)
            .OrderBy(t => t.Id)
            .ToListAsync();

    public async Task<VehicleTypeEntity?> GetByIdAsync(int id)
    {
        var list = await _ctx.Sql.Select<VehicleTypeEntity>()
            .Where(t => t.Id == id && (t.IsDeleted == null || t.IsDeleted == false))
            .ToListAsync();
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<int> InsertAsync(VehicleTypeEntity row)
    {
        row.CreateTime ??= DateTime.Now;
        var id = await _ctx.Sql.Insert(row).ExecuteIdentityAsync();
        row.Id = (int)id;
        return row.Id;
    }

    public Task<int> UpdateAsync(VehicleTypeEntity row)
    {
        row.UpdateTime = DateTime.Now;
        return _ctx.Sql.Update<VehicleTypeEntity>().SetSource(row).ExecuteAffrowsAsync();
    }

    /// <summary>物理删除，避免 SQLiteStudio 中仍看到已删记录（与旧版一致）。</summary>
    public Task<int> DeleteAsync(int id) =>
        _ctx.Sql.Delete<VehicleTypeEntity>()
            .Where(t => t.Id == id)
            .ExecuteAffrowsAsync();

    public async Task<int> ResolveTypeIdAsync(string? categoryName)
    {
        var name = (categoryName ?? "").Trim();
        if (string.IsNullOrEmpty(name))
            return 1;

        var list = await _ctx.Sql.Select<ProductCategoryEntity>()
            .Where(m => m.ModelType == name)
            .Take(1)
            .ToListAsync();
        return list.Count > 0 ? list[0].Id : 1;
    }

    public async Task<Dictionary<int, string>> GetCategoryNameByTypeIdAsync()
    {
        var models = await _ctx.Sql.Select<ProductCategoryEntity>().ToListAsync();
        return models.ToDictionary(m => m.Id, m => m.ModelType);
    }
}
