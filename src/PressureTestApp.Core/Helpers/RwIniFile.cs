using System.Globalization;
using System.Text;
using PressureTestApp.Core.Configs;

namespace PressureTestApp.Core.Helpers;

/// <summary>硬件校准单通道在 rw.ini 中的零点/增益（与 GJCS UCCalibration 一致）。</summary>
public sealed class RwIniCalibrationValues
{
    public double Zero { get; set; }
    public double Gain { get; set; } = 1;
    public int Index { get; set; }
}

/// <summary>与旧版 frmHardWare 控件对应的节名与 PLC 索引。</summary>
public sealed record RwCalibrationChannelDef(string DisplayTitle, string IniSection, int PlcIndex, bool IsOutput);

/// <summary>
/// 读取/写入旧项目 config\rw.ini（节名 = UCCalibration_ucCalibrationN，键 Gain / Zero / Index）。
/// </summary>
public static class RwIniFile
{
    public static string BundledPath => Path.Combine(AppContext.BaseDirectory, "config", "rw.ini");
    public static string UserPath => Path.Combine(AppConstants.ConfigDir, "rw.ini");

    public static readonly RwCalibrationChannelDef[] InputChannels =
    [
        new("高压输出(V)", "UCCalibration_ucCalibration2", 0, false),
        new("泄漏电流(mA)", "UCCalibration_ucCalibration3", 1, false),
        new("高压放电(V)", "UCCalibration_ucCalibration4", 2, false),
        new("厂房电压(V)", "UCCalibration_ucCalibration9", 3, false)
    ];

    public static readonly RwCalibrationChannelDef OutputChannel =
        new("电压设定(V)", "UCCalibration_ucCalibration1", 0, true);

    public static IReadOnlyList<RwCalibrationChannelDef> AllChannels { get; } =
        InputChannels.Append(OutputChannel).ToArray();

    /// <summary>测定值 = 工程值 * 增益值 - 零点值</summary>
    public static double ToMeasured(double engineering, double gain, double zero) =>
        engineering * gain - zero;

    /// <summary>由测定值反算工程值</summary>
    public static double ToEngineering(double measured, double gain, double zero) =>
        Math.Abs(gain) < 1e-12 ? measured : (measured + zero) / gain;

    /// <summary>固定：程序目录 config\rw.ini（如 bin\Debug\net8.0\config\rw.ini）</summary>
    public static string ResolvePath()
    {
        AppConstants.EnsureDirectories();
        if (File.Exists(BundledPath))
            return BundledPath;

        EnsureUserCopy();
        if (File.Exists(UserPath))
            return UserPath;

        return BundledPath;
    }

    /// <summary>首次运行：确保程序目录 config\rw.ini 存在（含五个校准节）。</summary>
    public static string EnsureExists()
    {
        MigrateLegacyRootFile();

        var path = ResolvePath();
        if (File.Exists(path))
            return path;

        WriteDefaultFile(path);
        return path;
    }

    public static void EnsureUserCopy()
    {
        AppConstants.EnsureDirectories();
        if (File.Exists(UserPath)) return;
        if (File.Exists(BundledPath))
            File.Copy(BundledPath, UserPath);
    }

    public static RwIniCalibrationValues LoadChannel(string iniSection, int defaultIndex, string? path = null)
    {
        path ??= ResolvePath();
        if (!File.Exists(path))
            return new RwIniCalibrationValues { Index = defaultIndex };

        var data = ParseFile(path);
        if (!data.TryGetValue(iniSection, out var section))
        {
            var key = data.Keys.FirstOrDefault(k =>
                string.Equals(k, iniSection, StringComparison.OrdinalIgnoreCase));
            if (key == null)
                return new RwIniCalibrationValues { Index = defaultIndex };
            section = data[key];
        }

        return new RwIniCalibrationValues
        {
            Zero = GetDouble(section, "Zero", 0),
            Gain = GetDouble(section, "Gain", 1),
            Index = GetInt(section, "Index", defaultIndex)
        };
    }

    public static void SaveChannel(string iniSection, double zero, double gain, int index, string? path = null)
    {
        ApplyToAllIniFiles(path, p => WriteChannelToFile(p, iniSection, zero, gain, index));
    }

    private static void MigrateLegacyRootFile()
    {
        try
        {
            var configDir = Path.Combine(AppContext.BaseDirectory, "config");
            var legacyRoot = Path.Combine(AppContext.BaseDirectory, "rw.ini");
            var target = Path.Combine(configDir, "rw.ini");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            if (File.Exists(legacyRoot) && !File.Exists(target))
                File.Move(legacyRoot, target);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"rw.ini 迁移失败：{ex.Message}");
        }
    }

    private static void WriteDefaultFile(string path)
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ch in AllChannels)
            data[ch.IniSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        WriteFile(path, data);
    }

    private static void ApplyToAllIniFiles(string? path, Action<string> action)
    {
        var primary = path ?? EnsureExists();
        try
        {
            action(primary);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"rw.ini 写入失败（{primary}）：{ex.Message}");
            throw;
        }

        foreach (var mirror in GetMirrorPaths(primary))
        {
            try
            {
                action(mirror);
            }
            catch (Exception ex)
            {
                NlogHelper.Info($"rw.ini 同步到 {mirror} 失败：{ex.Message}");
            }
        }
    }

    /// <summary>不写仓库根等其它副本，只维护程序目录 config。</summary>
    private static IEnumerable<string> GetMirrorPaths(string primary)
    {
        yield break;
    }

    private static void WriteChannelToFile(string path, string iniSection, double zero, double gain, int index)
    {
        var data = File.Exists(path)
            ? ParseFile(path)
            : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        var sectionKey = data.Keys.FirstOrDefault(k =>
                             string.Equals(k, iniSection, StringComparison.OrdinalIgnoreCase))
                         ?? iniSection;
        if (!data.ContainsKey(sectionKey))
            data[sectionKey] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var s = data[sectionKey];
        SetOrRemove(s, "Zero", zero, 0);
        SetOrRemove(s, "Gain", gain, 1);
        SetOrRemoveInt(s, "Index", index, 0);

        WriteFile(path, data);
    }

    private static void SetOrRemove(Dictionary<string, string> section, string key, double value, double defaultValue)
    {
        if (Math.Abs(value - defaultValue) < 1e-12)
            section.Remove(key);
        else
            section[key] = value.ToString(CultureInfo.InvariantCulture);
    }

    private static void SetOrRemoveInt(Dictionary<string, string> section, string key, int value, int defaultValue)
    {
        if (value == defaultValue)
            section.Remove(key);
        else
            section[key] = value.ToString(CultureInfo.InvariantCulture);
    }

    private static double GetDouble(Dictionary<string, string> section, string key, double defaultValue)
    {
        if (!section.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
               || double.TryParse(raw, out v)
            ? v
            : defaultValue;
    }

    private static int GetInt(Dictionary<string, string> section, string key, int defaultValue)
    {
        if (!section.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
               || int.TryParse(raw, out v)
            ? v
            : defaultValue;
    }

    private static Dictionary<string, Dictionary<string, string>> ParseFile(string path)
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
            foreach (var kv in section.Value.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"{kv.Key}={kv.Value}");
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
