using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;

namespace PressureTestApp.UI.ViewModels;

public partial class SystemLogViewModel : ObservableObject
{
    /// <summary>表格绑定：经过级别/关键字筛选后的结果。</summary>
    public ObservableCollection<SystemLogEntry> LogEntries { get; } = new();

    public ObservableCollection<string> LevelFilterOptions { get; } =
    [
        "全部级别",
        "Info",
        "Warn",
        "Error",
        "Debug"
    ];

    public ObservableCollection<string> LogFileOptions { get; } = new();

    [ObservableProperty] private string? _selectedLevelFilter = "全部级别";

    [ObservableProperty] private string? _selectedLogFile;

    [ObservableProperty] private string _searchKeyword = string.Empty;

    [ObservableProperty] private string _statusText = "正在加载…";

    /// <summary>当前选中文件的全部原始条目（未筛选）。</summary>
    private List<SystemLogEntry> _sourceEntries = [];

    public string LogDirectoryPath => AppConstants.LogDir;

    public Action? NavigateBackToMainTest { get; set; }

    public SystemLogViewModel()
    {
        Refresh();
    }

    /// <summary>切换日志文件时重新读取文件内容。</summary>
    partial void OnSelectedLogFileChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        LoadSelectedFile();
    }

    /// <summary>切换级别时重新筛选，无需重新读文件。</summary>
    partial void OnSelectedLevelFilterChanged(string? value) => ApplyFilters();

    /// <summary>输入关键字时实时筛选。</summary>
    partial void OnSearchKeywordChanged(string value) => ApplyFilters();

    [RelayCommand]
    private void Close() => NavigateBackToMainTest?.Invoke();

    /// <summary>
    /// 刷新：重新扫描日志目录 + 重新读取当前文件。
    /// </summary>
    [RelayCommand]
    public void Refresh()
    {
        var files = SystemLogReader.ListLogFiles(LogDirectoryPath);
        LogFileOptions.Clear();
        foreach (var file in files)
            LogFileOptions.Add(file);

        if (LogFileOptions.Count == 0)
        {
            SelectedLogFile = null;
            _sourceEntries = [];
            LogEntries.Clear();
            StatusText = "日志目录为空，暂无日志文件";
            return;
        }

        // 保留当前选中项；若不存在则选最新文件
        if (SelectedLogFile is null || !LogFileOptions.Contains(SelectedLogFile))
            SelectedLogFile = LogFileOptions[0];
        else
            LoadSelectedFile();
    }

    [RelayCommand]
    private void Export()
    {
        // 功能待实现
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchKeyword = string.Empty;
    }

    /// <summary>从磁盘读取 SelectedLogFile 对应的内容。</summary>
    private void LoadSelectedFile()
    {
        if (string.IsNullOrWhiteSpace(SelectedLogFile))
            return;

        try
        {
            var lines = SystemLogReader.ReadLogFile(LogDirectoryPath, SelectedLogFile);
            _sourceEntries = lines
                .Select(l => new SystemLogEntry(l.Timestamp, l.Level, l.Logger, l.Message))
                .ToList();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _sourceEntries = [];
            LogEntries.Clear();
            StatusText = $"读取日志失败：{ex.Message}";
        }
    }

    /// <summary>按「日志级别 + 关键字」从 _sourceEntries 筛选到 LogEntries。</summary>
    private void ApplyFilters()
    {
        IEnumerable<SystemLogEntry> query = _sourceEntries;

        // 1. 级别筛选
        if (!string.IsNullOrWhiteSpace(SelectedLevelFilter) && SelectedLevelFilter != "全部级别")
        {
            var level = SelectedLevelFilter.ToUpperInvariant();
            query = query.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        // 2. 关键字搜索（时间、级别、模块、内容任意字段）
        var keyword = SearchKeyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(e =>
                (e.Timestamp?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.Level.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (e.Logger?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Message?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        LogEntries.Clear();
        foreach (var entry in query)
            LogEntries.Add(entry);

        var total = _sourceEntries.Count;
        var shown = LogEntries.Count;
        StatusText = shown == total
            ? $"共 {total} 条记录 · {SelectedLogFile}"
            : $"显示 {shown} / {total} 条 · {SelectedLogFile}";
    }
}

public partial class SystemLogEntry : ObservableObject
{
    public string Timestamp { get; }
    public string Level { get; }
    public string Logger { get; }
    public string Message { get; }

    public IBrush LevelBrush => Level switch
    {
        "ERROR" or "FATAL" => new SolidColorBrush(Color.FromRgb(225, 75, 74)),
        "WARN" => new SolidColorBrush(Color.FromRgb(239, 154, 78)),
        "DEBUG" or "TRACE" => new SolidColorBrush(Color.FromRgb(128, 128, 128)),
        _ => new SolidColorBrush(Color.FromRgb(46, 190, 88))
    };

    public SystemLogEntry(string timestamp, string level, string logger, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Logger = logger;
        Message = message;
    }
}
