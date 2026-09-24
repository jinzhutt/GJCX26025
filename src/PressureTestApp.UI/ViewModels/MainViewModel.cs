using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
//using DocumentFormat.OpenXml.Spreadsheet;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Communication.Modbus;
using PressureTestApp.Communication.Plc;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;
using PressureTestApp.Core.Services;
using PressureTestApp.Data;
using PressureTestApp.Data.Entities;
using PressureTestApp.Data.Repositories;
using PressureTestApp.UI.Helpers;
using PressureTestApp.UI.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ursa.Controls;

namespace PressureTestApp.UI.ViewModels;

public enum MainNavSection
{
    MainTest,
    ReportManagement,
    DeviceInspection,
    HardwareCalibration,
    UserManagement,
    SystemLog,
    FaultDiagnosis
}

/// <summary>主试验界面右侧：工艺 / 摄像头一 / 报表 顶部标签。</summary>
public enum MainTestRightPanelTab
{
    Process,
    CameraOne,
    Report
}

/// <summary>
/// 主界面 ViewModel —— 管理三模式（在线 / 离线 / Excel 导入）的统一状态与切换
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{

    

    // ============= 侧边导航 =============
    [ObservableProperty] private MainNavSection _currentNavSection = MainNavSection.MainTest;

    partial void OnCurrentNavSectionChanged(MainNavSection value)
    {
        OnPropertyChanged(nameof(ShowMainTestContent));
        OnPropertyChanged(nameof(ShowReportManagementContent));
        OnPropertyChanged(nameof(ShowDeviceInspectionContent));
        OnPropertyChanged(nameof(ShowHardwareCalibrationContent));
        OnPropertyChanged(nameof(ShowUserManagementContent));
        OnPropertyChanged(nameof(ShowSystemLogContent));
        OnPropertyChanged(nameof(ShowFaultDiagnosisContent));
        OnPropertyChanged(nameof(IsMainTestNavSelected));
        OnPropertyChanged(nameof(IsReportManagementNavSelected));
        OnPropertyChanged(nameof(IsDeviceInspectionNavSelected));
        OnPropertyChanged(nameof(IsHardwareCalibrationNavSelected));
        OnPropertyChanged(nameof(IsUserManagementNavSelected));
        OnPropertyChanged(nameof(IsSystemLogNavSelected));
        OnPropertyChanged(nameof(IsFaultDiagnosisNavSelected));
        if (value == MainNavSection.FaultDiagnosis)
            FaultDiagnosis.RefreshFromMain();
        if (value == MainNavSection.SystemLog)
            SystemLog.Refresh();
    }

    public DeviceInspectionViewModel DeviceInspection { get; } = null!;
    public HardwareCalibrationViewModel HardwareCalibration { get; } = null!;
    public UserManagementViewModel UserManagement { get; }
    public TestReportManagementViewModel TestReportManagement { get; }
    public SystemLogViewModel SystemLog { get; }
    public FaultDiagnosisViewModel FaultDiagnosis { get; }

    public bool ShowMainTestContent => CurrentNavSection == MainNavSection.MainTest;
    public bool ShowReportManagementContent => CurrentNavSection == MainNavSection.ReportManagement;
    public bool ShowDeviceInspectionContent => CurrentNavSection == MainNavSection.DeviceInspection;
    public bool ShowHardwareCalibrationContent => CurrentNavSection == MainNavSection.HardwareCalibration;
    public bool ShowUserManagementContent => CurrentNavSection == MainNavSection.UserManagement;
    public bool ShowSystemLogContent => CurrentNavSection == MainNavSection.SystemLog;
    public bool ShowFaultDiagnosisContent => CurrentNavSection == MainNavSection.FaultDiagnosis;

    public bool IsMainTestNavSelected => CurrentNavSection == MainNavSection.MainTest;
    public bool IsReportManagementNavSelected => CurrentNavSection == MainNavSection.ReportManagement;
    public bool IsDeviceInspectionNavSelected => CurrentNavSection == MainNavSection.DeviceInspection;
    public bool IsHardwareCalibrationNavSelected => CurrentNavSection == MainNavSection.HardwareCalibration;
    public bool IsUserManagementNavSelected => CurrentNavSection == MainNavSection.UserManagement;
    public bool IsSystemLogNavSelected => CurrentNavSection == MainNavSection.SystemLog;
    public bool IsFaultDiagnosisNavSelected => CurrentNavSection == MainNavSection.FaultDiagnosis;

    // ============= 主界面右侧：工艺 / 摄像头 / 报表 =============
    [ObservableProperty] private MainTestRightPanelTab _selectedMainTestRightTab = MainTestRightPanelTab.Process;

    public bool IsProcessPanelTabSelected => SelectedMainTestRightTab == MainTestRightPanelTab.Process;
    public bool IsCameraOnePanelTabSelected => SelectedMainTestRightTab == MainTestRightPanelTab.CameraOne;
    public bool IsReportPanelTabSelected => SelectedMainTestRightTab == MainTestRightPanelTab.Report;

    partial void OnSelectedMainTestRightTabChanged(MainTestRightPanelTab value)
    {
        OnPropertyChanged(nameof(IsProcessPanelTabSelected));
        OnPropertyChanged(nameof(IsCameraOnePanelTabSelected));
        OnPropertyChanged(nameof(IsReportPanelTabSelected));
        
        if (value == MainTestRightPanelTab.Report)
        {
            RefreshReportData();
        }
    }

    [RelayCommand]
    private void SelectMainTestRightPanelTab(string? key)
    {
        var next = key switch
        {
            "Process" => MainTestRightPanelTab.Process,
            "CameraOne" => MainTestRightPanelTab.CameraOne,
            "Report" => MainTestRightPanelTab.Report,
            _ => SelectedMainTestRightTab
        };
        if (next == SelectedMainTestRightTab)
            return;
        SelectedMainTestRightTab = next;
        var label = next switch
        {
            MainTestRightPanelTab.Process => "主界面",
            MainTestRightPanelTab.CameraOne => "摄像头一",
            MainTestRightPanelTab.Report => "报表界面",
            _ => ""
        };
        AppendTestProcessLog($"切换「{label}」");
    }

    [RelayCommand]
    private void OpenCameraPreview()
        => AppendTestProcessLog("使用「开启摄像头」");

    [RelayCommand]
    private void ExitCameraPanel()
    {
        AppendTestProcessLog("使用「退出摄像头」");
        SelectedMainTestRightTab = MainTestRightPanelTab.Process;
    }

    // ============= 报表界面右侧栏（翻页 / 数字 / 保存打印）=============
    [ObservableProperty] private int _reportSidebarPageNumber = 5;

    private readonly IReportService _reportService;

    [ObservableProperty] private ObservableCollection<ObservableCollection<string>> _reportData = new();
    [ObservableProperty] private int _maxColumns = 0;

    [ObservableProperty] private string _reportProductModel = "";
    [ObservableProperty] private string _reportVehicleNumber = "";
    [ObservableProperty] private string _reportTesterName = "";
    [ObservableProperty] private string _reportTestTime = "";
    [ObservableProperty] private string _reportDeviceNumber = "";
    [ObservableProperty] private string _reportRemarks = "";

    private string _reportTargetPressure = "";
    public string ReportTargetPressure
    {
        get => _reportTargetPressure;
        set => SetProperty(ref _reportTargetPressure, value);
    }

    private string _reportActualDisplacement = "";
    public string ReportActualDisplacement
    {
        get => _reportActualDisplacement;
        set => SetProperty(ref _reportActualDisplacement, value);
    }

    private string _reportDisplacementOffset = "";
    public string ReportDisplacementOffset
    {
        get => _reportDisplacementOffset;
        set => SetProperty(ref _reportDisplacementOffset, value);
    }

    private string _reportDecelPressure = "";
    public string ReportDecelPressure
    {
        get => _reportDecelPressure;
        set => SetProperty(ref _reportDecelPressure, value);
    }

    private string _reportHighPressureLimit = "";
    public string ReportHighPressureLimit
    {
        get => _reportHighPressureLimit;
        set => SetProperty(ref _reportHighPressureLimit, value);
    }

    private string _reportReverseTime = "";
    public string ReportReverseTime
    {
        get => _reportReverseTime;
        set => SetProperty(ref _reportReverseTime, value);
    }

    [ObservableProperty] private string _reportTestResult = "";

    // 遗留字段（报表导出/数据库兼容）
    [ObservableProperty] private string _reportLoopOneWithstandVoltage = "";
    [ObservableProperty] private string _reportLoopOneLeakageCurrent = "";
    [ObservableProperty] private string _reportLoopOneTestResult = "";
    [ObservableProperty] private string _reportLoopTwoWithstandVoltage = "";
    [ObservableProperty] private string _reportLoopTwoLeakageCurrent = "";
    [ObservableProperty] private string _reportLoopTwoTestResult = "";
    [ObservableProperty] private string _reportLoopThreeWithstandVoltage = "";
    [ObservableProperty] private string _reportLoopThreeLeakageCurrent = "";
    [ObservableProperty] private string _reportLoopThreeTestResult = "";
    [ObservableProperty] private string _reportLoopFourWithstandVoltage = "";
    [ObservableProperty] private string _reportLoopFourLeakageCurrent = "";
    [ObservableProperty] private string _reportLoopFourTestResult = "";
    [ObservableProperty] private string _reportInsulationResistance = "";
    [ObservableProperty] private string _reportTurnWithstand = "";

    private void SnapshotBearingReportFromPlc()
    {
        ReportTargetPressure = PlcPubP00.ToString("F1");
        ReportActualDisplacement = PlcAiMai03.ToString("F1");
        ReportDisplacementOffset = PlcPubP04.ToString("F1");
        ReportDecelPressure = PlcPubP01.ToString("F1");
        ReportHighPressureLimit = PlcPubP14.ToString("F1");
        ReportReverseTime = PlcPubP15.ToString("F0");
    }

    [RelayCommand]
    private void ReportSidebarFlipUp()
    {
        ReportSidebarPageNumber = Math.Max(1, ReportSidebarPageNumber - 10);
        AppendTestProcessLog("报表：上翻");
    }

    [RelayCommand]
    private void ReportSidebarFlipDown()
    {
        ReportSidebarPageNumber = Math.Min(999, ReportSidebarPageNumber + 10);
        AppendTestProcessLog("报表：下翻");
    }

    [RelayCommand]
    private void ReportSidebarSpinnerUp()
        => ReportSidebarPageNumber = Math.Max(1, ReportSidebarPageNumber - 1);

    [RelayCommand]
    private void ReportSidebarSpinnerDown()
        => ReportSidebarPageNumber = Math.Min(999, ReportSidebarPageNumber + 1);

    [RelayCommand]
    private void ReportSidebarSave()
    {
        try
        {
            SnapshotBearingReportFromPlc();
            string exportPath = _reportService.ExportToFile(
                ReportProductModel,
                ReportVehicleNumber,
                ReportTesterName,
                ReportTestTime,
                ReportDeviceNumber,
                ReportRemarks,
                ReportTargetPressure,
                ReportActualDisplacement,
                ReportDisplacementOffset,
                ReportDecelPressure,
                ReportHighPressureLimit,
                ReportReverseTime,
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                ReportTestResult
            );
            
            AppendTestProcessLog($"报表已导出到: {exportPath}");

            try
            {
                AppendTestProcessLog("开始保存报表记录到数据库...");
                
                var record = new RecordEntity
                {
                    ProductModel = ReportProductModel,
                    VehicleNumber = ReportVehicleNumber,
                    TesterName = ReportTesterName,
                    TestTime = ReportTestTime,
                    DeviceNumber = ReportDeviceNumber,
                    Remarks = ReportRemarks,
                    LoopOneWithstandVoltage = ReportTargetPressure,
                    LoopOneLeakageCurrent = ReportActualDisplacement,
                    LoopOneTestResult = ReportDisplacementOffset,
                    LoopTwoWithstandVoltage = ReportDecelPressure,
                    LoopTwoLeakageCurrent = ReportHighPressureLimit,
                    LoopTwoTestResult = ReportReverseTime,
                    LoopThreeWithstandVoltage = ReportLoopThreeWithstandVoltage,
                    LoopThreeLeakageCurrent = ReportLoopThreeLeakageCurrent,
                    LoopThreeTestResult = ReportLoopThreeTestResult,
                    LoopFourWithstandVoltage = ReportLoopFourWithstandVoltage,
                    LoopFourLeakageCurrent = ReportLoopFourLeakageCurrent,
                    LoopFourTestResult = ReportLoopFourTestResult,
                    InsulationResistance = ReportInsulationResistance,
                    TurnWithstand = ReportTurnWithstand,
                    TestResult = ReportTestResult,
                    ReportPath = exportPath,
                    CreatedAt = DateTime.Now
                };

                AppendTestProcessLog($"数据: ProductModel={record.ProductModel}, VehicleNumber={record.VehicleNumber}, ReportPath={record.ReportPath}");

                try
                {
                    var rows = _kingbaseContext.Sql.Insert(record).ExecuteAffrows();
                    AppendTestProcessLog($"保存成功，影响行数: {rows}");
                }
                catch (Exception ex)
                {
                    AppendTestProcessLog($"插入失败: {ex.Message}");
                    throw;
                }
                
                AppendTestProcessLog("报表记录已保存到数据库");
            }
            catch (Exception ex)
            {
                AppendTestProcessLog($"保存报表记录到数据库失败: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            AppendTestProcessLog($"导出报表失败: {ex.Message}");
        }
    }
    //public Color XdpPlcStatusColor => XdpPlcIsConnected ? Color.FromRgb(46, 209, 88) : Color.FromRgb(128, 128, 128);

    //partial void OnXdpPlcIsConnectedChanged(bool value)
    //{
    //    OnPropertyChanged(nameof(XdpPlcStatusColor));
    //    OnPropertyChanged(nameof(XdpPlcStatusText));
    //}

    //public string XdpPlcStatusText => XdpPlcIsConnected ? "PLC 已连接" : "PLC 未连接";

    [RelayCommand]
    private void ReportSidebarPrint()
        => AppendTestProcessLog("使用「打印报表」");

    /// <summary>任务区（下载/查看/上传/绝缘）打开模态窗时，主窗是否显示半透明遮罩。</summary>
    [ObservableProperty] private bool _isTaskModalOverlayVisible;

    /// <summary>模拟量大数字编辑弹窗打开时主窗遮罩。</summary>
    [ObservableProperty] private bool _isAnalogEditOverlayVisible;

    /// <summary>任一模态类操作（任务窗 / 数值编辑）时主窗暗化。</summary>
    public bool IsAnyMainDimOverlay => IsTaskModalOverlayVisible || IsAnalogEditOverlayVisible;

    partial void OnIsTaskModalOverlayVisibleChanged(bool value)
        => OnPropertyChanged(nameof(IsAnyMainDimOverlay));

    partial void OnIsAnalogEditOverlayVisibleChanged(bool value)
        => OnPropertyChanged(nameof(IsAnyMainDimOverlay));

    // ============= 用户与时钟 =============
    [ObservableProperty] private string _userName = VarHelper.CurrentUserName ?? "";
    [ObservableProperty] private string _userId   = VarHelper.CurrentUserId ?? "";
    [ObservableProperty] private string _avatarLetter = "?";
    [ObservableProperty] private string _clockText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    // ============= 工作模式 =============
    [ObservableProperty] private WorkMode _workMode = VarHelper.CurrentWorkMode;

    public string ModeHint => WorkMode switch
    {
        WorkMode.Online      => "在线模式：车型/车号/工件编号只读（任务映射）；操作员可编辑",
        WorkMode.Offline     => "离线模式：可手动编辑信息录入（车型/车号/工件编号/操作员）",
        WorkMode.ExcelImport => "已加载 Excel 模板 · 仅需填写电压",
        _ => ""
    };

    public bool IsOnlineMode      => WorkMode == WorkMode.Online;
    public bool IsOfflineMode     => WorkMode == WorkMode.Offline;
    public bool IsExcelImportMode => WorkMode == WorkMode.ExcelImport;

    /// <summary>仅离线模式允许编辑「信息录入」「参数设定」</summary>
    public bool CanEditInfoAndParameters => IsOfflineMode;

    /// <summary>是否显示 Excel 横幅</summary>
    public bool ShowExcelBanner => IsExcelImportMode && !string.IsNullOrEmpty(ExcelFileName);

    // ============= 顶部实时数据 =============
    [ObservableProperty] private double _voltageKv;
    [ObservableProperty] private double _currentMa;
    [ObservableProperty] private int    _remainSeconds = 0;
    [ObservableProperty] private int    _totalSeconds = 0;

    /// <summary>左栏摘要：实时电压 / 电流 / 剩余时间一行展示。</summary>
    public string LiveMetricsLine => $"{HighVoltageOutput:F2} kV · {CurrentMa:F2} mA · {RemainSeconds} s";

    partial void OnVoltageKvChanged(double value) => OnPropertyChanged(nameof(LiveMetricsLine));

    partial void OnCurrentMaChanged(double value) => OnPropertyChanged(nameof(LiveMetricsLine));

    partial void OnRemainSecondsChanged(int value) => OnPropertyChanged(nameof(LiveMetricsLine));
    [ObservableProperty] private TestResult _status = TestResult.Pending;

    /// <summary>「混合模拟」区内展示文案（仪表与传感器模拟量说明等）。</summary>
    [ObservableProperty] private string _voiceBroadcastText = "仪表与传感器采集模拟量";

    partial void OnVoiceBroadcastTextChanged(string value)
    {
        PlayNotificationSound();
    }

    private void PlayNotificationSound()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                System.Media.SystemSounds.Beep.Play();
            }
            else if (OperatingSystem.IsLinux())
            {
                PlayLinuxBeep();
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "播放提示音失败");
        }
    }

    private void PlayLinuxBeep()
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "paplay";
            process.StartInfo.Arguments = "/usr/share/sounds/freedesktop/stereo/bell.oga";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            process.WaitForExit(1000);
        }
        catch
        {
            try
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "aplay";
                process.StartInfo.Arguments = "/usr/share/sounds/alsa/Front_Center.wav";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                process.WaitForExit(1000);
            }
            catch
            {
                // Console.Beep(frequency, duration) 仅 Windows；Linux 上会抛 PlatformNotSupportedException
            }
        }
    }

    public string StatusText => Status switch
    {
        TestResult.Pending    => "待启动",
        TestResult.Running    => "试验中",
        TestResult.Passed     => "通过",
        TestResult.Breakdown  => "击穿",
        TestResult.Stopped    => "已停止",
        TestResult.Error      => "异常",
        _ => "未知"
    };

    // ============= 共用：手动控制（三模式都有）=============
    [ObservableProperty] private double _manualSetVoltageKv = 10.0;
    [ObservableProperty] private int    _manualDurationSeconds = 60;
    [ObservableProperty] private double _manualProtectMa = 500;

    /// <summary>主界面左栏「参数设定」当前选中的回路（1～4）。</summary>
    [ObservableProperty] private int _selectedParameterLoop = 1;

    /// <summary>参数设定区各回路预设显示（与参考图每格输入框对应）。</summary>
    [ObservableProperty] private string _loopPresetText1 = "1";
    [ObservableProperty] private string _loopPresetText2 = "2";
    [ObservableProperty] private string _loopPresetText3 = "3";
    [ObservableProperty] private string _loopPresetText4 = "4";

    /// <summary>各回路电流设定（mA）</summary>
    [ObservableProperty] private string _loopCurrentText1 = "100";

    [ObservableProperty] private ObservableCollection<string> _parameterSectionNames = [];
    [ObservableProperty] private string _selectedParameterSection = "";
    [ObservableProperty] private string _loopCurrentText2 = "200";
    [ObservableProperty] private string _loopCurrentText3 = "300";
    [ObservableProperty] private string _loopCurrentText4 = "400";

    partial void OnSelectedParameterLoopChanged(int value)
    {
        OnPropertyChanged(nameof(IsParameterLoop1));
        OnPropertyChanged(nameof(IsParameterLoop2));
        OnPropertyChanged(nameof(IsParameterLoop3));
        OnPropertyChanged(nameof(IsParameterLoop4));
        if (CanEditInfoAndParameters)
            AppendTestProcessLog($"修改参数「选用回路」为 回路{value}");
    }

    public void LoadParameterSectionNames()
    {
        try
        {
            // 清理旧节（测试1/车型x 等），下拉只保留「运行」及新格式工艺节
            ParaIniFile.RemoveLegacyProcessSections();

            ParameterSectionNames.Clear();
            foreach (var key in ParaIniFile.GetProcessParameterSectionNames())
            {
                if (!string.IsNullOrWhiteSpace(key))
                    ParameterSectionNames.Add(key);
            }

            if (string.IsNullOrWhiteSpace(SelectedParameterSection) ||
                !ParameterSectionNames.Contains(SelectedParameterSection))
                SelectedParameterSection = ParaIniFile.RunSectionName;
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "加载参数章节名称失败");
        }
    }

    partial void OnSelectedParameterSectionChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        // 工艺「参数」下拉：只加载 Para.ini 里参数管理保存的节，不绑定/改写车型
        LoadParameterSectionToPlc(value);
    }

    private void LoadParameterSectionToPlc(string sectionName)
    {
        try
        {
            var iniPath = ParaIniFile.ResolvePath();
            if (!File.Exists(iniPath))
            {
                AppendTestProcessLog($"参数配置文件不存在：{iniPath}");
                return;
            }

            if (ParaIniFile.TryLoadSection(sectionName, out var para, out _, iniPath))
            {
                ApplyLoopPresetsFromPara(para);
                WriteParametersToPlc(para);
                AppendTestProcessLog($"已加载参数章节「{sectionName}」并下发至PLC");
            }
            else
            {
                AppendTestProcessLog($"参数章节「{sectionName}」不存在");
            }
        }
        catch (Exception ex)
        {
            AppendTestProcessLog($"加载参数章节失败：{ex.Message}");
            NlogHelper.Error(ex, "加载参数章节失败");
        }
    }

    private void WriteParametersToPlc(ParaIniParameters para)
    {
        if (double.TryParse(para.TargetPressure, out var targetPressure))
            PlcPubP00 = targetPressure;
        WritePlcTag("PUB.P00", PlcPubP00);

        if (double.TryParse(para.DisplacementOffset, out var displacementOffset))
            PlcPubP04 = displacementOffset;
        WritePlcTag("PUB.P04", PlcPubP04);

        if (double.TryParse(para.DecelerationThreshold, out var decelPressure))
            PlcPubP01 = decelPressure;
        WritePlcTag("PUB.P01", PlcPubP01);

        if (double.TryParse(para.HighPressureFloatLimit, out var highPressureFloatLimit))
            PlcPubP14 = highPressureFloatLimit;
        WritePlcTag("PUB.P14", PlcPubP14);

        if (double.TryParse(para.DelayReliefTime, out var reverseTime))
            PlcPubP15 = reverseTime;
        WritePlcTag("PUB.P15", (ushort)Math.Clamp((int)Math.Round(PlcPubP15), 0, 65535));

        var fast = string.Equals(para.FastSlowSetting?.Trim(), "1", StringComparison.Ordinal);
        PlcDoCdo02 = fast;
        WritePlcTag("DO.CDO02", fast);
    }

    /// <summary>将 Para.ini 参数同步到主界面「工艺参数设定」</summary>
    public void ApplyLoopPresetsFromPara(ParaIniParameters para)
    {
        if (double.TryParse(para.TargetPressure, out var targetPressure))
            PlcPubP00 = targetPressure;

        if (double.TryParse(para.DisplacementOffset, out var displacementOffset))
            PlcPubP04 = displacementOffset;

        if (double.TryParse(para.DecelerationThreshold, out var decelPressure))
            PlcPubP01 = decelPressure;

        if (double.TryParse(para.HighPressureFloatLimit, out var highPressureFloatLimit))
            PlcPubP14 = highPressureFloatLimit;

        if (double.TryParse(para.DelayReliefTime, out var reverseTime))
            PlcPubP15 = reverseTime;

        if (!string.IsNullOrWhiteSpace(para.FastSlowSetting))
            PlcDoCdo02 = string.Equals(para.FastSlowSetting.Trim(), "1", StringComparison.Ordinal);

        AppendTestProcessLog("已同步工艺参数设定");
    }

    /// <summary>急停或液位极限报警时禁止手动控制。</summary>
    public bool IsProcessControlEnabled => !PlcDiMdi16 && !PlcDiMdi11;

    private void RefreshProcessControlEnabled()
    {
        OnPropertyChanged(nameof(IsProcessControlEnabled));
    }

    partial void OnPlcDiMdi11Changed(bool value)
    {
        RefreshProcessControlEnabled();
        if (value)
        {
            AppendTestProcessLog("液位极限报警，已锁定手动控制");
            BroadcastOnce("液位极限报警，已锁定手动控制");
            ShowSafetyAlarm("液位极限报警！请立即停机检查油箱液位！");
        }
        else
        {
            ClearSafetyAlarm();
        }
    }

    partial void OnPlcDiMdi16Changed(bool value)
    {
        RefreshProcessControlEnabled();
        if (value)
        {
            AppendTestProcessLog("急停报警，已锁定手动控制");
            BroadcastOnce("急停报警，已锁定手动控制");
            ShowSafetyAlarm("急停报警！请立即检查现场并复位！");
        }
        else
        {
            ClearSafetyAlarm();
        }
    }
    // <summary>目标压力进度条颜色（绿/黄/红 三档警示）。</summary>
    public IBrush PlcPubP00Brush
    {
        get
        {
            double val = Math.Clamp(PlcPubP00, 0, 30);
            if (val >= 27) return Brushes.Red;
            if (val >= 20) return Brushes.Yellow;
            return Brushes.LimeGreen;
        }
    }

    /// <summary>目标压力进度条取值（0~30MPa）。</summary>
    public double TargetPressureProgressValue => Math.Clamp(PlcPubP00, 0, 30);

    /// <summary>目标压力进度条填充宽度（像素）。</summary>
    public double TargetPressureBarWidth => TargetPressureProgressValue / 30.0 * GaugeTrackWidthPx;

    /// <summary>实际位移进度条取值（行程 0~400mm）。</summary>
    public double ActualDisplacementProgressValue => Math.Clamp(PlcAiMai03, 0, 400);

    /// <summary>实际位移进度条填充宽度（像素）。</summary>
    public double ActualDisplacementBarWidth => ActualDisplacementProgressValue / 400.0 * GaugeTrackWidthPx;

    /// <summary>实际位移进度条颜色（绿/黄/红 三档，接近行程上限警示）。</summary>
    public IBrush PlcAiMai03Brush
    {
        get
        {
            double val = Math.Clamp(PlcAiMai03, 0, 400);
            if (val >= 360) return Brushes.Red;
            if (val >= 280) return Brushes.Yellow;
            return Brushes.LimeGreen;
        }
    }

    private const double GaugeTrackWidthPx = 150;

    private void RefreshTargetPressureGauge()
    {
        OnPropertyChanged(nameof(PlcPubP00Brush));
        OnPropertyChanged(nameof(TargetPressureProgressValue));
        OnPropertyChanged(nameof(TargetPressureBarWidth));
    }

    private void RefreshDisplacementGauge()
    {
        OnPropertyChanged(nameof(PlcAiMai03Brush));
        OnPropertyChanged(nameof(ActualDisplacementProgressValue));
        OnPropertyChanged(nameof(ActualDisplacementBarWidth));
    }

    partial void OnPlcAiMai03Changed(double value)
    {
        RefreshDisplacementGauge();
        if (value > 400)
        { 
            AppendTestProcessLog($"⚠ 实际位移越限：{value:F1} mm > 400 mm，已钳位到上限");
            BroadcastOnce("实际位移超过上限400毫米，已自动限制");
        }
    }

    partial void OnPlcPubP00Changed(double value)
    {
        RefreshTargetPressureGauge();
        if (_isUpdatingFromPlc) return;
        WritePlcTag("PUB.P00", value);
        // 越限校验
        if (value > 30)
        {
            AppendTestProcessLog($"⚠ 目标压力越限：{value:F1} MPa > 30 MPa，已钳位到上限");
            PlcPubP00 = 30;// 钳位到上限
            BroadcastOnce("目标压力超过上限30兆帕，已自动限制");
        }
    }

    partial void OnPlcPubP04Changed(double value)
    {
        if (_isUpdatingFromPlc) return;
        WritePlcTag("PUB.P04", value);
    }

    partial void OnPlcPubP01Changed(double value)
    {
        if (_isUpdatingFromPlc) return;
        WritePlcTag("PUB.P01", value);
    }

    partial void OnPlcPubP14Changed(double value)
    {
        if (_isUpdatingFromPlc) return;
        WritePlcTag("PUB.P14", value);
    }

    partial void OnPlcPubP15Changed(double value)
    {
        if (_isUpdatingFromPlc) return;
        WritePlcTag("PUB.P15", (ushort)Math.Clamp((int)Math.Round(value), 0, 65535));
    }

    public bool IsParameterLoop1
    {
        get => SelectedParameterLoop == 1;
        set { if (value) SelectedParameterLoop = 1; }
    }

    public bool IsParameterLoop2
    {
        get => SelectedParameterLoop == 2;
        set { if (value) SelectedParameterLoop = 2; }
    }

    public bool IsParameterLoop3
    {
        get => SelectedParameterLoop == 3;
        set { if (value) SelectedParameterLoop = 3; }
    }

    public bool IsParameterLoop4
    {
        get => SelectedParameterLoop == 4;
        set { if (value) SelectedParameterLoop = 4; }
    }

    /// <summary>"根据轴承型号从参数配置读取参数"。</summary>
    public void LoadParametersByBearingType(string carType)
    {
        if (string.IsNullOrEmpty(carType)) return;

        try
        {
            var iniPath = ParaIniFile.ResolvePath();
            if (!File.Exists(iniPath))
            {
                AppendTestProcessLog($"参数配置文件不存在：{iniPath}");
                return;
            }

            if (ParaIniFile.TryLoadSection(carType, out var para, out var section, iniPath))
            {
                ApplyLoopPresetsFromPara(para);
                AppendTestProcessLog($"已从参数配置加载车型「{carType}」的参数");
            }
            else
            {
                AppendTestProcessLog($"车型「{carType}」的参数配置不存在");
            }
        }
        catch (Exception ex)
        {
            AppendTestProcessLog($"加载参数配置失败：{ex.Message}");
        }
    }

    /// <summary>根据当前选中的回路获取电压、电流参数</summary>
    /// <returns>(电压V, 电流mA)</returns>
    private (double VoltageV, double CurrentMa) GetCurrentLoopParameters()
    {
        string voltageText = SelectedParameterLoop switch
        {
            1 => LoopPresetText1,
            2 => LoopPresetText2,
            3 => LoopPresetText3,
            4 => LoopPresetText4,
            _ => LoopPresetText1
        };

        string currentText = SelectedParameterLoop switch
        {
            1 => LoopCurrentText1,
            2 => LoopCurrentText2,
            3 => LoopCurrentText3,
            4 => LoopCurrentText4,
            _ => LoopCurrentText1
        };

        double voltageV = double.TryParse(voltageText, out var v) ? v : 0;
        double currentMa = double.TryParse(currentText, out var c) ? c : 0;

        return (voltageV, currentMa);
    }

    /// <summary>底栏：仿真模式提示（离线 / Excel 导入时显示，PLC连接成功后隐藏）。</summary>
    public bool ShowSimulationStatus => (IsOfflineMode || IsExcelImportMode) && !PlcConnected;

    /// <summary>底栏：在线且 API 未连时显示网络异常。</summary>
    public bool ShowNetworkErrorStatus => IsOnlineMode && !ApiConnected;

    public string StatusPlcText => PlcConnected ? "PLC连接正常" : "PLC未连接";
    /// <summary>工艺区顶部：PLC 状态指示灯颜色。</summary>
    public SolidColorBrush PlcStatusBrush=> PlcConnected ? new SolidColorBrush(Color.FromRgb(46, 209, 88)): new SolidColorBrush(Color.FromRgb(128, 128, 128));
    // <summary>工艺区顶部：PLC 状态文字。</summary>
    public string PlcStatusTextForPanel => PlcConnected ? "PLC 已连接" : "PLC 未连接";


    [ObservableProperty] private string _currentProductName = "未选择";
    [ObservableProperty] private string _currentProductCode = "";

    // ============= 在线 + Excel 模式：任务明细 =============
    public ObservableCollection<TaskDetailItem> TaskDetails { get; } = new();

    /// <summary>试验信息过程日志（任务明细下方列表）。</summary>
    public ObservableCollection<string> TestProcessLogLines { get; } = new();

    /// <summary>向「试验信息过程」追加一行，格式与界面一致：<c>HH:mm:ss: 说明</c>。</summary>
    public void AppendTestProcessLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}: {message}";
        if (Dispatcher.UIThread.CheckAccess())
            TestProcessLogLines.Add(line);
        else
            Dispatcher.UIThread.Post(() => TestProcessLogLines.Add(line));
    }

    [ObservableProperty] private int _currentDetailIndex = 0;
    public int TotalDetails => TaskDetails.Count;
    public TaskDetailItem? CurrentDetail =>
        TaskDetails.Count > 0 && CurrentDetailIndex < TaskDetails.Count
            ? TaskDetails[CurrentDetailIndex] : null;
    public string CurrentDetailDisplay =>
        TaskDetails.Count > 0 ? $"第 {CurrentDetailIndex + 1} / {TaskDetails.Count} 条" : "无任务";

    public bool HasPrevious => CurrentDetailIndex > 0;
    public bool HasNext => CurrentDetailIndex < TaskDetails.Count - 1;

    // Excel 导入横幅
    [ObservableProperty] private string? _excelFileName;
    [ObservableProperty] private int _excelTaskCount;

    // ============= 当前任务信息 =============
    [ObservableProperty] private string _currentTaskNumber = "—";
    [ObservableProperty] private string _currentTaskTitle  = "未加载任务";
    [ObservableProperty] private string _currentCarType    = "—";
    [ObservableProperty] private string _currentTrainCar   = "—";
    [ObservableProperty] private string _assistantInfo     = "未指定";

    /// <summary>主界面左侧「信息录入」：车型</summary>
    [ObservableProperty] private string _infoEntryBearingTypeText = "";

    /// <summary>主界面左侧「信息录入」：车号</summary>
    [ObservableProperty] private string _infoEntryTrainCarText = "";

    /// <summary>主界面左侧「信息录入」：工件编号</summary>
    [ObservableProperty] private string _infoEntryWorkpieceCodeText = "";

    // ============= 环境数据 =============
    [ObservableProperty] private double _envTempC = 23.5;
    [ObservableProperty] private double _envHumidity = 48;
    
    // ============= 检测数据 =============
    [ObservableProperty] private double _highVoltageOutput = 0.0;

    // ============= 串口仪器 =============
    [ObservableProperty] private bool _instrument1Connected = true;
    [ObservableProperty] private string _instrument1Data = "10.25 kV · 0.42 mA";
    [ObservableProperty] private bool _instrument2Connected = true;
    [ObservableProperty] private string _instrument2Data = "23.5 ℃ · 48 %";

    // ============= PLC AI 模拟量（从 ALL_PLC_XDP.csv 映射）=============
    [ObservableProperty] private double _plcAiMai00;      // 径向泵_高压实际压力
    [ObservableProperty] private double _plcAiMai01;      // 径向泵_低压实际压力
    [ObservableProperty] private double _plcAiMai02;      //径向泵实际温度
    [ObservableProperty] private double _plcAiMai03;      // 径向泵实际液位
    [ObservableProperty] private double _plcAiMai04;      // 电压Uab
    [ObservableProperty] private double _plcAiMai05;      // 电压Ubc
    [ObservableProperty] private double _plcAiMai06;      //电压Uca
    [ObservableProperty] private double _plcAiMai07;      // 电流Ia
    [ObservableProperty] private double _plcAiMai08;      // 电流Ib
    [ObservableProperty] private double _plcAiMai09;      // 电流Ic
    [ObservableProperty] private double _plcAiMai10;      //合相有功功率
    [ObservableProperty] private double _plcAiMai11;      // 合相无功功率
    [ObservableProperty] private double _plcAiMai12;      // 合相功率因数
    [ObservableProperty] private double _plcAiMai13;      // 频率
    [ObservableProperty] private double _plcAiMai14;      //正向有功电能
    [ObservableProperty] private double _plcAiMai15;      // 反向有功电能
    [ObservableProperty] private double _plcAiMai16;      // 径向泵电机转速

    //[ObservableProperty] private double _plcAiGain00;     // VD400 增益1
    //[ObservableProperty] private double _plcAiGain01;     // VD404 增益2
    //[ObservableProperty] private double _plcAiGain02;     // VD408 增益3
    //[ObservableProperty] private double _plcAiGain03;     // VD862 增益4

    //[ObservableProperty] private double _plcAiZero00;     // VD300 零点1
    //[ObservableProperty] private double _plcAiZero01;     // VD304 零点2
    //[ObservableProperty] private double _plcAiZero02;     // VD308 零点3
    //[ObservableProperty] private double _plcAiZero03;     // VD858 零点4

    // ============= PLC DI 数字量输入（从 ALL_PLC_XDP.csv 映射）=============
    [ObservableProperty] private bool _plcDiMdi00;        // 人脸识别成功NO
    [ObservableProperty] private bool _plcDiMdi01;        // 恢复默认参数
    [ObservableProperty] private bool _plcDiMdi02;        // 加热与搅拌显示
    [ObservableProperty] private bool _plcDiMdi03;        // 径向泵待机状态
    [ObservableProperty] private bool _plcDiMdi04;        // 径向泵加压状态
    [ObservableProperty] private bool _plcDiMdi05;        // 径向泵泄压状态
    [ObservableProperty] private bool _plcDiMdi06;        // 径向泵保压状态
    [ObservableProperty] private bool _plcDiMdi07;        // 径向泵异常状态
    [ObservableProperty] private bool _plcDiMdi08;        // 当前在登录页面中
    [ObservableProperty] private bool _plcDiMdi09;        // 当前在运行页面中
    [ObservableProperty] private bool _plcDiMdi10;        // 径向泵液位下限报警
    [ObservableProperty] private bool _plcDiMdi11;        // 径向泵液位极限报警
    [ObservableProperty] private bool _plcDiMdi12;        // 径向泵液位上限报警
    [ObservableProperty] private bool _plcDiMdi13;        // 径向泵温度报警
    [ObservableProperty] private bool _plcDiMdi14;        // 径向伺服报警
    [ObservableProperty] private bool _plcDiMdi15;        //径向泵过滤报警
    [ObservableProperty] private bool _plcDiMdi16;        // 急停报警
    [ObservableProperty] private bool _plcDiMdi17;        // 径向泵温液传感器掉线报警
    [ObservableProperty] private bool _plcDiMdi18;        // 径向泵低压传感器掉线报警
    [ObservableProperty] private bool _plcDiMdi19;        // 径向泵高压传感器掉线报警
    [ObservableProperty] private bool _plcDiMdi20;        // 总报警信号


    // ============= PLC DO 数字量输出（从 ALL_PLC_XDP.csv 映射）=============
    [ObservableProperty] private bool _plcDoCdo00;        // 径向泵持续泄压模式
    [ObservableProperty] private bool _plcDoCdo01;        // 径向泵点动泄压模式
    [ObservableProperty] private bool _plcDoCdo02;        // 径向泵点动加压模式
    [ObservableProperty] private bool _plcDoCdo03;        // 径向泵持续加压模式

   
    // ============= PLC PUB 公共变量（参数设定）（从 ALL_PLC_XDP.csv 映射）=============
    [ObservableProperty] private double _plcPubP00;        // 径向泵_目标压力
    [ObservableProperty] private double _plcPubP01;        // 径向泵_距目标多少减速
    [ObservableProperty] private double _plcPubP02;        // 径向泵_minSpeed
    [ObservableProperty] private double _plcPubP03;        // 径向泵_maxSpeed
    [ObservableProperty] private double _plcPubP04;        // 径向泵液位补充值
    [ObservableProperty] private double _plcPubP05;        // 径向泵泄压速度
    [ObservableProperty] private double _plcPubP06;        // 径向泵液位报警下限
    [ObservableProperty] private double _plcPubP07;        // 径向泵液位报警极限
    [ObservableProperty] private double _plcPubP08;        // 径向泵液位报警上限
    [ObservableProperty] private double _plcPubP09;        // 径向泵停止加热温度
    [ObservableProperty] private double _plcPubP10;        // 径向泵开始加热温度
    [ObservableProperty] private double _plcPubP11;        // 径向泵报警温度
    [ObservableProperty] private double _plcPubP12;        // 径向泵低压设定压力
    [ObservableProperty] private double _plcPubP13;        // 径向泵温度补充值
    [ObservableProperty] private double _plcPubP14;        // 高压浮动限制
    [ObservableProperty] private double _plcPubP15;        // 延时泄压时间S
    [ObservableProperty] private double _plcPubP16;        // 高压限制显示压力

    //[ObservableProperty] private double _plcPubP63;        // VD526 试验时间
    //[ObservableProperty] private double _plcPubP64;        // VD700 结束电压结果
    //[ObservableProperty] private double _plcPubP65;        // VD750 结束泄漏电流结果

    // ============= PLC PUB DWord 类型变量（设定次数）=============
    //[ObservableProperty] private double _plcPubP09;        // VD1100 设定急停次数
    //[ObservableProperty] private double _plcPubP10;        // VD1104 设定接地报警次数
    //[ObservableProperty] private double _plcPubP11;        // VD1108 设定电铃次数
    //[ObservableProperty] private double _plcPubP12;        // VD1112 设定门联锁次数
    //[ObservableProperty] private double _plcPubP13;        // VD1116 设定调压低限次数
    //[ObservableProperty] private double _plcPubP14;        // VD1120 设定调压高限次数
    //[ObservableProperty] private double _plcPubP15;        // VD1124 设定电机报警次数
    //[ObservableProperty] private double _plcPubP16;        // VD1128 设定主接触器次数
    //[ObservableProperty] private double _plcPubP17;        // VD1132 设定从接触器次数
    //[ObservableProperty] private double _plcPubP18;        // VD1136 设定放电次数
    //[ObservableProperty] private double _plcPubP19;        // VD1140 设定电动门锁次数
    //[ObservableProperty] private double _plcPubP20;        // VD1144 设定启动按钮次数
    //[ObservableProperty] private double _plcPubP21;        // VD1148 设定升压按钮次数
    //[ObservableProperty] private double _plcPubP22;        // VD1152 设定降压按钮次数
    //[ObservableProperty] private double _plcPubP23;        // VD1156 设定停止按钮次数
    //[ObservableProperty] private double _plcPubP24;        // VD1160 设定复位按钮次数
    //[ObservableProperty] private double _plcPubP25;        // VD1164 设定计时按钮次数
    //[ObservableProperty] private double _plcPubP26;        // VD1168 设定计数器次数

    // ============= PLC PUB 语音提醒变量 =============
    //[ObservableProperty] private bool _plcPubP45;        // V900.0 试验通过---语音提醒
    //[ObservableProperty] private bool _plcPubP46;        // V900.1 试验失败---语音提醒
    //[ObservableProperty] private bool _plcPubP47;        // V900.2 高压输出已合闸，注意安全---语音提醒
    //[ObservableProperty] private bool _plcPubP48;        // V900.3 电机复位中_（持续提醒---语音提醒
    //[ObservableProperty] private bool _plcPubP49;        // V900.4 已到达零位---语音提醒
    //[ObservableProperty] private bool _plcPubP50;        // V900.5 急停已按下，请注意---语音提醒
    //[ObservableProperty] private bool _plcPubP51;        // V900.6 降压放电中_（持续提醒---语音提醒
    //[ObservableProperty] private bool _plcPubP52;        // V900.7 接地保护失效---语音提醒
    //[ObservableProperty] private bool _plcPubP53;        // V901.0 升压器过流保护---语音提醒
    //[ObservableProperty] private bool _plcPubP54;        // V901.1 放电已完成---语音提醒
    //[ObservableProperty] private bool _plcPubP55;        // V901.2 启动试验---语音提醒
    //[ObservableProperty] private bool _plcPubP56;        // V901.3 电压传感器损坏---语音提醒
    //[ObservableProperty] private bool _plcPubP57;        // V901.4 门限位失效，请注意---语音提醒
    //[ObservableProperty] private bool _plcPubP58;        // V901.5 未到达设置电压---语音提醒
    //[ObservableProperty] private bool _plcPubP59;        // V901.6 仪表与传感器采值误差偏---语音提醒
    //[ObservableProperty] private bool _plcPubP60;        // V901.7 伺服过流报警---语音提醒
    //[ObservableProperty] private bool _plcPubP61;        // V10.3 试验开始
    //[ObservableProperty] private bool _plcPubP62;        // V10.0 手动自动


    // ============= PLC PUB DWord 类型变量（实际次数）=============
    //[ObservableProperty] private double _plcPubP66;        // VD1000 急停次数
    //[ObservableProperty] private double _plcPubP67;        // VD1004 接地报警次数
    //[ObservableProperty] private double _plcPubP68;        // VD1008 电铃次数
    //[ObservableProperty] private double _plcPubP69;        // VD1012 门联锁次数
    //[ObservableProperty] private double _plcPubP70;        // VD1016 调压低限次数
    //[ObservableProperty] private double _plcPubP71;        // VD1020 调压高限次数
    //[ObservableProperty] private double _plcPubP72;        // VD1024 电机报警次数
    //[ObservableProperty] private double _plcPubP73;        // VD1028 主接触器次数
    //[ObservableProperty] private double _plcPubP74;        // VD1032 从接触器次数
    //[ObservableProperty] private double _plcPubP75;        // VD1036 放电次数
    //[ObservableProperty] private double _plcPubP76;        // VD1040 电动门锁次数
    //[ObservableProperty] private double _plcPubP77;        // VD1044 启动按钮次数
    //[ObservableProperty] private double _plcPubP78;        // VD1048 升压按钮次数
    //[ObservableProperty] private double _plcPubP79;        // VD1052 降压按钮次数
    //[ObservableProperty] private double _plcPubP80;        // VD1056 停止按钮次数
    //[ObservableProperty] private double _plcPubP81;        // VD1060 复位按钮次数
    //[ObservableProperty] private double _plcPubP82;        // VD1064 计时按钮次数
    //[ObservableProperty] private double _plcPubP83;        // VD1068 计数器次数
    //[ObservableProperty] private bool _plcPubP84;          // V2.3 试验停止信号


    // ============= PLC PUB 寿命到达标志 =============
    //[ObservableProperty] private bool _plcPubP27;        // V910.0 急停寿命到达
    //[ObservableProperty] private bool _plcPubP28;        // V910.1 接地报警寿命到达
    //[ObservableProperty] private bool _plcPubP29;        // V910.2 电铃寿命到达
    //[ObservableProperty] private bool _plcPubP30;        // V910.3 门联锁寿命到达
    //[ObservableProperty] private bool _plcPubP31;        // V910.4 调压低限寿命到达
    //[ObservableProperty] private bool _plcPubP32;        // V910.5 调压高限寿命到达
    //[ObservableProperty] private bool _plcPubP33;        // V910.6 调压电机报警次数到达
    //[ObservableProperty] private bool _plcPubP34;        // V910.7 主接触器寿命到达
    //[ObservableProperty] private bool _plcPubP35;        // V911.0 从接触器寿命到达
    //[ObservableProperty] private bool _plcPubP36;        // V911.1 放电继电器寿命到达
    //[ObservableProperty] private bool _plcPubP37;        // V911.2 电动门锁寿命到达
    //[ObservableProperty] private bool _plcPubP38;        // V911.3 启动按钮寿命到达
    //[ObservableProperty] private bool _plcPubP39;        // V911.4 升压按钮寿命到达
    //[ObservableProperty] private bool _plcPubP40;        // V911.5 降压按钮寿命到达
    //[ObservableProperty] private bool _plcPubP41;        // V911.6 停止按钮寿命到达
    //[ObservableProperty] private bool _plcPubP42;        // V911.7 复位按钮寿命到达
    //[ObservableProperty] private bool _plcPubP43;        // V912.0 计时按钮寿命到达
    //[ObservableProperty] private bool _plcPubP44;        // V912.1 计数器寿命到达

    // ============= 检测信号灯（第一排）=============
    [ObservableProperty] private Color _signalColor_DI00 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI01 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI02 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI03 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI04 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI05 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI06 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI07 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI08 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI09 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI10 = Colors.Gray;

    // ============= 检测信号灯（第二排）=============
    [ObservableProperty] private Color _signalColor_DI11 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI12 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI13 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI14 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI15 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI16 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI17 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI18 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI19 = Colors.Gray;
    [ObservableProperty] private Color _signalColor_DI20 = Colors.Gray;

    // ============= 状态栏 =============
    [ObservableProperty] private bool _plcConnected;
    [ObservableProperty] private bool _dbConnected = true;
    [ObservableProperty] private bool _apiConnected;

    // ============= 耐压曲线（LiveCharts 双 Y 轴）=============
    /// <summary>电压曲线点（X：时间 s，Y：kV），试验引擎接入后可继续追加。</summary>
    public ObservableCollection<ObservablePoint> WithstandVoltagePoints { get; } = new();

    /// <summary>泄漏电流曲线点（X：时间 s，Y：mA）。</summary>
    public ObservableCollection<ObservablePoint> WithstandCurrentPoints { get; } = new();

    public ISeries[] WithstandCurveSeries { get; }

    public Axis[] WithstandCurveXAxes { get; }

    public Axis[] WithstandCurveYAxes { get; }

    private System.Timers.Timer? _curveDataTimer;
    private DateTime _curveStartTime;
    private bool _isCollectingCurveData;
    private Axis? _withstandCurveXAxis;
    private const double AutoPanWindowSeconds = 30;
    public bool IsCollectingCurveData
    {
        get => _isCollectingCurveData;
        set
        {
            if (SetProperty(ref _isCollectingCurveData, value))
            {
                if (value)
                    StartCurveCollection();
                else
                    StopCurveCollection();
            }
        }
    }
    /// <summary>当前速度模式显示：true=快速（M420=1），false=慢速。</summary>
    private bool _currentSpeedModeIsFast;
    public bool CurrentSpeedModeIsFast
    {
        get => _currentSpeedModeIsFast;
        set
        {
            if (SetProperty(ref _currentSpeedModeIsFast, value))
            {
                OnPropertyChanged(nameof(SpeedModeButtonText));
            }
        }
    }
    /// <summary>快慢速按钮文案，根据当前速度模式动态显示。</summary>
    public string  SpeedModeButtonText => CurrentSpeedModeIsFast ? "当前：快速（点击切慢速）" : "当前：慢速（点击切快速）";
   


    private readonly HashSet<string> _broadcastedMessages = new();

    private void BroadcastOnce(string message)
    {
        if (!_broadcastedMessages.Contains(message))
        {
            _broadcastedMessages.Add(message);
            VoiceBroadcastText = message;
            Speak(message);
        }
    }

    private void Speak(string text)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
                synthesizer.Volume = 100;
                synthesizer.Rate = 0;
                synthesizer.SpeakAsync(text);
            }
            else if (OperatingSystem.IsLinux())
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "espeak";
                process.StartInfo.Arguments = $"-zh -s 150 \"{text}\"";
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "语音合成失败");
        }
    }

    public void ClearBroadcastHistory()
    {
        _broadcastedMessages.Clear();
    }

    private void StartCurveCollection()
    {
      // if (_isCollectingCurveData) return;
        
        WithstandVoltagePoints.Clear();
        WithstandCurrentPoints.Clear();
        _curveStartTime = DateTime.Now;
        
        _curveDataTimer = new System.Timers.Timer(100);
        _curveDataTimer.Elapsed += OnCurveDataTimerElapsed;
        _curveDataTimer.Start();
    }

    private void StopCurveCollection()
    {
        _curveDataTimer?.Stop();
        _curveDataTimer?.Dispose();
        _curveDataTimer = null;
    }

    private async Task RunTestLoopAsync(double voltageV, double currentMa, double durationSeconds)
    {
        _curveStartTime = DateTime.Now;

        while (Status == TestResult.Running )
        {

            //if (PlcDiMdi24)
            //{
            //    StartCurveCollection();
            //    elapsedSeconds = (DateTime.Now - _curveStartTime).TotalSeconds;
            //}
            //else
            //    StopCurveCollection();

            //if (elapsedSeconds >= durationSeconds)
            //{
            //    //RemainSeconds = 0;
            //    //StopTest();
            //    //break;
            //}

            //RemainSeconds = (int)PlcPubP63;// (int)(durationSeconds - elapsedSeconds);

            //if (elapsedSeconds > 5  && PlcDiMdi26)
            //{
            //    //(double simVoltageKv, double simCurrentMa) = GenerateSimulatedData(elapsedSeconds);
            //    //PlcPubP05 = simVoltageKv * 1000;
            //    //PlcPubP06 = simCurrentMa;
            //    RemainSeconds = 0;
            //    StopTest();
            //    break;
            //}

            await Task.Delay(100);
        }
    }

    private void OnCurveDataTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
       // if (!_isCollectingCurveData) return;

        double elapsedSeconds = (DateTime.Now - _curveStartTime).TotalSeconds;
        
        double pressureMpa = PlcPubP00;
        double displacementMm = PlcAiMai03;

        Dispatcher.UIThread.Post(() =>
        {
            WithstandVoltagePoints.Add(new ObservablePoint(elapsedSeconds, Math.Round(pressureMpa, 2)));
            WithstandCurrentPoints.Add(new ObservablePoint(elapsedSeconds, Math.Round(displacementMm, 1)));
            
            if (_withstandCurveXAxis != null && elapsedSeconds > AutoPanWindowSeconds)
            {
                _withstandCurveXAxis.MinLimit = elapsedSeconds - AutoPanWindowSeconds;
                _withstandCurveXAxis.MaxLimit = elapsedSeconds;
            }
        });
    }

    private (double voltageKv, double currentMa) GenerateSimulatedData(double elapsedSeconds)
    {
        double targetVoltage = PlcPubP00 / 1000.0;
        double targetCurrent = PlcPubP01;
        double duration = PlcPubP02;

        double voltageKv;
        double currentMa;

        double rampUpTime = duration * 0.3;
        double holdTime = duration * 0.4;
        double rampDownTime = duration * 0.3;

        if (elapsedSeconds <= rampUpTime)
        {
            double ratio = elapsedSeconds / rampUpTime;
            voltageKv = targetVoltage * ratio;
            currentMa = targetCurrent * ratio * 0.8 + 10 * Math.Sin(ratio * Math.PI);
        }
        else if (elapsedSeconds <= rampUpTime + holdTime)
        {
            voltageKv = targetVoltage;
            currentMa = targetCurrent * 0.9 + 5 * Math.Sin((elapsedSeconds - rampUpTime) * 2 * Math.PI);
        }
        else if (elapsedSeconds <= rampUpTime + holdTime + rampDownTime)
        {
            double ratio = (elapsedSeconds - rampUpTime - holdTime) / rampDownTime;
            voltageKv = targetVoltage * (1 - ratio);
            currentMa = targetCurrent * (1 - ratio * 0.6) + 5;
        }
        else
        {
            voltageKv = 0;
            currentMa = 5;
        }

        return (Math.Max(0, voltageKv), Math.Max(0, currentMa));
    }

    public string NetIndicatorText => WorkMode switch
    {
        WorkMode.Online      => ApiConnected ? "WiFi · 已连接" : "WiFi · 离线",
        WorkMode.Offline     => "WiFi 未连接",
        WorkMode.ExcelImport => "Excel 模板已加载",
        _ => ""
    };

    // ============= 服务 =============
    //private readonly IPlc200Communicator _plc;// smart 200
    private readonly IXdpPlcCommunicator _plc; //信捷
    private readonly System.Timers.Timer _clockTimer;
    private readonly System.Timers.Timer _plcPollTimer;
    private readonly System.Timers.Timer _envPollTimer;
    
    private volatile bool _isUpdatingFromPlc;
    private void UpdateFromPlc(Action action)
    {
        _isUpdatingFromPlc = true;
        try { action(); }
        finally { _isUpdatingFromPlc = false; }
    }
    private readonly ITaskApiService _api;
    private readonly IExcelImportService _excelService;
    private readonly LegacyTaskRepository _taskRepo;
    private readonly ProductRepository _productRepo;
    private readonly TemphumiHelper? _tempHumiHelper;
    private readonly KingbaseContext _kingbaseContext;

    public MainViewModel()
    {
        //_plc          = App.Services.GetRequiredService<IPlc200Communicator>();
        _plc = App.Services.GetRequiredService<IXdpPlcCommunicator>();
        _api          = App.Services.GetRequiredService<ITaskApiService>();
        _excelService = App.Services.GetRequiredService<IExcelImportService>();
        _taskRepo     = App.Services.GetRequiredService<LegacyTaskRepository>();
        _productRepo  = App.Services.GetRequiredService<ProductRepository>();
        _tempHumiHelper = App.Services.GetService<TemphumiHelper>();
        _reportService = App.Services.GetRequiredService<IReportService>();
        _kingbaseContext = new KingbaseContext();

        //DeviceInspection = new DeviceInspectionViewModel(_plc);  //计数器
        //DeviceInspection.NavigateBackToMainTest = () => CurrentNavSection = MainNavSection.MainTest;

        //HardwareCalibration = new HardwareCalibrationViewModel(); //零点增益
        //HardwareCalibration.NavigateBackToMainTest = () => CurrentNavSection = MainNavSection.MainTest;

        UserManagement = new UserManagementViewModel();
        UserManagement.NavigateBackToMainTest = () => CurrentNavSection = MainNavSection.MainTest;
        UserManagement.AppendMainTestProcessLog = AppendTestProcessLog;
        UserManagement.SyncMainLoopPresets = ApplyLoopPresetsFromPara;
        UserManagement.RefreshMainParameterSections = LoadParameterSectionNames;

        TestReportManagement = new TestReportManagementViewModel(_reportService, _kingbaseContext);
        TestReportManagement.NavigateBackToMainTest = () => CurrentNavSection = MainNavSection.MainTest;

        SystemLog = new SystemLogViewModel();
        SystemLog.NavigateBackToMainTest = () => CurrentNavSection = MainNavSection.MainTest;

        FaultDiagnosis = new FaultDiagnosisViewModel(this);
        FaultDiagnosis.NavigateBackToMainTest = () => CurrentNavSection = MainNavSection.MainTest;

        var chart = BuildWithstandCurveChart(WithstandVoltagePoints, WithstandCurrentPoints);//
        WithstandCurveSeries = chart.Series;
        WithstandCurveXAxes = chart.XAxes;
        WithstandCurveYAxes = chart.YAxes;
        _withstandCurveXAxis = chart.XAxes.FirstOrDefault();
        SeedDemoWithstandCurve();

        AvatarLetter = !string.IsNullOrEmpty(UserName) ? UserName[..1] : "?";

        RefreshReportData();

        _clockTimer = new System.Timers.Timer(1000);
        _clockTimer.Elapsed += (_, _) =>
            ClockText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clockTimer.Start();

        // PLC 数据轮询定时器（50ms 间隔，与 Smart200 读取频率一致）
        _plcPollTimer = new System.Timers.Timer(50);
        _plcPollTimer.Elapsed += (_, _) => UpdatePlcData();
        _plcPollTimer.Start();

        // 环境温湿度轮询定时器（1000ms 间隔）
        _envPollTimer = new System.Timers.Timer(1000);
        _envPollTimer.Elapsed += (_, _) => UpdateEnvData();
        _envPollTimer.Start();

        // 初始化信号灯状态（默认灰色，PLC值为1时变绿色）
        //SignalGroundProtectionColor = Colors.Gray;
        //SignalComputerControlColor = Colors.Gray;
        //SignalBellColor = Colors.Gray;
        //SignalOvercurrentColor = Colors.Gray;
        //SignalProtectionCurrentColor = Colors.Gray;
        //SignalWithstandVoltageTimeColor = Colors.Gray;
        //SignalCabinetDoorColor = Colors.Gray;

        LoadParameterSectionNames();
        //SignalRegulatorLowLimitColor = Colors.Gray;
        //SignalRegulatorHighLimitColor = Colors.Gray;
        //SignalStopColor = Colors.Gray;

        //TestProcessLogLines.Add("15:49:43: 操作台柜门打开");
        //TestProcessLogLines.Add("15:49:43: 试验停止信号");

        PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(WorkMode):
                    OnPropertyChanged(nameof(IsOnlineMode));
                    OnPropertyChanged(nameof(IsOfflineMode));
                    OnPropertyChanged(nameof(IsExcelImportMode));
                    OnPropertyChanged(nameof(CanEditInfoAndParameters));
                    OnPropertyChanged(nameof(ModeHint));
                    OnPropertyChanged(nameof(NetIndicatorText));
                    OnPropertyChanged(nameof(ShowSimulationStatus));
                    OnPropertyChanged(nameof(ShowNetworkErrorStatus));
                    break;
                case nameof(Status):
                    OnPropertyChanged(nameof(StatusText));
                    break;
                case nameof(CurrentDetailIndex):
                    OnPropertyChanged(nameof(CurrentDetail));
                    OnPropertyChanged(nameof(CurrentDetailDisplay));
                    OnPropertyChanged(nameof(HasPrevious));
                    OnPropertyChanged(nameof(HasNext));
                    ApplyCurrentDetailToUi();
                    break;
                case nameof(ApiConnected):
                    OnPropertyChanged(nameof(NetIndicatorText));
                    OnPropertyChanged(nameof(ShowNetworkErrorStatus));
                    break;
                case nameof(PlcConnected):
                    OnPropertyChanged(nameof(StatusPlcText));
                    OnPropertyChanged(nameof(PlcStatusBrush));
                    OnPropertyChanged(nameof(PlcStatusTextForPanel));
                    OnPropertyChanged(nameof(ShowSimulationStatus));
                    break;
                case nameof(CurrentNavSection):
                    OnPropertyChanged(nameof(ShowMainTestContent));
                    OnPropertyChanged(nameof(ShowReportManagementContent));
                    OnPropertyChanged(nameof(ShowDeviceInspectionContent));
                    OnPropertyChanged(nameof(ShowHardwareCalibrationContent));
                    OnPropertyChanged(nameof(ShowUserManagementContent));
                    OnPropertyChanged(nameof(ShowSystemLogContent));
                    OnPropertyChanged(nameof(ShowFaultDiagnosisContent));
                    OnPropertyChanged(nameof(IsMainTestNavSelected));
                    OnPropertyChanged(nameof(IsReportManagementNavSelected));
                    OnPropertyChanged(nameof(IsDeviceInspectionNavSelected));
                    OnPropertyChanged(nameof(IsHardwareCalibrationNavSelected));
                    OnPropertyChanged(nameof(IsUserManagementNavSelected));
                    OnPropertyChanged(nameof(IsSystemLogNavSelected));
                    OnPropertyChanged(nameof(IsFaultDiagnosisNavSelected));
                    break;
            }
        };
    }

    /// <summary>
    /// 从温湿度传感器读取数据并更新 ViewModel 属性
    /// </summary>
    private void UpdateEnvData()
    {
        if (_tempHumiHelper == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            EnvTempC = _tempHumiHelper.Temp;
            EnvHumidity = _tempHumiHelper.RH;
        });
    }

    /// <summary>
    /// 从 PLC 读取数据并更新 ViewModel 属性
    /// </summary>
    private void UpdatePlcData()
    {
        if (_plc == null)
            return;

        Dispatcher.UIThread.Post(() => PlcConnected = _plc.IsConnected);

        if (!_plc.IsConnected)
            return;

        try
        {
            var plcStatusList = _plc.GetPlcStatusList();
            if (plcStatusList == null || plcStatusList.Count == 0)
                return;

            foreach (var data in plcStatusList)
            {
                if (string.IsNullOrEmpty(data.TagName))
                    continue;

                // 所有 UI 更新都必须在 UI 线程执行
                Dispatcher.UIThread.Post(() => UpdateFromPlc(() => UpdatePropertyFromPlc(data)));
            }

            // 刷新设备检查模块的计数
            Dispatcher.UIThread.Post(() => DeviceInspection?.RefreshCountsCommand.Execute(null));
            Dispatcher.UIThread.Post(() =>
            {
                if (CurrentNavSection == MainNavSection.FaultDiagnosis)
                    FaultDiagnosis.RefreshFromMain();
            });
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "PLC 数据更新失败");
        }
    }

    /// <summary>
    /// 写入 PLC 变量
    /// </summary>
    /// <param name="tagName">PLC 变量名</param>
    /// <param name="value">值</param>
    public void WritePlcTag(string tagName, object value)
    {
        try
        {
            if (_plc != null && _plc.IsConnected)
            {
                _plc.WriteTag(tagName, value);
                NlogHelper.Info($"写入 PLC 变量成功：{tagName} = {value}");
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, $"写入 PLC 变量失败：{tagName}");
        }
    }

    /// <summary>
    /// 根据 PLC 数据更新对应的 ViewModel 属性
    /// </summary>
    private void UpdatePropertyFromPlc(XdpPlcDataModel data)
    {
        switch (data.TagName)
        {
            // AI 模拟量
            case "AI.MAI00":
                PlcAiMai00 = Convert.ToDouble(data.Value);//径向泵_高压实际压力
                HighVoltageOutput = PlcAiMai00;
                break;
            case "AI.MAI01":
                PlcAiMai01 = Convert.ToDouble(data.Value);//径向泵_低压实际压力
                CurrentMa = PlcAiMai01;
                break;              
            case "AI.MAI02":
                PlcAiMai02 = Convert.ToDouble(data.Value);//径向泵实际温度
                break;
            case "AI.MAI03":
                PlcAiMai03 = Convert.ToDouble(data.Value);
                RefreshDisplacementGauge();
                break;
            case "AI.MAI04":
                PlcAiMai04 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI05":
                PlcAiMai05 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI06":
                PlcAiMai06 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI07":
                PlcAiMai07 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI08":
                PlcAiMai08 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI09":
                PlcAiMai09 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI10":
                PlcAiMai10 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI11":
                PlcAiMai11 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI12":
                PlcAiMai12 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI13":
                PlcAiMai13 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI14":
                PlcAiMai14 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI15":
                PlcAiMai15 = Convert.ToDouble(data.Value);
                break;
            case "AI.MAI16":
                PlcAiMai16 = Convert.ToDouble(data.Value);
                break;
            //case "AI.Gain00":
            //    PlcAiGain00 = Convert.ToDouble(data.Value);
            //    break;
            //case "AI.Gain01":
            //    PlcAiGain01 = Convert.ToDouble(data.Value);
            //    break;
            //case "AI.Gain02":
            //    PlcAiGain02 = Convert.ToDouble(data.Value);
            //    break;
            //case "AI.Gain03":
            //    PlcAiGain03 = Convert.ToDouble(data.Value);
            //    break;
            //case "AI.Zero00":
            //    PlcAiZero00 = Convert.ToDouble(data.Value);
            //    break;
            //case "AI.Zero01":
            //    PlcAiZero01 = Convert.ToDouble(data.Value);
            //    break;
            //case "AI.Zero02":
            //    PlcAiZero02 = Convert.ToDouble(data.Value);
            //    break;
            //case "AI.Zero03":
            //    PlcAiZero03 = Convert.ToDouble(data.Value);
            //    break;

            // DI 数字量输入  Color.FromRgb(255, 0, 0)红色，Color.FromRgb(46, 209, 88)绿色
            case "DI.MDI00":
                PlcDiMdi00 = Convert.ToBoolean(data.Value);
                SignalColor_DI00 = PlcDiMdi00 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                break;
            case "DI.MDI01":
                PlcDiMdi01 = Convert.ToBoolean(data.Value);
                SignalColor_DI01 = PlcDiMdi01 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                CurrentSpeedModeIsFast = PlcDiMdi01; // M419=1 快速，M419=0 慢速
                break;
            case "DI.MDI02":
                PlcDiMdi02 = Convert.ToBoolean(data.Value);
                SignalColor_DI02 = PlcDiMdi02 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                CurrentSpeedModeIsFast = PlcDiMdi02; // M420=1 快速，M420=0 慢速
                break;
            case "DI.MDI03":
                PlcDiMdi03 = Convert.ToBoolean(data.Value);
                SignalColor_DI03 = PlcDiMdi03 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                break;
            case "DI.MDI04":
                PlcDiMdi04 = Convert.ToBoolean(data.Value);
                SignalColor_DI04 = PlcDiMdi04 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                break;
            case "DI.MDI05":
                PlcDiMdi05 = Convert.ToBoolean(data.Value);
                SignalColor_DI05 = PlcDiMdi05 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                break;
            case "DI.MDI06":
                PlcDiMdi06 = Convert.ToBoolean(data.Value);
                SignalColor_DI06 = PlcDiMdi06 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                break;
            case "DI.MDI07":
                PlcDiMdi07 = Convert.ToBoolean(data.Value);
                SignalColor_DI07 = PlcDiMdi07 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                break;
            case "DI.MDI08":
                PlcDiMdi08 = Convert.ToBoolean(data.Value);
                SignalColor_DI08 = PlcDiMdi08 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                break;
            case "DI.MDI09":
                PlcDiMdi09 = Convert.ToBoolean(data.Value);
                SignalColor_DI09 = PlcDiMdi09 ? Color.FromRgb(46, 209, 88) : Colors.Gray;
                break;
            case "DI.MDI10":
                PlcDiMdi10 = Convert.ToBoolean(data.Value);
                SignalColor_DI10 = PlcDiMdi10 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI11":
                PlcDiMdi11 = Convert.ToBoolean(data.Value);
                SignalColor_DI11 = PlcDiMdi11 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI12":
                PlcDiMdi12 = Convert.ToBoolean(data.Value);
                SignalColor_DI12 = PlcDiMdi12 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI13":
                PlcDiMdi13 = Convert.ToBoolean(data.Value);
                SignalColor_DI13 = PlcDiMdi13 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI14":
                PlcDiMdi14 = Convert.ToBoolean(data.Value);
                SignalColor_DI14 = PlcDiMdi14 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI15":
                PlcDiMdi15 = Convert.ToBoolean(data.Value);
                SignalColor_DI15 = PlcDiMdi15 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI16":
                PlcDiMdi16 = Convert.ToBoolean(data.Value);
                SignalColor_DI16 = PlcDiMdi16 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI17":
                PlcDiMdi17 = Convert.ToBoolean(data.Value);
                SignalColor_DI17 = PlcDiMdi17 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI18":
                PlcDiMdi18 = Convert.ToBoolean(data.Value);
                SignalColor_DI18 = PlcDiMdi18 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI19":
                PlcDiMdi19 = Convert.ToBoolean(data.Value);
                SignalColor_DI19 = PlcDiMdi19 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            case "DI.MDI20":
                PlcDiMdi20 = Convert.ToBoolean(data.Value);
                SignalColor_DI20 = PlcDiMdi20 ? Color.FromRgb(255, 0, 0) : Colors.Gray;
                break;
            // DO 数字量输出
            case "DO.CDO00":
                PlcDoCdo00 = Convert.ToBoolean(data.Value);
                break;
            case "DO.CDO01":
                PlcDoCdo01 = Convert.ToBoolean(data.Value);
                break;
            case "DO.CDO02":
                PlcDoCdo02 = Convert.ToBoolean(data.Value);
                break;
            case "DO.CDO03":
                PlcDoCdo03 = Convert.ToBoolean(data.Value);
                break;
           
            // PUB 公共变量
            case "PUB.P00":
                PlcPubP00 = Convert.ToDouble(data.Value);
                RefreshTargetPressureGauge();
                break;
            case "PUB.P01":
                PlcPubP01 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P02":
                PlcPubP02 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P03":
                PlcPubP03 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P04":
                PlcPubP04 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P05":
                PlcPubP05 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P06":
                PlcPubP06 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P07":
                PlcPubP07 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P08":
                PlcPubP08 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P09":
                PlcPubP09 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P10":
                PlcPubP10 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P11":
                PlcPubP11 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P12":
                PlcPubP12 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P13":
                PlcPubP13 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P14":
                PlcPubP14 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P15":
                PlcPubP15 = Convert.ToDouble(data.Value);
                break;
            case "PUB.P16":
                PlcPubP16 = Convert.ToDouble(data.Value);
                break;

            //// PUB DWord类型变量（设定次数）
            //case "PUB.P09":
            //    PlcPubP09 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P10":
            //    PlcPubP10 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P11":
            //    PlcPubP11 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P12":
            //    PlcPubP12 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P13":
            //    PlcPubP13 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P14":
            //    PlcPubP14 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P15":
            //    PlcPubP15 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P16":
            //    PlcPubP16 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P17":
            //    PlcPubP17 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P18":
            //    PlcPubP18 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P19":
            //    PlcPubP19 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P20":
            //    PlcPubP20 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P21":
            //    PlcPubP21 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P22":
            //    PlcPubP22 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P23":
            //    PlcPubP23 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P24":
            //    PlcPubP24 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P25":
            //    PlcPubP25 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P26":
            //    PlcPubP26 = Convert.ToDouble(data.Value);
            //    break;

            //// PUB 语音提醒变量
            //case "PUB.P45":
            //    PlcPubP45 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP45) BroadcastOnce("试验通过");
            //    break;
            //case "PUB.P46":
            //    PlcPubP46 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP46) BroadcastOnce("试验失败");
            //    break;
            //case "PUB.P47":
            //    PlcPubP47 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP47) BroadcastOnce("高压输出已合闸，注意安全");
            //    break;
            //case "PUB.P48":
            //    PlcPubP48 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP48) BroadcastOnce("电机复位中");
            //    break;
            //case "PUB.P49":
            //    PlcPubP49 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP49) BroadcastOnce("已到达零位");
            //    break;
            //case "PUB.P50":
            //    PlcPubP50 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP50) BroadcastOnce("急停已按下，请注意");
            //    break;
            //case "PUB.P51":
            //    PlcPubP51 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP51) BroadcastOnce("降压放电中");
            //    break;
            //case "PUB.P52":
            //    PlcPubP52 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP52) BroadcastOnce("接地保护失效");
            //    break;
            //case "PUB.P53":
            //    PlcPubP53 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP53) BroadcastOnce("升压器过流保护");
            //    break;
            //case "PUB.P54":
            //    PlcPubP54 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP54) BroadcastOnce("放电已完成");
            //    break;
            //case "PUB.P55":
            //    PlcPubP55 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP55) BroadcastOnce("启动试验");
            //    break;
            //case "PUB.P56":
            //    PlcPubP56 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP56) BroadcastOnce("电压传感器损坏");
            //    break;
            //case "PUB.P57":
            //    PlcPubP57 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP57) BroadcastOnce("门限位失效，请注意");
            //    break;
            //case "PUB.P58":
            //    PlcPubP58 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP58) BroadcastOnce("未到达设置电压");
            //    break;
            //case "PUB.P59":
            //    PlcPubP59 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP59) BroadcastOnce("仪表与传感器采值误差偏大");
            //    break;
            //case "PUB.P60":
            //    PlcPubP60 = Convert.ToBoolean(data.Value);
            //    if (PlcPubP60) BroadcastOnce("伺服过流报警");
            //    break;
            //case "PUB.P61":
            //    PlcPubP61 = Convert.ToBoolean(data.Value);
            //    break;
            //case "PUB.P62":
            //    PlcPubP62 = Convert.ToBoolean(data.Value);
            //    break;

            //// PUB DWord类型变量（实际次数）
            //case "PUB.P66":
            //    PlcPubP66 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P67":
            //    PlcPubP67 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P68":
            //    PlcPubP68 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P69":
            //    PlcPubP69 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P70":
            //    PlcPubP70 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P71":
            //    PlcPubP71 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P72":
            //    PlcPubP72 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P73":
            //    PlcPubP73 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P74":
            //    PlcPubP74 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P75":
            //    PlcPubP75 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P76":
            //    PlcPubP76 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P77":
            //    PlcPubP77 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P78":
            //    PlcPubP78 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P79":
            //    PlcPubP79 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P80":
            //    PlcPubP80 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P81":
            //    PlcPubP81 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P82":
            //    PlcPubP82 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P83":
            //    PlcPubP83 = Convert.ToDouble(data.Value);
            //    break;
            //case "PUB.P84":
            //    PlcPubP84 = Convert.ToBoolean(data.Value);
            //    break;
        }
    }

    [RelayCommand]
    private void SelectNav(string? section)
    {
        var next = section switch
        {
            "main" => MainNavSection.MainTest,
            "reportManagement" => MainNavSection.ReportManagement,
            "deviceInspection" => MainNavSection.DeviceInspection,
            "hardwareCalibration" => MainNavSection.HardwareCalibration,
            "userManagement" => MainNavSection.UserManagement,
            "systemLog" => MainNavSection.SystemLog,
            "faultDiagnosis" => MainNavSection.FaultDiagnosis,
            _ => CurrentNavSection
        };
        if (next == CurrentNavSection)
        {
            if (next == MainNavSection.UserManagement)
                UserManagement.RefreshCommand.Execute(null);
            return;
        }

        AppendTestProcessLog(next switch
        {
            MainNavSection.MainTest => "使用「主界面」",
            MainNavSection.ReportManagement => "使用「历史报表」",
            MainNavSection.DeviceInspection => "使用「设备检查」",
            MainNavSection.HardwareCalibration => "使用「硬件校准」",
            MainNavSection.UserManagement => "使用「数据管理」",
            MainNavSection.SystemLog => "使用「系统日志」",
            MainNavSection.FaultDiagnosis => "使用「错误诊断」",
            _ => "切换导航"
        });
        CurrentNavSection = next;
        if (next == MainNavSection.UserManagement)
            UserManagement.RefreshCommand.Execute(null);
        if (next == MainNavSection.FaultDiagnosis)
            FaultDiagnosis.RefreshFromMain();
        if (next == MainNavSection.SystemLog)
            SystemLog.Refresh();
        NlogHelper.Info($"侧边导航：{next}");
    }

    [RelayCommand]
    private void SwitchMode(string mode)
    {
        WorkMode = mode switch
        {
            "online"  => WorkMode.Online,
            "offline" => WorkMode.Offline,
            "excel"   => WorkMode.ExcelImport,
            _ => WorkMode
        };
        VarHelper.CurrentWorkMode = WorkMode;
        OnPropertyChanged(nameof(ModeHint));
        OnPropertyChanged(nameof(IsOnlineMode));
        OnPropertyChanged(nameof(IsOfflineMode));
        OnPropertyChanged(nameof(CanEditInfoAndParameters));
        AppendTestProcessLog(mode switch
        {
            "online" => "使用「在线模式」（车型等只读，操作员可编辑；请从任务查看选择任务）",
            "offline" => "使用「离线模式」（可编辑信息录入）",
            "excel" => "使用「Excel 导入」",
            _ => "切换工作模式"
        });
        NlogHelper.Info($"切换到模式：{WorkMode}");
    }

    [RelayCommand]
    private void PreviousDetail()
    {
        if (HasPrevious) CurrentDetailIndex--;
    }

    [RelayCommand]
    private void NextDetail()
    {
        if (HasNext) CurrentDetailIndex++;
    }

    private void ApplyCurrentDetailToUi()
    {
        var d = CurrentDetail;
        if (d == null) return;
        CurrentTaskNumber = d.DetailId ?? "—";
        CurrentTaskTitle  = d.StepName ?? "未命名步骤";
        CurrentCarType    = d.ProjectNumber ?? "—";
        CurrentTrainCar   = string.IsNullOrWhiteSpace(d.TrainNo) ? "—" : d.TrainNo!;
        InfoEntryBearingTypeText = CurrentCarType == "—" ? "" : CurrentCarType;
        InfoEntryTrainCarText = CurrentTrainCar == "—" ? "" : CurrentTrainCar;
        InfoEntryWorkpieceCodeText = !string.IsNullOrWhiteSpace(d.CarCode)
            ? d.CarCode!
            : (d.DetailId ?? "");
    }

    partial void OnInfoEntryBearingTypeTextChanged(string value)
    {
        if (!CanEditInfoAndParameters) return;// 离线模式才允许编辑
        CurrentCarType = string.IsNullOrWhiteSpace(value) ? "—" : value;
        ReportProductModel = value;
        // 车型与工艺参数解耦：不再按车型名自动加载 Para.ini
    }

    partial void OnInfoEntryTrainCarTextChanged(string value)
    {
        if (!CanEditInfoAndParameters) return;
        CurrentTrainCar = string.IsNullOrWhiteSpace(value) ? "—" : value;
        ReportVehicleNumber = value;
    }

    [RelayCommand]
    private async Task ImportExcelAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;
        var result = await _excelService.ImportAsync(filePath);
        TaskDetails.Clear();
        foreach (var t in result.Tasks)
        {
            TaskDetails.Add(new TaskDetailItem
            {
                DetailId = t.TaskId,
                StepName = t.TestItem,
                ProjectNumber = t.CarType,
                TrainNo = t.TrainNo,
                CarCode = t.CarNo
            });
        }
        ExcelFileName = result.FileName;
        ExcelTaskCount = result.Tasks.Count;
        CurrentDetailIndex = 0;
        WorkMode = WorkMode.ExcelImport;
        OnPropertyChanged(nameof(TotalDetails));
        OnPropertyChanged(nameof(CurrentDetailDisplay));
        OnPropertyChanged(nameof(ShowExcelBanner));
        NlogHelper.Info($"Excel 导入：{result.Tasks.Count} 条，错误 {result.Errors.Count}");
    }

    [RelayCommand]
    private void Logout() => VarHelper.ClearUserSession();

    /// <summary>任务查看确认后：加载该子任务全部步骤，并定位到所选明细。</summary>
    public async void ApplyOnlineTaskSelection(OnlineTaskSelection sel)
    {
        try
        {
            WorkMode = WorkMode.Online;
            VarHelper.CurrentWorkMode = WorkMode.Online;

            var steps = await _taskRepo.GetHoldStepsAsync(sel.HoldTaskId!, sel.HoldItemId);
            TaskDetails.Clear();
            var focusIndex = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                var d = steps[i];
                if (d.ID == sel.LocalDetailId)
                    focusIndex = i;
                TaskDetails.Add(new TaskDetailItem
                {
                    LocalDetailId = d.ID,
                    LocalTaskRowId = sel.LocalTaskRowId,
                    HoldTaskId = sel.HoldTaskId,
                    HoldItemId = sel.HoldItemId,
                    DetailId = d.detailId,
                    StepName = d.stepName,
                    StepContent = d.stepContent,
                    ProjectNumber = sel.ProjectNumber,
                    TrainNo = sel.TrainNo,
                    CarCode = sel.CarCode,
                    ResultStandard = d.resultStandard,
                    ResultUnit = d.resultUnit,
                    IsTested = d.isComplete >= 1
                });
            }

            CurrentDetailIndex = focusIndex;
            OnPropertyChanged(nameof(TotalDetails));
            OnPropertyChanged(nameof(CurrentDetailDisplay));
            ApplyCurrentDetailToUi();
            AppendTestProcessLog($"已选择任务：{sel.StepName}（{sel.DetailId}）");
            NlogHelper.Info($"在线任务已选：HoldTask={sel.HoldTaskId} Item={sel.HoldItemId} Detail={sel.DetailId}");
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "加载所选任务失败");
            AppendTestProcessLog($"加载任务失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void StartTest()
    {
        if (IsOnlineMode && CurrentDetail is null)
        {
            AppendTestProcessLog("在线模式请先通过「任务查看」选择任务");
            return;
        }

        //if (!PlcDiMdi00) { AppendTestProcessLog("请注意,急停情况下无法启动"); return; }
        //if (!PlcDiMdi07) { AppendTestProcessLog("请注意,门限位失效情况下无法启动"); return; }
        //if (PlcDiMdi02) { AppendTestProcessLog("请注意,请将手自动按钮切换到电脑控制"); return; }
        //if (PlcDiMdi01) { AppendTestProcessLog("请注意,接地信号断开,无法开始试验"); return; }
        //if (PlcDiMdi04) { AppendTestProcessLog("请注意,升压器输入过流,无法开始试验"); return; }
        //if (PlcDiMdi05) { AppendTestProcessLog("请注意,保护电流过流,无法开始试验"); return; }
        if (PlcDiMdi11) { AppendTestProcessLog("请注意,油箱液位到达极限,无法开始退卸"); return; }
        if (!PlcPubP00.Equals(0)) { AppendTestProcessLog("请注意,目标压力未设置,无法开始退卸"); return; }
        if(PlcAiMai03>=400) { AppendTestProcessLog("请注意,拉杆已到达极限位置,无法开始退卸"); return; }


        AppendTestProcessLog("使用「开始试验」");
        Status = TestResult.Running;
        MarkOnlineTestStarted();
        NlogHelper.Info($"开始试验 · 模式={WorkMode}");
        UpdateReportTestTime();
        _ = IsTestStartAsync();
    }

    // 开始试验操作
    private async Task IsTestStartAsync()
    {
        //taskManager.Hmi = this;
        //BaseTest.ucGrid = ucGrid1;
        Window? mainWindow = null;
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = desktop.MainWindow;
        }
        if (mainWindow != null && await GenericDialog.ShowMsgAsync(mainWindow, "试验即将开始,请确认线路是否接好！！！", "提示") == false)
            return;

        //WritePlcTag("DO.CDO10", false);//主回路合闸控制
        //WritePlcTag("DO.CDO11", false);//匝间试验切换
        //WritePlcTag("DO.CDO15", false);//绝缘试验切换

        //WritePlcTag("DI.MDI24", false);
        //WritePlcTag("DI.MDI25", false);
        //WritePlcTag("DI.MDI26", false);

        //WritePlcTag("PUB.P62", false);
        //WritePlcTag("PUB.P61", false);
        //PlcDiMdi24 = false;  //关闭计时
        //PlcDiMdi25 = false;  //过流标志位
        //PlcDiMdi26 = false;  //试验完成标志
        //PlcPubP62 = false;                   //手动自动
        //PlcPubP61 = false;                   //试验开始标志

        WritePlcTag("PUB.P00",PlcPubP00);//目标压力
        WritePlcTag("PUB.P04",PlcPubP04);//位移补充值
        WritePlcTag("PUB.P01",PlcPubP01);//开始减速压力
        WritePlcTag("PUB.P14",PlcPubP14);//高压浮动限制
        WritePlcTag("PUB.P15",PlcPubP15);//方向时间
        //if (string.IsNullOrEmpty(InfoEntryBearingTypeText))
        //{
        //    AppendTestProcessLog("请先选择车型型号！");
        //    return;
        //}

        //if (!PlcDiMdi09)
        //{
        //    AppendTestProcessLog("请先电机回到零位！");
        //    return;
        //}

        //var (voltageV, currentMa) = GetCurrentLoopParameters();

        //WritePlcTag("PUB.P00", voltageV);
        //WritePlcTag("PUB.P01", currentMa);
        //WritePlcTag("PUB.P02", ManualDurationSeconds);
        //WritePlcTag("PUB.P03", 0);
        //WritePlcTag("PUB.P04", 1);
        //WritePlcTag("PUB.P62", true);
        //WritePlcTag("PUB.P61", true);

        //PlcPubP00 = voltageV;               //电压（V）
        //PlcPubP01 = currentMa;              //电流（mA）
        //PlcPubP02 = ManualDurationSeconds;  //时间（s）
        //PlcPubP03 = 0;                      //设定电压零点
        //PlcPubP04 = 1;                      //设定电压增益
        //PlcPubP62 = true;                   //手动自动
        //PlcPubP61 = true;                   //试验开始标志
        // DI.MDI26
        //AppendTestProcessLog($"在线试验 - 回路{SelectedParameterLoop} - 车型: {InfoEntryBearingTypeText}, 电压: {voltageV}V, 电流: {currentMa}mA, 时间: {ManualDurationSeconds}s");
        AppendTestProcessLog($"开始退卸作业 - 车型: {InfoEntryBearingTypeText}, 目标压力: {PlcPubP00}MPa, 反向时间: {PlcPubP15}s");
        //Thread.Sleep(3000);


        //  _ = RunTestLoopAsync(voltageV, currentMa, ManualDurationSeconds);
        // 启动曲线采集
        StartCurveCollection();

      //  return;
        //else
        //{
        //    if (string.IsNullOrEmpty(VarHelper.mTestViewModel.ModelName)) { MessageHelper.MessageOK(frm, "请选择产品型号后开始试验"); return false; }
        //    if (!GetRadioButton()) { MessageHelper.MessageOK(frm, "回路未选择！"); return false; }
        //    Reportwrite();
        //    InitCurve(LabTimeSetting.Value);
        //    BaseTest.CheckCount = GetRadioButtonIndex();
        //    OPCHelper.PUBgrp[0] = LabVoltageSetting.Value; //电压
        //    OPCHelper.PUBgrp[1] = uiCurrentLab.Value;      //电流
        //    OPCHelper.PUBgrp[2] = LabTimeSetting.Value;    //时间
        //    OPCHelper.PUBgrp[61] = true;  //试验开始
        //    taskManager.StartTask("离线试验");
        //}
        //return true;
    }


    [RelayCommand]
    private void EndTest()
    {
        // 拉杆停止（如果有控制）
        WritePlcTag("DO.CDO00", false);  // 拉杆上行 OFF
        WritePlcTag("DO.CDO01", false);  // 拉杆下行 OFF

        AppendTestProcessLog("使用「结束退卸作业」");
        Status = TestResult.Stopped;
        MarkOnlineTestEnded();
        NlogHelper.Info("结束退卸作业");
        StopCurveCollection();
        // 任务回传只列出 isComplete=1；结束作业后标记待回传
        _ = MarkCurrentDetailReadyForUploadAsync();
    }

    private async Task MarkCurrentDetailReadyForUploadAsync()
    {
        if (!IsOnlineMode || CurrentDetail is null)
            return;
        try
        {
            var detail = await _taskRepo.GetHoldByIdAsync(CurrentDetail.LocalDetailId);
            if (detail is null) return;
            if (detail.isComplete >= 1)
            {
                CurrentDetail.IsTested = true;
                return;
            }

            detail.isComplete = 1;
            if (string.IsNullOrWhiteSpace(detail.operatePersonName))
                detail.operatePersonName = ResolveMesWorkerName();
            if (string.IsNullOrWhiteSpace(detail.operateTime))
                detail.operateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            await _taskRepo.ModifyOrAddHoldTaskAsync(detail);
            CurrentDetail.IsTested = true;
            AppendTestProcessLog($"任务已标记待回传，可在「任务上传」中搜索：{CurrentDetail.StepName}");
            NlogHelper.Info($"任务标记待回传：detailId={detail.detailId}，holdRowId={detail.ID}");
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "标记任务待回传失败");
            AppendTestProcessLog($"标记待回传失败：{ex.Message}");
        }
    }

    private void WriteLoopTestData()
    {
        double voltageKv =0.0 ;//PlcPubP64
        double currentMa = 0.0;//PlcPubP65

        string testResult = currentMa <= GetCurrentThreshold() ? "合格" : "不合格";

        switch (SelectedParameterLoop)
        {
            case 1:
                ReportLoopOneWithstandVoltage = $"{voltageKv:F2}";
                ReportLoopOneLeakageCurrent = $"{currentMa:F2}";
                ReportLoopOneTestResult = testResult;
                break;
            case 2:
                ReportLoopTwoWithstandVoltage = $"{voltageKv:F2}";
                ReportLoopTwoLeakageCurrent = $"{currentMa:F2}";
                ReportLoopTwoTestResult = testResult;
                break;
            case 3:
                ReportLoopThreeWithstandVoltage = $"{voltageKv:F2}";
                ReportLoopThreeLeakageCurrent = $"{currentMa:F2}";
                ReportLoopThreeTestResult = testResult;
                break;
            case 4:
                ReportLoopFourWithstandVoltage = $"{voltageKv:F2}";
                ReportLoopFourLeakageCurrent = $"{currentMa:F2}";
                ReportLoopFourTestResult = testResult;
                break;
        }
    }

    private double GetCurrentThreshold()
    {
        return SelectedParameterLoop switch
        {
            1 => string.IsNullOrEmpty(LoopCurrentText1) ? 100 : double.Parse(LoopCurrentText1),
            2 => string.IsNullOrEmpty(LoopCurrentText2) ? 100 : double.Parse(LoopCurrentText2),
            3 => string.IsNullOrEmpty(LoopCurrentText3) ? 100 : double.Parse(LoopCurrentText3),
            4 => string.IsNullOrEmpty(LoopCurrentText4) ? 100 : double.Parse(LoopCurrentText4),
            _ => 100
        };
    }

    private bool _disposed;

    private bool _isSafetyAlarmFlashing;
    public bool IsSafetyAlarmFlashing
    {
        get => _isSafetyAlarmFlashing;
        set => SetProperty(ref _isSafetyAlarmFlashing, value);
    }

    private string _safetyAlarmMessage = "";
    public string SafetyAlarmMessage
    {
        get => _safetyAlarmMessage;
        set => SetProperty(ref _safetyAlarmMessage, value);
    }

    private System.Timers.Timer? _alarmFlashTimer;

    private void ShowSafetyAlarm(string message)
    {
        SafetyAlarmMessage = message;
        IsSafetyAlarmFlashing = true;

        _alarmFlashTimer?.Stop();
        _alarmFlashTimer = new System.Timers.Timer(500);
        _alarmFlashTimer.Elapsed += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsSafetyAlarmFlashing = !IsSafetyAlarmFlashing;
            });
        };
        _alarmFlashTimer.Start();
    }

    private void ClearSafetyAlarm()
    {
        _alarmFlashTimer?.Stop();
        _alarmFlashTimer?.Dispose();
        _alarmFlashTimer = null;
        IsSafetyAlarmFlashing = false;
        SafetyAlarmMessage = "";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            StopMesStatusReporting();

            // 停止并释放定时器
            _clockTimer?.Stop();
            _clockTimer?.Dispose();

            _plcPollTimer?.Stop();
            _plcPollTimer?.Dispose();

            _envPollTimer?.Stop();
            _envPollTimer?.Dispose();

            // 释放温湿度传感器资源
            if (_tempHumiHelper != null)
                _tempHumiHelper.IsExit = true;

            // 停止 PLC 后台线程但保持连接（用于退出登录时）
            _plc?.Stop();
        }

        _disposed = true;
    }

    ~MainViewModel()
    {
        Dispose(false);
    }

    /// <summary>LiveCharts 轴标题走 Skia 绘制；Segoe UI 无汉字字形会显示为「□□」。</summary>
    private static SKTypeface CreateChartCjkTypeface()
    {
        var mgr = SKFontManager.Default;
        return mgr.MatchCharacter('压')
               ?? mgr.MatchCharacter('泄')
               ?? SKTypeface.FromFamilyName("Microsoft YaHei UI")
               ?? SKTypeface.FromFamilyName("Microsoft YaHei")
               ?? SKTypeface.Default;
    }

    private static (ISeries[] Series, Axis[] XAxes, Axis[] YAxes) BuildWithstandCurveChart(
        ObservableCollection<ObservablePoint> voltagePoints, ObservableCollection<ObservablePoint> currentPoints)//
    {
        var axisTypeface = CreateChartCjkTypeface();
        var labelPaint = new SolidColorPaint(SKColors.White) { SKTypeface = axisTypeface };
        var namePaint = new SolidColorPaint(SKColors.White) { SKTypeface = axisTypeface };
        var gridPaint = new SolidColorPaint(new SKColor(230, 230, 230, 160))
        {
            StrokeThickness = 1,
            PathEffect = new DashEffect(new float[] { 6, 6 }, 0)
        };

        var voltageStroke = new SolidColorPaint(new SKColor(0xEF, 0x9A, 0x4E)) { StrokeThickness = 2 };
        var currentStroke = new SolidColorPaint(new SKColor(0x85, 0xB7, 0xEB)) { StrokeThickness = 2 };

        var xAxis = new Axis
        {
            Name = "时间(s)",
            NamePaint = namePaint,
            NameTextSize = 12,
            LabelsPaint = labelPaint,
            TextSize = 11,
            MinStep = 10,
            ForceStepToMin = true,
            SeparatorsPaint = gridPaint,
            ShowSeparatorLines = true,
            TicksPaint = new SolidColorPaint(SKColors.Gainsboro) { StrokeThickness = 1 }
        };

        var yVoltage = new Axis
        {
            Position = AxisPosition.Start,
            Name = "压力(MPa)",
            NamePaint = namePaint,
            NameTextSize = 12,
            LabelsPaint = labelPaint,
            TextSize = 11,
            MinLimit = 0,
            MaxLimit = 30,
            MinStep = 5,
            ForceStepToMin = true,
            SeparatorsPaint = gridPaint,
            ShowSeparatorLines = true,
            TicksPaint = new SolidColorPaint(SKColors.Gainsboro) { StrokeThickness = 1 }
        };

        var yDisplacement = new Axis
        {
            Position = AxisPosition.End,
            Name = "位移(mm)",
            NamePaint = namePaint,
            NameTextSize = 12,
            LabelsPaint = labelPaint,
            TextSize = 11,
            MinLimit = 0,
            MaxLimit = 400,
            MinStep = 50,
            ForceStepToMin = true,
            ShowSeparatorLines = false,
            SeparatorsPaint = gridPaint,
            TicksPaint = new SolidColorPaint(SKColors.Gainsboro) { StrokeThickness = 1 }
        };

        var series = new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Values = currentPoints,
                Name = "位移",
                Stroke = currentStroke,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.2,
                ScalesYAt = 0
            },
            new LineSeries<ObservablePoint>
            {
                Values = voltagePoints,
                Name = "压力",
                Stroke = voltageStroke,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.2,
                ScalesYAt = 1
            }
        };

        return (Series: series, XAxes: new[] { xAxis }, YAxes: new[] { yDisplacement, yVoltage }); 
    }

    private void SeedDemoWithstandCurve()
    {
        WithstandVoltagePoints.Clear();
        WithstandCurrentPoints.Clear();

        // 演示曲线：升压 → 保压 → 降压（与常见耐压界面一致，接入 PLC 后由实时数据替换/追加）
        // 演示曲线：升压 → 保压 → 退卸（接入 PLC 后由 HD1000 / HD1032 实时数据替换）
        double[] t =
        {
            0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40,
            42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 62, 64, 66, 68, 70
        };
        foreach (var x in t)
        {
            double pressure;
            double displacement;
            if (x <= 28)
            {
                pressure = x / 28.0 * 18.0;
                displacement = x / 28.0 * 120.0;
            }
            else if (x <= 52)
            {
                pressure = 18.0;
                displacement = 120.0 + (x - 28) * 2.5;
            }
            else
            {
                pressure = 18.0 * (1 - (x - 52) / 18.0);
                displacement = 180.0 - (x - 52) * 4.0;
            }

            WithstandVoltagePoints.Add(new ObservablePoint(x, Math.Round(Math.Max(0, pressure), 2)));
            WithstandCurrentPoints.Add(new ObservablePoint(x, Math.Round(Math.Max(0, displacement), 1)));
        }
    }
