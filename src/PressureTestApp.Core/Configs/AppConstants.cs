namespace PressureTestApp.Core.Configs;

/// <summary>
/// 应用全局常量。目录一律相对程序根目录（可执行文件所在目录），不走用户目录。
/// </summary>
public static class AppConstants
{
    /// <summary>应用版本</summary>
    public const string Version = "2.0.0";

    /// <summary>应用名称</summary>
    public const string AppName = "一体式新型耐压设备";

    /// <summary>程序根目录（可执行文件所在目录，调试时为 bin\Debug\net8.0）</summary>
    public static string RootDir => AppContext.BaseDirectory;

    /// <summary>配置目录：始终为程序根\config（如 bin\Debug\net8.0\config），不使用仓库根或其它路径</summary>
    public static string ConfigDir => Path.Combine(RootDir, "config");

    /// <summary>MES 配置文件：程序根\config\ProductionSystemConfig.ini</summary>
    public static string ProductionSystemConfigPath =>
        Path.Combine(ConfigDir, "ProductionSystemConfig.ini");

    /// <summary>数据库目录：程序根\data（不读 Configs 里的业务 ini）</summary>
    public static string DataDir => Path.Combine(RootDir, "data");

    /// <summary>日志目录：程序根\logs</summary>
    public static string LogDir => Path.Combine(RootDir, "logs");

    /// <summary>Excel 模板目录：程序根\templates</summary>
    public static string TemplateDir => Path.Combine(RootDir, "templates");

    /// <summary>报表导出目录：程序根\reports</summary>
    public static string ReportDir => Path.Combine(RootDir, "reports");

    /// <summary>确保程序根下各目录存在</summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(TemplateDir);
        Directory.CreateDirectory(ReportDir);
    }
}
