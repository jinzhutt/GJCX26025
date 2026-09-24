using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

public class InspectionStaffRepository// 耐压巡检人员仓储（用于管理耐压巡检人员信息，支持增删改查）
{
    private readonly KingbaseContext _ctx;

    public InspectionStaffRepository(KingbaseContext ctx) => _ctx = ctx;

    public Task<List<InspectionStaffEntity>> GetAllAsync() =>
        _ctx.Sql.Select<InspectionStaffEntity>()
            .OrderBy(s => s.Id)
            .ToListAsync();

    public async Task<InspectionStaffEntity?> GetByIdAsync(int id)
    {
        var list = await _ctx.Sql.Select<InspectionStaffEntity>()
            .Where(s => s.Id == id)
            .ToListAsync();
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<int> InsertAsync(InspectionStaffEntity row)
    {
        var id = await _ctx.Sql.Insert(row).ExecuteIdentityAsync();
        row.Id = (int)id;
        return row.Id;
    }

    public Task<int> UpdateAsync(InspectionStaffEntity row) =>
        _ctx.Sql.Update<InspectionStaffEntity>().SetSource(row).ExecuteAffrowsAsync();

    public Task<int> DeleteAsync(int id) =>
        _ctx.Sql.Delete<InspectionStaffEntity>()
            .Where(s => s.Id == id)
            .ExecuteAffrowsAsync();
}
