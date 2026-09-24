using PressureTestApp.Core.Helpers;

namespace PressureTestApp.Core.Configs;

/// <summary>
/// 数据库连接配置（SQLite 单文件）
/// </summary>
public class DatabaseConfig : IniConfig
{
    /// <summary>程序运行目录下的默认数据库路径</summary>
    public static string DefaultTestBedDbPath => 
        Path.Combine(AppContext.BaseDirectory, "data", "TestBed.db");

    /// <summary>SQLite 数据库文件完整路径（留空使用默认路径）</summary>
    [IniKeyNames("Database", "SqliteFile", "")]
    public string SqliteFile { get; set; } = "";

    [IniKeyNames("Database", "AutoSyncStructure", "false")]
    public bool AutoSyncStructure { get; set; } = false;

    public DatabaseConfig() : base(Path.Combine(AppConstants.ConfigDir, "database.ini"))
    {
        SetSectionName("Database");
        Load();
    }

    /// <summary>首次运行：确保程序根 config\database.ini 可用。</summary>
    public static void EnsureUserIniFromBundled() =>
        IniBundledCopy.CopyBundledIfMissing("database.ini");

    /// <summary>解析路径并确保数据库文件所在目录存在</summary>
    public string ResolveSqliteFilePath()
    {
        var path = string.IsNullOrWhiteSpace(SqliteFile)
            ? DefaultTestBedDbPath
            : SqliteFile.Trim();

        // database.ini 里常见 data\TestBed.db；Linux 上 \ 不是目录分隔符，需规范化
        path = path.Replace('\\', Path.DirectorySeparatorChar)
                   .Replace('/', Path.DirectorySeparatorChar);

        if (!Path.IsPathRooted(path))
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));

        if (!File.Exists(path))
        {
            var alt = Path.ChangeExtension(path, path.EndsWith(".db3", StringComparison.OrdinalIgnoreCase) ? ".db" : ".db3");
            if (File.Exists(alt))
                path = alt;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        return path;
    }

    public string BuildConnectionString()
    {
        var path = ResolveSqliteFilePath();
        return $"Data Source={path};Pooling=true;";
    }
}

/// <summary>
/// PLC 通信配置（西门子 S7）
/// </summary>
public class PlcConfig : IniConfig
{
    [IniKeyNames("PLC", "Enabled", "false")]
    public bool Enabled { get; set; } = false;

    [IniKeyNames("PLC", "CpuType", "S71200")]
    public string CpuType { get; set; } = "S71200";

    [IniKeyNames("PLC", "IP", "192.168.0.1")]
    public string IP { get; set; } = "192.168.0.1";

    [IniKeyNames("PLC", "Rack", "0")]
    public int Rack { get; set; } = 0;

    [IniKeyNames("PLC", "Slot", "1")]
    public int Slot { get; set; } = 1;

    [IniKeyNames("PLC", "PollInterval", "100")]
    public int PollInterval { get; set; } = 100;

    [IniKeyNames("PLC", "ConnectTimeout", "5000")]
    public int ConnectTimeout { get; set; } = 5000;

    public PlcConfig() : base(Path.Combine(AppConstants.ConfigDir, "plc.ini"))
    {
        SetSectionName("PLC");
        Load();
    }
}

/// <summary>
/// 设备认证配置（与旧版 AuthenticationConfig.ini 一致）
/// </summary>
public class AuthenticationConfig : IniConfig
{
    private const string FileName = "AuthenticationConfig.ini";

    /// <summary>认证编码（EPE 下发的 deviceNumber）</summary>
    [IniFlatKey("认证编码")]
    public string Authentication { get; set; } = "";

    /// <summary>是否开启认证功能（是/否，控制登录页是否显示设备认证入口）</summary>
    [IniFlatKey("是否开启认证功能", "是")]
    public string IsAuthentication { get; set; } = "是";

    public bool IsAuthenticationEnabled =>
        string.Equals(IsAuthentication?.Trim(), "是", StringComparison.Ordinal);

    public AuthenticationConfig() : base(Path.Combine(AppConstants.ConfigDir, FileName)) { }

