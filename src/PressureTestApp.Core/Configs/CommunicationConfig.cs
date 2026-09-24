using System;
using System.IO;

namespace PressureTestApp.Core.Configs;

public class CommunicationConfig : IniConfig
{
    private const string FileName = "CommunicationConfig.ini";
    public const string SectionName = "CommunicationConfig";

    [IniKeyNames(SectionName, "PLC_IP", "192.168.0.1")]
    public string PlcIp { get; set; } = "192.168.0.1";

    [IniKeyNames(SectionName, "PLC_Port", "102")]
    public string PlcPort { get; set; } = "102";

    [IniKeyNames(SectionName, "TempHumi_Port", "COM1")]
    public string TempHumiPortName { get; set; } = "COM1";

    [IniKeyNames(SectionName, "TempHumi_BaudRate", "9600")]
    public string TempHumiBaudRate { get; set; } = "9600";

    [IniKeyNames(SectionName, "Insulation_Port", "COM2")]
    public string InsulationPortName { get; set; } = "COM2";

    [IniKeyNames(SectionName, "Insulation_BaudRate", "9600")]
    public string InsulationBaudRate { get; set; } = "9600";

    [IniKeyNames(SectionName, "Turn_Port", "COM3")]
    public string TurnPortName { get; set; } = "COM3";

    [IniKeyNames(SectionName, "Turn_BaudRate", "9600")]
    public string TurnBaudRate { get; set; } = "9600";

    public CommunicationConfig()
        : base(ResolveFilePath(), SectionName)
    {
        Load();
    }

    public static string ResolveFilePath() =>
        Path.Combine(AppConstants.ConfigDir, FileName);

    public static void EnsureUserIniFromBundled() => IniBundledCopy.CopyBundledIfMissing(FileName);
}