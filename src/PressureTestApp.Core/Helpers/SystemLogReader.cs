using System.Text.RegularExpressions;

namespace PressureTestApp.Core.Helpers;

/// <summary>
/// 读取并解析 NLog 写入的本地日志文件。
/// 格式：${longdate} [${level}] ${logger} | ${message}
/// </summary>
public static class SystemLogReader
{
    // 示例：2026-07-04 11:20:15.123 [INFO] MainViewModel | 侧边导航：MainTest
    private static readonly Regex LinePattern = new(
        @"^(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+\[(?<level>\w+)\]\s+(?<logger>[^|]+?)\s+\|\s+(?<message>.+)$",
        RegexOptions.Compiled);

    /// <summary>列出日志目录下所有 .log 文件，按修改时间倒序。</summary>
    public static IReadOnlyList<string> ListLogFiles(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .OrderByDescending(name =>
            {
                var path = Path.Combine(logDirectory, name);
                return File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.MinValue;
            })
            .ToList();
    }

    /// <summary>读取指定日志文件并解析为条目列表。</summary>
    public static IReadOnlyList<SystemLogLine> ReadLogFile(string logDirectory, string fileName)
    {
        var path = Path.Combine(logDirectory, fileName);
        if (!File.Exists(path))
            return Array.Empty<SystemLogLine>();

        var lines = new List<SystemLogLine>();
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var match = LinePattern.Match(line);
            if (match.Success)
            {
                lines.Add(new SystemLogLine(
                    match.Groups["time"].Value,
                    match.Groups["level"].Value.ToUpperInvariant(),
                    match.Groups["logger"].Value.Trim(),
                    match.Groups["message"].Value.Trim()));
            }
            else
            {
                // 无法解析的行（如异常堆栈续行）仍保留，便于排查
                lines.Add(new SystemLogLine(string.Empty, "INFO", string.Empty, line));
            }
        }

        return lines;
    }
}

public record SystemLogLine(string Timestamp, string Level, string Logger, string Message);