    public static void EnsureUserIniFromBundled()
    {
        IniBundledCopy.CopyBundledIfMissing(FileName);
        TryMigrateLegacyApiIni();

        // 现场仅 MES：关闭 EPE 设备认证入口
        var authPath = Path.Combine(AppConstants.ConfigDir, FileName);
        if (!File.Exists(authPath))
            return;

        var text = File.ReadAllText(authPath);
        if (text.Contains("是否开启认证功能=是", StringComparison.Ordinal))
        {
            text = text.Replace("是否开启认证功能=是", "是否开启认证功能=否", StringComparison.Ordinal);
            File.WriteAllText(authPath, text);
            NlogHelper.Info("已关闭 EPE 设备认证（AuthenticationConfig.ini · 是否开启认证功能=否）");
        }
    }

    private static void TryMigrateLegacyApiIni()
    {
        var legacy = Path.Combine(AppConstants.ConfigDir, "api.ini");
        if (!File.Exists(legacy))
            return;

        var authPath = Path.Combine(AppConstants.ConfigDir, FileName);
        var auth = new AuthenticationConfig();
        if (!string.IsNullOrWhiteSpace(auth.Authentication))
            return;

        foreach (var line in File.ReadAllLines(legacy))
        {
            var t = line.Trim();
            if (t.StartsWith("DeviceId=", StringComparison.OrdinalIgnoreCase))
            {
                auth.Authentication = t.Substring("DeviceId=".Length).Trim();
                auth.Save();
                NlogHelper.Info($"已从 api.ini 迁移认证编码到 {FileName}");
                break;
            }
        }
    }
}

/// <summary>
/// 生产线 / MES 接口配置（现场仅对接 MES 4.1–4.5）。
/// 旧 EPE URL 属性仅保留空默认值，供遗留类编译；运行时不再注册/调用 EPE。
/// </summary>
/// <summary>
/// MES 4.1–4.5 / 生产线接口配置（ProductionSystemConfig.ini）。
/// 以 ini 文件为准；构造时 Load，避免类默认值写回覆盖现场配置。
/// </summary>
public class ProductionSystemConfig : IniConfig
{
    private const string FileName = "ProductionSystemConfig.ini";
    private const string Section = "ProductionSystemConfig";

    /// <summary>协议 4.1 相对路径（相对 MES接口前缀）。</summary>
    [IniKeyNames(Section, "获取作业工件信息接口", "/integration/getWorkpieceInfo")]
    public string MesGetWorkpieceInfoPath { get; set; } = "/integration/getWorkpieceInfo";

    /// <summary>协议 4.2 相对路径（配置保留；操作员获取与借鉴项目一致走登录用户）。</summary>
    [IniKeyNames(Section, "获取作业人员信息接口", "/integration/getPersonInfoByFaceScan")]
    public string MesGetPersonInfoPath { get; set; } = "/integration/getPersonInfoByFaceScan";

    /// <summary>协议 4.3 相对路径。</summary>
    [IniKeyNames(Section, "设备数据回传接口", "/integration/uploadWorkData")]
    public string MesUploadWorkDataPath { get; set; } = "/integration/uploadWorkData";

    /// <summary>协议 4.4 相对路径。</summary>
    [IniKeyNames(Section, "设备状态上报接口", "/integration/uploadDeviceStatus")]
    public string MesUploadDeviceStatusPath { get; set; } = "/integration/uploadDeviceStatus";

    /// <summary>协议 4.5 相对路径。</summary>
    [IniKeyNames(Section, "设备故障上报接口", "/integration/uploadDeviceFault")]
    public string MesUploadDeviceFaultPath { get; set; } = "/integration/uploadDeviceFault";

    /// <summary>MES WebAPI 前缀（与借鉴项目一致，默认 80 端口；5089 是本机状态查询端口，不是 MES）。</summary>
    [IniKeyNames(Section, "MES接口前缀", "http://192.168.10.88/bri/biz")]
    public string MesBaseUrl { get; set; } = "http://192.168.10.88/bri/biz";

    /// <summary>工位编码，协议 4.1 stationCode（工位编码文档暂未定稿，可配置）。</summary>
    [IniKeyNames(Section, "MES工位编码", "GW-ZCTXGZZ")]
    public string MesStationCode { get; set; } = "GW-ZCTXGZZ";

    /// <summary>设备 ID，协议设备清单「轴承退卸工作站」。</summary>
    [IniKeyNames(Section, "MES设备ID", "2074669635279593478")]
    public string MesDeviceId { get; set; } = "2074669635279593478";

    /// <summary>协议 4.6：告知 MES 的本机查询完整 URL（文档/联调用，监听端口见下一项）。</summary>
    [IniKeyNames(Section, "设备状态查询接口", "")]
    public string MesDeviceStatusQueryUrl { get; set; } = "";