// ============= 报表显示 =============

    [RelayCommand]
    private void ClearReportData()
    {
        ReportTargetPressure = "";
        ReportActualDisplacement = "";
        ReportDisplacementOffset = "";
        ReportDecelPressure = "";
        ReportHighPressureLimit = "";
        ReportReverseTime = "";
        ReportTestResult = "";
        ReportLoopOneWithstandVoltage = "";
        ReportLoopOneLeakageCurrent = "";
        ReportLoopOneTestResult = "";
        ReportLoopTwoWithstandVoltage = "";
        ReportLoopTwoLeakageCurrent = "";
        ReportLoopTwoTestResult = "";
        ReportLoopThreeWithstandVoltage = "";
        ReportLoopThreeLeakageCurrent = "";
        ReportLoopThreeTestResult = "";
        ReportLoopFourWithstandVoltage = "";
        ReportLoopFourLeakageCurrent = "";
        ReportLoopFourTestResult = "";
        ReportInsulationResistance = "";
        ReportTurnWithstand = "";
    }

    [RelayCommand]
    private void RefreshReportData()
    {
        try
        {
            string savedProductModel = ReportProductModel;
            string savedVehicleNumber = ReportVehicleNumber;
            string savedTestTime = ReportTestTime;
            
            var data = _reportService.ReadAllSheetData();
            Console.WriteLine($"[DEBUG] Total rows read: {data.Count}");
            
            ReportData.Clear();
            MaxColumns = 0;

            foreach (var row in data)
            {
                var rowData = new ObservableCollection<string>();
                foreach (var cell in row)
                {
                    rowData.Add(cell ?? string.Empty);
                }
                ReportData.Add(rowData);
                
                if (row.Count > MaxColumns)
                {
                    MaxColumns = row.Count;
                }
            }
            
            Console.WriteLine($"[DEBUG] ReportData has {ReportData.Count} rows, {MaxColumns} columns");
            
            UpdateReportTesterInfo();
            UpdateReportDisplayProperties();
            
            AppendTestProcessLog("报表数据已刷新");
            
            if (!string.IsNullOrEmpty(savedProductModel) || !string.IsNullOrEmpty(savedVehicleNumber))
            {
                UpdateReportVehicleInfo(savedProductModel, savedVehicleNumber);
            }
            
            if (!string.IsNullOrEmpty(savedTestTime))
            {
                UpdateReportTestTime(savedTestTime);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] RefreshReportData failed: {ex.Message}");
            AppendTestProcessLog($"报表刷新失败: {ex.Message}");
        }
    }

    private void UpdateReportTesterInfo()
    {
        string testerName = VarHelper.CurrentUserName ?? "";
        if (string.IsNullOrEmpty(testerName))
            return;
            
        Console.WriteLine($"[DEBUG] Updating report tester info: {testerName}");
        
        for (int i = 0; i < ReportData.Count; i++)
        {
            var row = ReportData[i];
            for (int j = 0; j < row.Count; j++)
            {
                if (row[j] == "试验员")
                {
                    if (j + 1 < row.Count)
                    {
                        row[j + 1] = testerName;
                        Console.WriteLine($"[DEBUG] Updated 试验员 value at row {i}, column {j + 1}");
                    }
                    else if (j + 1 == row.Count)
                    {
                        row.Add(testerName);
                        Console.WriteLine($"[DEBUG] Added 试验员 value at row {i}, column {j + 1}");
                    }
                }
            }
        }
    }

    public void UpdateReportVehicleInfo(string productModel, string vehicleNumber)
    {
        Console.WriteLine($"[DEBUG] Updating report vehicle info: ProductModel={productModel}, VehicleNumber={vehicleNumber}");
        
        ReportProductModel = productModel;
        ReportVehicleNumber = vehicleNumber;
        
        for (int i = 0; i < ReportData.Count; i++)
        {
            var row = ReportData[i];
            for (int j = 0; j < row.Count; j++)
            {
                if (row[j] == "车型型号")
                {
                    if (j + 1 < row.Count)
                    {
                        row[j + 1] = productModel;
                        Console.WriteLine($"[DEBUG] Updated 车型型号 value at row {i}, column {j + 1}");
                    }
                    else if (j + 1 == row.Count)
                    {
                        row.Add(productModel);
                        Console.WriteLine($"[DEBUG] Added 车型型号 value at row {i}, column {j + 1}");
                    }
                }
                else if (row[j] == "车型车号")
                {
                    if (j + 1 < row.Count)
                    {
                        row[j + 1] = vehicleNumber;
                        Console.WriteLine($"[DEBUG] Updated 车型车号 value at row {i}, column {j + 1}");
                    }
                    else if (j + 1 == row.Count)
                    {
                        row.Add(vehicleNumber);
                        Console.WriteLine($"[DEBUG] Added 车型车号 value at row {i}, column {j + 1}");
                    }
                }
            }
        }
        
        AppendTestProcessLog($"报表已更新：车型型号={productModel}，车型车号={vehicleNumber}");
    }
    private void UpdateReportTestTime()
    {
        string testTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        UpdateReportTestTime(testTime);
    }

    private void UpdateReportTestTime(string testTime)
    {
        Console.WriteLine($"[DEBUG] Updating report test time: {testTime}");

        for (int i = 0; i < ReportData.Count; i++)
        {
            var row = ReportData[i];
            for (int j = 0; j < row.Count; j++)
            {
                if (row[j] == "试验时间")
                {
                    if (j + 1 < row.Count)
                    {
                        row[j + 1] = testTime;
                        Console.WriteLine($"[DEBUG] Updated 试验时间 value at row {i}, column {j + 1}");
                    }
                    else if (j + 1 == row.Count)
                    {
                        row.Add(testTime);
                        Console.WriteLine($"[DEBUG] Added 试验时间 value at row {i}, column {j + 1}");
                    }
                }
            }
        }

        ReportTestTime = testTime;
        AppendTestProcessLog($"报表已更新试验时间：{testTime}");
    }

    private void UpdateReportDisplayProperties()
    {
        foreach (var row in ReportData)
        {
            for (int j = 0; j < row.Count; j++)
            {
                string key = row[j];
                string value = (j + 1 < row.Count) ? row[j + 1] : "";
                
                switch (key)
                {
                    case "车型型号":
                        ReportProductModel = value;
                        break;
                    case "车型车号":
                        ReportVehicleNumber = value;
                        break;
                    case "试验员":
                        ReportTesterName = value;
                        break;
                    case "试验时间":
                        ReportTestTime = value;
                        break;
                    case "设备编号":
                        ReportDeviceNumber = value;
                        break;
                    case "备注信息":
                        ReportRemarks = value;
                        break;
                    case "目标压力(MPa)":
                        ReportTargetPressure = value;
                        break;
                    case "实际位移(mm)":
                        ReportActualDisplacement = value;
                        break;
                    case "位移补充值(mm)":
                        ReportDisplacementOffset = value;
                        break;
                    case "开始减速压力(bar)":
                        ReportDecelPressure = value;
                        break;
                    case "高压浮动限制T":
                        ReportHighPressureLimit = value;
                        break;
                    case "反向时间(S)":
                        ReportReverseTime = value;
                        break;
                    case "测试结果":
                        ReportTestResult = value;
                        break;
                }
            }
        }
    }

}

/// <summary>
/// 任务明细项（在线 / Excel 模式都用这个）
/// </summary>
public class TaskDetailItem
{
    public int LocalDetailId { get; set; }
    public int LocalTaskRowId { get; set; }
    public string? HoldTaskId { get; set; }
    public string? HoldItemId { get; set; }
    public string? DetailId { get; set; }
    public string? StepName { get; set; }
    public string? StepContent { get; set; }
    public string? ProjectNumber { get; set; }
    public string? TrainNo { get; set; }
    public string? CarCode { get; set; }
    public string? ResultStandard { get; set; }
    public string? ResultUnit { get; set; }
    public bool IsTested { get; set; }
}
