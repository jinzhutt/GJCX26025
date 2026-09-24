namespace PressureTestApp.Core.Configs;

/// <summary>
/// 保护参数（设备检查计数阈值）
/// 文件路径：{程序目录}\config\DeviceInspectConfig.ini
/// </summary>
public sealed class DeviceInspectConfig : IniConfig
{
    /// <summary>出厂默认；仅当 INI 或某一键不存在时使用，运行中以 INI 实际内容为准。</summary>
    public const string DefaultThreshold = "10000";

    public const string SectionName = "DeviceInspectConfig";

    public static string ResolveFilePath() =>
        Path.Combine(AppConstants.ConfigDir, "DeviceInspectConfig.ini");

    public DeviceInspectConfig()
        : base(ResolveFilePath())
    {
        SetSectionName(SectionName);
        Load();
    }

    [IniKeyNames(SectionName, "急停计数", DefaultThreshold)]
    public string EmergencyStop { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "接地报警计数", DefaultThreshold)]
    public string Grounding { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "电铃计数", DefaultThreshold)]
    public string ElectricBell { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "门联锁计数", DefaultThreshold)]
    public string Interlock { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "调压底限计数", DefaultThreshold)]
    public string LowerLimit { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "调压高限计数", DefaultThreshold)]
    public string HighLimit { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "调压电机报警计数", DefaultThreshold)]
    public string ElectricalMachinery { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "主接触器计数", DefaultThreshold)]
    public string MainContact { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "从接触器计数", DefaultThreshold)]
    public string FromContact { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "放电次数", DefaultThreshold)]
    public string Discharge { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "电动门锁计数", DefaultThreshold)]
    public string DoorLock { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "启动按钮计数", DefaultThreshold)]
    public string Start { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "升压按钮计数", DefaultThreshold)]
    public string Boost { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "降压按钮计数", DefaultThreshold)]
    public string StepDown { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "停止按钮计数", DefaultThreshold)]
    public string Stop { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "复位按钮计数", DefaultThreshold)]
    public string Reset { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "计时按钮计数", DefaultThreshold)]
    public string TimeBtn { get; set; } = DefaultThreshold;

    [IniKeyNames(SectionName, "计时器计数", DefaultThreshold)]
    public string TimerWare { get; set; } = DefaultThreshold;
}
