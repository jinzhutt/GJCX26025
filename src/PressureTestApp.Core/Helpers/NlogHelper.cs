using NLog;
using NLog.Config;
using NLog.Targets;

namespace PressureTestApp.Core.Helpers;

/// <summary>
/// NLog 日志辅助类（保留原项目接口风格）
/// </summary>
public static class NlogHelper
{
    private static Logger? _logger;
    private static readonly object _lock = new();

    /// <summary>
    /// 初始化 NLog（应用启动时调用一次）
    /// </summary>
    public static void Initialize(string logDirectory)
    {
        lock (_lock)
        {
            if (_logger != null) return;

            Directory.CreateDirectory(logDirectory);

            var config = new LoggingConfiguration();

            // 文件日志
            var fileTarget = new FileTarget("file")
            {
                FileName = Path.Combine(logDirectory, "${shortdate}.log"),
                Layout = "${longdate} [${level:uppercase=true}] ${logger:shortName=true} | ${message} ${exception:format=tostring}",
                Encoding = System.Text.Encoding.UTF8,
                ArchiveAboveSize = 10 * 1024 * 1024,    // 单文件 10MB
                MaxArchiveFiles = 30,                    // 保留 30 个
                ConcurrentWrites = true,
                KeepFileOpen = false
            };

            // 控制台
            var consoleTarget = new ConsoleTarget("console")
            {
                Layout = "${time} [${level:uppercase=true}] ${logger:shortName=true} | ${message}"
            };

            config.AddTarget(fileTarget);
            config.AddTarget(consoleTarget);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);

            LogManager.Configuration = config;
            _logger = LogManager.GetLogger("App");
        }
    }

    public static void Trace(string message) => _logger?.Trace(message);
    public static void Debug(string message) => _logger?.Debug(message);
    public static void Info(string message) => _logger?.Info(message);
    public static void Warn(string message) => _logger?.Warn(message);
    public static void Warn(Exception ex, string message) => _logger?.Warn(ex, message);
    public static void Error(string message) => _logger?.Error(message);
    public static void Error(Exception ex, string message) => _logger?.Error(ex, message);
    public static void Fatal(Exception ex, string message) => _logger?.Fatal(ex, message);

    /// <summary>
    /// 获取分类日志器（用于不同模块区分）
    /// </summary>
    public static Logger GetLogger(string name) => LogManager.GetLogger(name);
}