    /// <summary>协议 4.6：本机对 MES 开放的状态查询端口（GET /getDeviceStatus）。</summary>
    [IniKeyNames(Section, "MES本机状态查询端口", "5089")]
    public string MesLocalStatusPort { get; set; } = "5089";

    /// <summary>
    /// 曲线图保存目录（现场可改为共享盘路径）。
    /// 保存目录；有曲线时回传 remark=完整路径（无 data）。
    /// </summary>
    [IniKeyNames(Section, "曲线图路径", @"curves")]
    public string MesCurveImageDir { get; set; } = "curves";

    /// <summary>解析曲线保存目录：绝对路径直接用；相对路径相对程序根；空则程序根\curves。</summary>
    public string ResolveMesCurveImageDir()
    {
        var dir = (MesCurveImageDir ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dir))
            return Path.Combine(AppContext.BaseDirectory, "curves");
        if (!Path.IsPathRooted(dir))
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dir));
        return dir;
    }

    // —— 旧 EPE 完整 URL（可选；ini 无此项时保持空，避免 Save 写回旧 10.76 地址）——
    [IniKeyNames(Section, "设备授权验证接口", "")]
    public string Empower { get; set; } = "";

    [IniKeyNames(Section, "心跳接口", "")]
    public string Heartbeat { get; set; } = "";

    [IniKeyNames(Section, "人员同步接口", "")]
    public string Personnel { get; set; } = "";

    [IniKeyNames(Section, "修改密码接口", "")]
    public string ChangePassword { get; set; } = "";

    [IniKeyNames(Section, "车型数据接口", "")]
    public string VehicleData { get; set; } = "";

    [IniKeyNames(Section, "车列数据接口", "")]
    public string TrainData { get; set; } = "";

    [IniKeyNames(Section, "获取耐压任务接口", "")]
    public string GetMainTask { get; set; } = "";

    [IniKeyNames(Section, "耐压任务明细接口", "")]
    public string GetHoldTask { get; set; } = "";

    [IniKeyNames(Section, "耐压任务结果回传接口", "")]
    public string ResultFeedback { get; set; } = "";

    [IniKeyNames(Section, "业务字典数据接口", "")]
    public string BusinessDictionary { get; set; } = "";

    [IniKeyNames(Section, "耐压任务异常试验数据上传接口", "")]
    public string AbnormalData { get; set; } = "";

    public ProductionSystemConfig() : base(Path.Combine(AppConstants.ConfigDir, FileName))
    {
        Load();
    }

    /// <summary>仅当程序根 config 下不存在时从仓库模板复制，不覆盖已有配置。</summary>
    public static void EnsureUserIniFromBundled() =>
        IniBundledCopy.CopyBundledIfMissing(FileName);
}

/// <summary>确保程序根 config 下存在指定 INI（若缺失则无法从别处复制时仅建目录）。</summary>
internal static class IniBundledCopy
{
    public static void CopyBundledIfMissing(string fileName)
    {
        AppConstants.EnsureDirectories();
        var iniPath = Path.Combine(AppConstants.ConfigDir, fileName);
        if (File.Exists(iniPath))
            return;

        // ConfigDir 已是程序根\config，与 BaseDirectory\config 相同；再尝试显式 bundled 路径
        var bundled = Path.Combine(AppContext.BaseDirectory, "config", fileName);
        if (File.Exists(bundled) && !string.Equals(bundled, iniPath, StringComparison.OrdinalIgnoreCase))
            File.Copy(bundled, iniPath);
    }
}

/// <summary>
/// 应用主配置
/// </summary>
public class AppConfig : IniConfig
{
    [IniKeyNames("App", "WorkMode", "Offline")]
    public string WorkMode { get; set; } = "Offline";

    [IniKeyNames("App", "RememberPassword", "true")]
    public bool RememberPassword { get; set; } = true;

    [IniKeyNames("App", "LastUserId", "")]
    public string LastUserId { get; set; } = "";

    [IniKeyNames("App", "LastPassword", "")]
    public string LastPassword { get; set; } = "";

    [IniKeyNames("App", "WindowMaximized", "true")]
    public bool WindowMaximized { get; set; } = true;

    /// <summary>已废弃：Para.ini 固定为程序目录 config\Para.ini，不再读取此键。</summary>
    [IniKeyNames("App", "ParaIniFile", "")]
    public string ParaIniFile { get; set; } = "";

    public AppConfig() : base(Path.Combine(AppConstants.ConfigDir, "app.ini"))
    {
        SetSectionName("App");
        Load();
    }
}
