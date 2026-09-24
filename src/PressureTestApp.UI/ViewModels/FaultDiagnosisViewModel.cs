using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PressureTestApp.UI.ViewModels;

public partial class FaultDiagnosisViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public ObservableCollection<FaultDiagnosisAlarmRow> AlarmRows { get; } = new();

    public Action? NavigateBackToMainTest { get; set; }

    public FaultDiagnosisViewModel(MainViewModel main)
    {
        _main = main;
        foreach (var def in AlarmDefinitions)
            AlarmRows.Add(new FaultDiagnosisAlarmRow(def.Name, def.Suggestion));
    }

    public bool PlcConnected => _main.PlcConnected;

    public string PlcStatusText => _main.StatusPlcText;

    public int ActiveAlarmCount => AlarmRows.Count(r => r.IsAlarm);

    public string OverallStatusText =>
        !PlcConnected ? "PLC 未连接，无法获取实时诊断数据"
        : ActiveAlarmCount == 0 ? "设备运行正常，未检测到报警"
        : $"检测到 {ActiveAlarmCount} 项报警，请按建议逐项排查";

    public bool HasActiveAlarm => PlcConnected && ActiveAlarmCount > 0;

    public IBrush OverallStatusBrush => HasActiveAlarm
        ? new SolidColorBrush(Color.FromRgb(225, 75, 74))
        : PlcConnected
            ? new SolidColorBrush(Color.FromRgb(46, 190, 88))
            : new SolidColorBrush(Color.FromRgb(128, 128, 128));

    public string HighPressureText => $"{_main.PlcAiMai00:F2} MPa";

    public string LowPressureText => $"{_main.PlcAiMai01:F2} bar";

    public string TemperatureText => $"{_main.PlcAiMai02:F1} ℃";

    public string LiquidLevelText => $"{_main.PlcAiMai03:F1}";

    public string MotorSpeedText => $"{_main.PlcAiMai16:F0} rpm";

    public string LiquidLevelLimitText =>
        $"下限 {_main.PlcPubP06:F1} / 极限 {_main.PlcPubP07:F1} / 上限 {_main.PlcPubP08:F1}";

    public string AlarmTempText => $"{_main.PlcPubP11:F1} ℃";

    [RelayCommand]
    private void Close() => NavigateBackToMainTest?.Invoke();

    [RelayCommand]
    public void RefreshFromMain()
    {
        UpdateRow(0, _main.PlcDiMdi10);
        UpdateRow(1, _main.PlcDiMdi11);
        UpdateRow(2, _main.PlcDiMdi12);
        UpdateRow(3, _main.PlcDiMdi13);
        UpdateRow(4, _main.PlcDiMdi14);
        UpdateRow(5, _main.PlcDiMdi15);
        UpdateRow(6, _main.PlcDiMdi16);
        UpdateRow(7, _main.PlcDiMdi17);
        UpdateRow(8, _main.PlcDiMdi18);
        UpdateRow(9, _main.PlcDiMdi19);
        UpdateRow(10, _main.PlcDiMdi20);

        OnPropertyChanged(nameof(PlcConnected));
        OnPropertyChanged(nameof(PlcStatusText));
        OnPropertyChanged(nameof(ActiveAlarmCount));
        OnPropertyChanged(nameof(OverallStatusText));
        OnPropertyChanged(nameof(HasActiveAlarm));
        OnPropertyChanged(nameof(OverallStatusBrush));
        OnPropertyChanged(nameof(HighPressureText));
        OnPropertyChanged(nameof(LowPressureText));
        OnPropertyChanged(nameof(TemperatureText));
        OnPropertyChanged(nameof(LiquidLevelText));
        OnPropertyChanged(nameof(MotorSpeedText));
        OnPropertyChanged(nameof(LiquidLevelLimitText));
        OnPropertyChanged(nameof(AlarmTempText));
    }

    private void UpdateRow(int index, bool isAlarm)
    {
        if (index < 0 || index >= AlarmRows.Count)
            return;
        AlarmRows[index].IsAlarm = isAlarm;
    }

    private static readonly AlarmDefinition[] AlarmDefinitions =
    [
        new("径向泵液位下限报警", "检查油箱液位是否偏低，必要时补充液压油；核对液位下限设定值是否合理。"),
        new("径向泵液位极限报警", "立即停机，检查油箱液位与液位传感器；排除泄漏后补充油液并复位。"),
        new("径向泵液位上限报警", "检查液位是否过高或传感器异常；核对液位上限设定值并调整。"),
        new("径向泵温度报警", "检查油温与加热/冷却回路；确认报警温度设定值，排查散热与负载异常。"),
        new("径向伺服报警", "检查伺服驱动器面板故障码、电机接线与机械卡阻；必要时断电复位伺服。"),
        new("径向泵过滤报警", "检查液压滤芯是否堵塞，更换滤芯并确认压差开关/传感器正常。"),
        new("急停报警", "确认现场急停按钮是否被按下或线路断开；排除危险后旋转复位急停并检查安全回路。"),
        new("径向泵温液传感器掉线", "检查温液传感器接线、插头与供电；更换损坏传感器并重新标定。"),
        new("径向泵低压传感器掉线", "检查低压传感器接线与信号线；确认传感器供电正常，必要时更换传感器。"),
        new("径向泵高压传感器掉线", "检查高压传感器接线与屏蔽；确认量程匹配，更换故障传感器。"),
        new("总报警信号", "结合上表已激活项逐项排查；若单项未亮但总报警有效，检查 PLC 总报警逻辑与复位条件。")
    ];

    private record AlarmDefinition(string Name, string Suggestion);
}

public partial class FaultDiagnosisAlarmRow : ObservableObject
{
    public string Name { get; }
    public string Suggestion { get; }

    [ObservableProperty] private bool _isAlarm;

    public string StatusText => IsAlarm ? "报警" : "正常";

    public IBrush StatusBrush => IsAlarm
        ? new SolidColorBrush(Color.FromRgb(225, 75, 74))
        : new SolidColorBrush(Color.FromRgb(46, 190, 88));

    public FaultDiagnosisAlarmRow(string name, string suggestion)
    {
        Name = name;
        Suggestion = suggestion;
    }

    partial void OnIsAlarmChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
    }
}
