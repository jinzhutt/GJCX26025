namespace PressureTestApp.Communication.Api;

/// <summary>
/// MES 4.3 / 试验结论判定（与绝缘仪界面、主界面耐压回路逻辑一致）。
/// </summary>
public static class MesResultJudge
{
    /// <summary>
    /// 绝缘电阻：测试值 ≥ 标准下限 → 合格，否则不合格。
    /// </summary>
    public static string JudgeInsulationResistance(double? irValueMohm, double? irMinMohm)
    {
        if (irValueMohm is null || irMinMohm is null)
            return "";
        return irValueMohm.Value >= irMinMohm.Value ? "合格" : "不合格";
    }

    /// <summary>
    /// 绝缘耐压（漏电流）：测试值 ≤ 标准上限 → 合格，否则不合格。
    /// </summary>
    public static string JudgeWithstandLeakage(double? leakValueMa, double? leakMaxMa)
    {
        if (leakValueMa is null || leakMaxMa is null)
            return "";
        return leakValueMa.Value <= leakMaxMa.Value ? "合格" : "不合格";
    }

    /// <summary>
    /// 匝间绝缘：U-V / V-W / U-W 三项均为 OK → 合格；任一项 NG → 不合格。
    /// （与 TH2883 GetResult 中 temp[0]=="1" 为 OK 一致）
    /// </summary>
    public static string JudgeTurnInsulation(string? uvStatus, string? vwStatus, string? uwStatus)
    {
        static bool IsOk(string? s) =>
            string.Equals(s?.Trim(), "OK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s?.Trim(), "合格", StringComparison.Ordinal);

        static bool IsNg(string? s) =>
            string.Equals(s?.Trim(), "NG", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s?.Trim(), "不合格", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(uvStatus) && string.IsNullOrWhiteSpace(vwStatus) && string.IsNullOrWhiteSpace(uwStatus))
            return "";

        if (IsNg(uvStatus) || IsNg(vwStatus) || IsNg(uwStatus))
            return "不合格";

        if (IsOk(uvStatus) && IsOk(vwStatus) && IsOk(uwStatus))
            return "合格";

        return "";
    }

    public static bool TryParseDouble(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var s = text.Trim();
        // 去掉单位后缀，如 "999 MΩ" / "1.2mA"
        var end = 0;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] is '.' or '-' or '+'))
            end++;
        if (end == 0)
            return false;
        return double.TryParse(s[..end], out value);
    }
}
