using FreeSql;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;
using PressureTestApp.Data.Entities;

namespace PressureTestApp.Data;

/// <summary>
/// 数据库上下文（FreeSql + SQLite）。
/// </summary>
public sealed class KingbaseContext : IDisposable
{
    private static IFreeSql? _instance;
    private static readonly object _lock = new();

    public IFreeSql Sql => _instance ?? throw new InvalidOperationException("数据库未初始化，请先调用 Initialize");

    /// <summary>
    /// 初始化数据库（应用启动时调用一次）
    /// </summary>
    public static void Initialize(DatabaseConfig config)
    {
        lock (_lock)
        {
            if (_instance != null) return;

            try
            {
                // Microsoft.Data.Sqlite / 跨平台原生库（Linux 上必须，否则找不到 SQLite.Interop.dll）
                SQLitePCL.Batteries_V2.Init();

                var dbFile = config.ResolveSqliteFilePath();
                var connStr = config.BuildConnectionString();
                NlogHelper.Info($"初始化 SQLite 数据库 → {dbFile}");

                // 旧库（Type / AuxiliaryTable 等）禁止在 Insert 时自动 SyncStructure，否则会生成 _FreeSqlTmp_Type 导致写入失败
                // UseNoneCommandParameter：Microsoft.Data.Sqlite 参数插值有已知问题
                var builder = new FreeSql.FreeSqlBuilder()
                    .UseConnectionString(DataType.Sqlite, connStr)
                    .UseAutoSyncStructure(false)
                    .UseNoneCommandParameter(true)
                    .UseMonitorCommand(cmd =>
                    {
                        // 调试时打印 SQL，可去注释
                        // NlogHelper.Trace($"SQL: {cmd.CommandText}");
                    });

                _instance = builder.Build();
                CleanupFreeSqlTempTables();

                // 自动建表（强制同步，确保 Record 表被创建）
                NlogHelper.Info("开始同步数据库表结构...");
                try
                {
                    // Users 为旧库已有表，不参与自动建表/改表
                    // AuxiliaryTable / Users 等为旧库已有表，不参与自动建表/改表
                    _instance.CodeFirst.SyncStructure(
                        typeof(TrainEntity),
                        typeof(HoldTaskEntity),
                        typeof(HoldTaskDetailEntity),
                        typeof(OfflineRecordEntity),
                        typeof(DeviceCounterEntity),
                        typeof(RecordEntity),
                        typeof(MesWorkDataPendingEntity)
                    );
                    NlogHelper.Info("数据库表结构已同步，包含 Record 表");
                }
                catch (Exception ex)
                {
                    NlogHelper.Error(ex, "同步数据库表结构失败");
                }

                // 首次启动初始化默认数据
                EnsureDefaultData();
            }
            catch (Exception ex)
            {
                NlogHelper.Error(ex, "数据库初始化失败");
                throw;
            }
        }
    }

    /// <summary>清理 FreeSql 误同步旧库时留下的临时表（会导致 Type 表插入失败）。</summary>
    private static void CleanupFreeSqlTempTables()
    {
        if (_instance == null) return;
        try
        {
            var tables = _instance.Ado.Query<string>("""
                SELECT name FROM sqlite_master
                WHERE type = 'table' AND name LIKE '_FreeSqlTmp_%'
                """);
            foreach (var table in tables)
            {
                _instance.Ado.ExecuteNonQuery($"DROP TABLE IF EXISTS [{table}]");
                NlogHelper.Info($"已清理 FreeSql 临时表：{table}");
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"清理 FreeSql 临时表时跳过：{ex.Message}");
        }
    }

    private static void EnsureDefaultData()
    {
        if (_instance == null) return;

        // 默认管理员账号 admin / admin
        var admin = _instance.Select<UserEntity>().Where(u => u.JobNumber == "admin").First();// 先查找是否存在 admin 账号
        if (admin == null)
        {
            _instance.Insert(new UserEntity
            {
                JobNumber = "admin",
                Name = "管理员",
                PasswordHash = PasswordHelper.SHA512Encode("admin", "admin"),
                Permission = UserPermissionMapper.RoleToPermission(UserRole.Admin),
                Enable = 1,
                Sort = 0,
                CreateDateTime = DateTime.Now
            }).ExecuteAffrows();
            NlogHelper.Info("已创建默认管理员账号 admin / admin（Users 表）");
        }
        else if (IsLegacyShortPasswordHash(admin.PasswordHash))
        {
            // 旧库 Users 多为 16 字节 Base64，与 SHA512×1024 不兼容，重置为 admin/admin
            admin.PasswordHash = PasswordHelper.SHA512Encode("admin", "admin");
            admin.Permission = UserPermissionMapper.RoleToPermission(UserRole.Admin);
            admin.Enable = 1;
            admin.UpdateDateTime = DateTime.Now;
            _instance.Update<UserEntity>().SetSource(admin).ExecuteAffrows();
            NlogHelper.Info("已重置 admin 密码为 admin（旧库短哈希 → SHA512×1024）");
        }

        // 默认设备计数器
        var counterKeys = new[]
        {
            ("TotalTest", "总试验次数"),
            ("PassedTest", "通过次数"),
            ("BreakdownTest", "击穿次数"),
            ("Loop1Count", "回路1次数"),
            ("Loop2Count", "回路2次数"),
            ("Loop3Count", "回路3次数"),
            ("Loop4Count", "回路4次数"),
            ("EmergencyStop", "急停次数"),
            ("RunningHours", "运行小时")
        };
        foreach (var (key, name) in counterKeys)// 确保每个计数器都存在，不存在则创建
        {
            var exists = _instance.Select<DeviceCounterEntity>().Where(c => c.CounterKey == key).Any();// 先查找是否存在
            if (!exists)// 不存在则创建
            {
                _instance.Insert(new DeviceCounterEntity
                {
                    CounterKey = key,
                    CounterName = name,
                    Value = 0
                }).ExecuteAffrows();
            }
        }
    }

    /// <summary>旧 Users 表密码多为 16 字节 Base64（24 字符），非本项目 SHA512×1024 格式</summary>
    private static bool IsLegacyShortPasswordHash(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || stored.Length > 32) return false;
        try { return Convert.FromBase64String(stored).Length == 16; }
        catch { return false; }
    }

    public void Dispose()
    {
        _instance?.Dispose();
        _instance = null;
    }
}
