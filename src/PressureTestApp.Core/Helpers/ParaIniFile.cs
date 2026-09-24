using System.Text;
using PressureTestApp.Core.Configs;

namespace PressureTestApp.Core.Helpers;

/// <summary>Para.ini「运行」工艺参数（目标压力/位移补充值等；兼容旧径向泵键名）</summary>
public sealed class ParaIniParameters
{
    public string TestTime { get; set; } = "";

    /// <summary>目标压力 HD1000</summary>
    public string TargetPressure { get; set; } = "";
    /// <summary>位移补充值 HD1022</summary>
    public string DisplacementOffset { get; set; } = "";
    /// <summary>开始减速压力 bar HD1078</summary>
    public string DecelerationThreshold { get; set; } = "";
    /// <summary>高压浮动限制T HD1068</summary>
    public string HighPressureFloatLimit { get; set; } = "";
    /// <summary>反向时间 S HD1072（旧键名：延时泄压时间）</summary>
    public string DelayReliefTime { get; set; } = "";
    /// <summary>快速慢速设置 M421：0 慢速 / 1 快速</summary>
    public string FastSlowSetting { get; set; } = "0";

    // —— 旧版径向泵字段（读兼容，新界面不再编辑）——
    public string MinSpeed { get; set; } = "";
    public string MaxSpeed { get; set; } = "";
    public string TemperatureOffset { get; set; } = "";
    public string HighPressureDisplayLimit { get; set; } = "";
    public string ReliefSpeed { get; set; } = "";
    public string LiquidLevelAlarmLow { get; set; } = "";
    public string LiquidLevelAlarmLimit { get; set; } = "";
    public string LiquidLevelAlarmHigh { get; set; } = "";
    public string StopHeatingTemp { get; set; } = "";
    public string StartHeatingTemp { get; set; } = "";
    public string AlarmTemp { get; set; } = "";
    public string LowPressureSet { get; set; } = "";
}

/// <summary>
/// 读取/写入 config\Para.ini。参数管理只维护「运行」节（工艺参数），与数据库车型无关。
/// </summary>
public static class ParaIniFile
{
    /// <summary>参数管理固定节名（图示 [运行]）</summary>
    public const string RunSectionName = "运行";

    /// <summary>随程序发布的模板（编译后位于 exe 同目录 config\Para.ini）</summary>
    public static string BundledPath => Path.Combine(AppContext.BaseDirectory, "config", "Para.ini");

    /// <summary>运行时可写副本（首次从 BundledPath 复制）</summary>
    public static string UserPath => Path.Combine(AppConstants.ConfigDir, "Para.ini");

    /// <summary>
    /// 固定且唯一：程序目录 config\Para.ini
    /// （调试 = ...\bin\Debug\net8.0\config\Para.ini；绝不使用仓库根 PressureTestApp\config\Para.ini）
    /// </summary>
    public static string ResolvePath()
    {
        AppConstants.EnsureDirectories();
        return BundledPath;
    }

    /// <summary>首次运行：确保程序目录 config\Para.ini 存在</summary>
    public static void EnsureUserCopy()
    {
        AppConstants.EnsureDirectories();
        if (File.Exists(UserPath)) return;
        if (File.Exists(BundledPath) &&
            !string.Equals(Path.GetFullPath(BundledPath), Path.GetFullPath(UserPath), StringComparison.OrdinalIgnoreCase))
            File.Copy(BundledPath, UserPath);
    }

