using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;
using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data.Repositories;

public class UserRepository// 用户仓储（用于管理用户信息，支持增删改查）
{
    private readonly KingbaseContext _ctx;
    public UserRepository(KingbaseContext ctx) => _ctx = ctx;

    public async Task<UserEntity?> FindByJobNumberAsync(string jobNumber)
    {
        var list = await _ctx.Sql.Select<UserEntity>()
            .Where(u => u.JobNumber == jobNumber && u.Enable == 1)
            .Take(1)
            .ToListAsync();
        return list.FirstOrDefault();
    }

    public Task<List<UserEntity>> GetAllAsync(bool includeDisabled = false)
    {
        var q = _ctx.Sql.Select<UserEntity>().OrderBy(u => u.JobNumber);
        if (!includeDisabled) q = q.Where(u => u.Enable == 1);
        return q.ToListAsync();
    }

    public async Task<int> InsertAsync(UserEntity user)
    {
        user.Sort = 0;
        user.Enable = 1;
        user.CreateDateTime = DateTime.Now;
        if (string.IsNullOrWhiteSpace(user.Permission))
            user.Permission = UserPermissionMapper.RoleToPermission(UserRole.Operator);
        var id = await _ctx.Sql.Insert(user).ExecuteIdentityAsync();
        return (int)id;
    }

    public Task<int> UpdateAsync(UserEntity user)
    {
        user.UpdateDateTime = DateTime.Now;
        return _ctx.Sql.Update<UserEntity>().SetSource(user).ExecuteAffrowsAsync();
    }

    public Task<int> DisableAsync(int userId) =>
        _ctx.Sql.Update<UserEntity>()
            .Where(u => u.Id == userId)
            .Set(u => u.Enable, 0)
            .Set(u => u.UpdateDateTime, DateTime.Now)
            .ExecuteAffrowsAsync();

    public Task UpdateLastLoginAsync(int userId) =>
        _ctx.Sql.Update<UserEntity>()
            .Where(u => u.Id == userId)
            .Set(u => u.LastLoginAt, DateTime.Now)
            .ExecuteAffrowsAsync();

    /// <summary>API 同步：写入旧表 Users，密码列为 EPE 下发的 MD5 十六进制</summary>
    public async Task SyncFromApiAsync(IEnumerable<UserEntity> users)
    {
        foreach (var u in users)
        {
            var existing = await _ctx.Sql.Select<UserEntity>()
                .Where(x => x.JobNumber == u.JobNumber)
                .FirstAsync();

            if (existing == null)
            {
                u.CreateDateTime = DateTime.Now;
                u.Enable = 1;
                u.Sort = 0;
                u.Permission = UserPermissionMapper.RoleToPermission(UserRole.Operator);
                await _ctx.Sql.Insert(u).ExecuteAffrowsAsync();
            }
            else
            {
                existing.Name = u.Name;
                existing.PasswordHash = u.PasswordHash;
                existing.DepId = u.DepId;
                existing.DepName = u.DepName;
                existing.UpdateDateTime = DateTime.Now;
                await _ctx.Sql.Update<UserEntity>().SetSource(existing).ExecuteAffrowsAsync();
            }
        }
    }
}