    public static IReadOnlyList<string> GetSectionNames(string? path = null)
    {
        path ??= ResolvePath();
        if (!File.Exists(path)) return Array.Empty<string>();
        return ParseFile(path).Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// 参数管理/主界面「参数」下拉：仅「运行」及新格式工艺节（含 目标压力(MPa)），不含旧径向泵/车型节。
    /// </summary>
    public static IReadOnlyList<string> GetProcessParameterSectionNames(string? path = null)
    {
        EnsureRunSection(path);
        path ??= ResolvePath();
        if (!File.Exists(path))
            return new[] { RunSectionName };

        var data = ParseFile(path);
        var list = new List<string>();
        foreach (var kv in data)
        {
            if (IsProcessParameterSection(kv.Key, kv.Value))
                list.Add(kv.Key);
        }

        if (!list.Any(x => string.Equals(x, RunSectionName, StringComparison.OrdinalIgnoreCase)))
            list.Insert(0, RunSectionName);

        return list
            .OrderBy(x => string.Equals(x, RunSectionName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>删除旧工艺节（保留「运行」及含 目标压力(MPa) 的新节）。</summary>
    public static void RemoveLegacyProcessSections(string? path = null)
    {
        ApplyToAllIniFiles(path, RemoveLegacyProcessSectionsInFile);
    }

    private static void RemoveLegacyProcessSectionsInFile(string path)
    {
        if (!File.Exists(path)) return;
        var data = ParseFile(path);
        var remove = data.Keys
            .Where(k => !IsProcessParameterSection(k, data[k]))
            .ToList();
        if (remove.Count == 0) return;
        foreach (var k in remove)
            data.Remove(k);
        if (!data.Keys.Any(k => string.Equals(k, RunSectionName, StringComparison.OrdinalIgnoreCase)))
            data[RunSectionName] = CreateEmptyRunSectionKeys();
        WriteFile(path, data);
        NlogHelper.Info($"Para.ini 已清理旧节 {remove.Count} 个，保留工艺节（{path}）");
    }

    /// <summary>确保存在 [运行] 节（参数管理专用）。</summary>
    public static void EnsureRunSection(string? path = null)
    {
        path ??= ResolvePath();
        try
        {
            var data = File.Exists(path)
                ? ParseFile(path)
                : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var key = ResolveSectionKey(data, RunSectionName);
            if (key != null)
                return;

            data[RunSectionName] = CreateEmptyRunSectionKeys();
            WriteFile(path, data);
            foreach (var mirror in GetMirrorPaths(path))
            {
                try
                {
                    var m = File.Exists(mirror)
                        ? ParseFile(mirror)
                        : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                    if (ResolveSectionKey(m, RunSectionName) == null)
                    {
                        m[RunSectionName] = CreateEmptyRunSectionKeys();
                        WriteFile(mirror, m);
                    }
                }
                catch { /* ignore mirror */ }
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"Para.ini 创建「运行」节失败：{ex.Message}");
        }
    }

    private static Dictionary<string, string> CreateEmptyRunSectionKeys() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["目标压力(MPa)"] = "",
        ["位移补充值(mm)"] = "",
        ["开始减速压力(bar)"] = "",
        ["高压浮动限制T"] = "",
        ["反向时间(S)"] = "",
        ["快速慢速设置"] = "0"
    };

    /// <summary>是否为工艺参数节：节名「运行」，或含新键 目标压力(MPa)（不含旧径向泵/回路节）。</summary>
    public static bool IsProcessParameterSection(string sectionName, Dictionary<string, string> keys)
    {
        if (string.Equals(sectionName, RunSectionName, StringComparison.OrdinalIgnoreCase))
            return true;

        // 旧耐压「回路电压/电流」节不算
        if (keys.Keys.Any(k => k.Contains("回路", StringComparison.Ordinal) &&
                               (k.Contains("电压", StringComparison.Ordinal) || k.Contains("电流", StringComparison.Ordinal))))
            return false;

        // 仅新格式（参数管理保存的键名）；旧「径向泵_目标压力」节不再出现在下拉
        return keys.ContainsKey("目标压力(MPa)");
    }

    public static bool TryLoadSection(string sectionName, out ParaIniParameters parameters, out string resolvedSection, string? path = null)
    {
        parameters = new ParaIniParameters();
        resolvedSection = "";
        path ??= ResolvePath();
        if (!File.Exists(path) || string.IsNullOrWhiteSpace(sectionName))
            return false;

        var data = ParseFile(path);
        resolvedSection = ResolveSectionKey(data, sectionName.Trim()) ?? "";
        if (string.IsNullOrEmpty(resolvedSection))
            return false;

        var s = data[resolvedSection];
        parameters.TestTime = Get(s, "试验时间");

        parameters.TargetPressure = FirstNonEmpty(Get(s, "目标压力(MPa)"), Get(s, "目标压力"), Get(s, "径向泵_目标压力"));
        parameters.DisplacementOffset = FirstNonEmpty(Get(s, "位移补充值(mm)"), Get(s, "位移补充值"), Get(s, "径向泵液位补充值"));
        parameters.DecelerationThreshold = FirstNonEmpty(
            Get(s, "开始减速压力(bar)"),
            Get(s, "开始减速压力bar"),
            Get(s, "径向泵_距目标多少减速"));
        parameters.HighPressureFloatLimit = FirstNonEmpty(
            Get(s, "高压浮动限制T"),
            Get(s, "高压浮动限制"));
        parameters.DelayReliefTime = FirstNonEmpty(
            Get(s, "反向时间(S)"),
            Get(s, "反向时间S"),
            Get(s, "延时泄压时间"));
        parameters.FastSlowSetting = FirstNonEmpty(Get(s, "快速慢速设置"), "0");

        parameters.MinSpeed = Get(s, "径向泵_minSpeed");
        parameters.MaxSpeed = Get(s, "径向泵_maxSpeed");
        parameters.TemperatureOffset = Get(s, "径向泵温度补充值");
        parameters.HighPressureDisplayLimit = Get(s, "高压限制显示压力");
        parameters.ReliefSpeed = Get(s, "径向泵泄压速度");
        parameters.LiquidLevelAlarmLow = Get(s, "径向泵液位报警下限");
        parameters.LiquidLevelAlarmLimit = Get(s, "径向泵液位报警极限");
        parameters.LiquidLevelAlarmHigh = Get(s, "径向泵液位报警上限");
        parameters.StopHeatingTemp = Get(s, "径向泵停止加热温度");
        parameters.StartHeatingTemp = Get(s, "径向泵开始加热温度");
        parameters.AlarmTemp = Get(s, "径向泵报警温度");
        parameters.LowPressureSet = Get(s, "径向泵低压设定压力");
        return true;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }
        return "";
    }

    public static void SaveSection(string sectionName, ParaIniParameters parameters, string? path = null)
    {
        ApplyToAllIniFiles(path, p => WriteSectionToFile(p, sectionName, parameters));
    }

    /// <summary>型号管理新增车型时：在 Para.ini 中创建对应空节（与旧版一致，仅 [节名]）。</summary>
    public static void EnsureSectionExists(string sectionName, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(sectionName)) return;
        ApplyToAllIniFiles(path, p => EnsureSectionExistsInFile(p, sectionName.Trim()));
    }

    /// <summary>将数据库中全部车型名同步到 Para.ini（补全缺失的节）。</summary>
    public static void SyncSections(IEnumerable<string> sectionNames, string? path = null)
    {
        foreach (var name in sectionNames)
            EnsureSectionExists(name, path);
    }

    public static void RenameSection(string oldName, string newName, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        var o = (oldName ?? "").Trim();
        var n = newName.Trim();
        if (string.IsNullOrEmpty(o))
        {
            EnsureSectionExists(n, path);
            return;
        }

        if (string.Equals(o, n, StringComparison.OrdinalIgnoreCase))
            return;

        ApplyToAllIniFiles(path, p => RenameSectionInFile(p, o, n));
    }

    public static void RemoveSection(string sectionName, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(sectionName)) return;
        ApplyToAllIniFiles(path, p => RemoveSectionFromFile(p, sectionName.Trim()));
    }

    private static void ApplyToAllIniFiles(string? path, Action<string> action)
    {
        var primary = path ?? ResolvePath();
        try
        {
            action(primary);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"Para.ini 写入失败（{primary}）：{ex.Message}");
        }

        foreach (var mirror in GetMirrorPaths(primary))
        {
            try
            {
                action(mirror);
            }
            catch (Exception ex)
            {
                NlogHelper.Info($"Para.ini 同步到 {mirror} 失败：{ex.Message}");
            }
        }
    }

    /// <summary>不再同步到仓库根 config\Para.ini，仅写程序目录那一份。</summary>
    private static IEnumerable<string> GetMirrorPaths(string primary)
    {
        yield break;
    }

    private static void EnsureSectionExistsInFile(string path, string sectionName)
    {
        var data = File.Exists(path)
            ? ParseFile(path)
            : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var section = ResolveSectionKey(data, sectionName) ?? sectionName;
        if (!data.ContainsKey(section))
            data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        WriteFile(path, data);
    }

    private static void RenameSectionInFile(string path, string oldName, string newName)
    {
        if (!File.Exists(path))
        {
            EnsureSectionExistsInFile(path, newName);
            return;
        }

        var data = ParseFile(path);
        var oldKey = ResolveSectionKey(data, oldName);
        if (string.IsNullOrEmpty(oldKey))
        {
            EnsureSectionExistsInFile(path, newName);
            return;
        }

        var content = data[oldKey];
        data.Remove(oldKey);
        var newKey = ResolveSectionKey(data, newName) ?? newName;
        if (!data.ContainsKey(newKey))
            data[newKey] = content;
        else
            foreach (var kv in content)
                data[newKey][kv.Key] = kv.Value;
        WriteFile(path, data);
    }

    private static void RemoveSectionFromFile(string path, string sectionName)
    {
        if (!File.Exists(path)) return;
        var data = ParseFile(path);
        var key = ResolveSectionKey(data, sectionName);
        if (string.IsNullOrEmpty(key)) return;
        data.Remove(key);
        WriteFile(path, data);
    }

    private static void WriteSectionToFile(string path, string sectionName, ParaIniParameters parameters)
    {
        var data = File.Exists(path)
            ? ParseFile(path)
            : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var section = ResolveSectionKey(data, sectionName.Trim()) ?? sectionName.Trim();

        // 参数管理新建/保存的节：只写工艺键（与 [运行] 同类内容）
        var s = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Set(s, "目标压力(MPa)", parameters.TargetPressure);
        Set(s, "位移补充值(mm)", parameters.DisplacementOffset);
        Set(s, "开始减速压力(bar)", parameters.DecelerationThreshold);
        Set(s, "高压浮动限制T", parameters.HighPressureFloatLimit);
        Set(s, "反向时间(S)", parameters.DelayReliefTime);
        Set(s, "快速慢速设置", string.IsNullOrWhiteSpace(parameters.FastSlowSetting) ? "0" : parameters.FastSlowSetting.Trim());

        data[section] = s;
        WriteFile(path, data);
    }

    private static string? ResolveSectionKey(Dictionary<string, Dictionary<string, string>> data, string sectionName)
    {
        if (data.ContainsKey(sectionName)) return sectionName;
        return data.Keys.FirstOrDefault(k => string.Equals(k, sectionName, StringComparison.OrdinalIgnoreCase));
    }

    private static string Get(Dictionary<string, string> section, string key) =>
        section.TryGetValue(key, out var v) ? v : "";

    private static void Set(Dictionary<string, string> section, string key, string value) =>
        section[key] = value ?? "";

    public static Dictionary<string, Dictionary<string, string>> ParseFile(string path)
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var current = "";
        foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                if (!data.ContainsKey(current))
                    data[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var idx = line.IndexOf('=');
            if (idx < 0 || string.IsNullOrEmpty(current)) continue;
            data[current][line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }

        return data;
    }

    private static void WriteFile(string path, Dictionary<string, Dictionary<string, string>> data)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        foreach (var section in data.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"[{section.Key}]");
            foreach (var kv in section.Value)
                sb.AppendLine($"{kv.Key}={kv.Value}");
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
