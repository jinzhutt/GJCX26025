using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Data;
using PressureTestApp.Data.Entities;
using PressureTestApp.UI.Services;
using PressureTestApp.UI.Views;

namespace PressureTestApp.UI.ViewModels;

public partial class TestReportManagementViewModel : ObservableObject
{
    private readonly IReportService _reportService;
    private readonly KingbaseContext _kingbaseContext;

    [ObservableProperty] private string _productCategory = "耐压测试";
    [ObservableProperty] private string _productModelFilter = "车型1";
    [ObservableProperty] private string _vehicleNumberFilter = "";
    [ObservableProperty] private string _dateStartText = "2026-05-06";
    [ObservableProperty] private string _dateEndText = "2026-05-09";

    // 表单显示数据
    [ObservableProperty] private string _selectedProductModel = "";
    [ObservableProperty] private string _selectedVehicleNumber = "";
    [ObservableProperty] private string _selectedTesterName = "";
    [ObservableProperty] private string _selectedTestTime = "";
    [ObservableProperty] private string _selectedDeviceNumber = "";
    [ObservableProperty] private string _selectedRemarks = "";
    
    [ObservableProperty] private string _selectedLoopOneWithstandVoltage = "";
    [ObservableProperty] private string _selectedLoopOneLeakageCurrent = "";
    [ObservableProperty] private string _selectedLoopOneTestResult = "";
    [ObservableProperty] private string _selectedLoopTwoWithstandVoltage = "";
    [ObservableProperty] private string _selectedLoopTwoLeakageCurrent = "";
    [ObservableProperty] private string _selectedLoopTwoTestResult = "";
    [ObservableProperty] private string _selectedLoopThreeWithstandVoltage = "";
    [ObservableProperty] private string _selectedLoopThreeLeakageCurrent = "";
    [ObservableProperty] private string _selectedLoopThreeTestResult = "";
    [ObservableProperty] private string _selectedLoopFourWithstandVoltage = "";
    [ObservableProperty] private string _selectedLoopFourLeakageCurrent = "";
    [ObservableProperty] private string _selectedLoopFourTestResult = "";
    [ObservableProperty] private string _selectedInsulationResistance = "";
    [ObservableProperty] private string _selectedTurnWithstand = "";
    [ObservableProperty] private string _selectedTestResult = "";

    public ObservableCollection<RecordEntity> RecordList { get; } = new();
    [ObservableProperty] private RecordEntity? _selectedRecord;

    public Action? NavigateBackToMainTest { get; set; }

    public TestReportManagementViewModel(IReportService reportService, KingbaseContext kingbaseContext)
    {
        _reportService = reportService;
        _kingbaseContext = kingbaseContext;
        LoadRecordData();
    }

    private void LoadRecordData()
    {
        try
        {
            RecordList.Clear();
            var records = _kingbaseContext.Sql.Select<RecordEntity>().OrderByDescending(r => r.CreatedAt).ToList();
            foreach (var record in records)
            {
                RecordList.Add(record);
            }
            NlogHelper.Info($"从数据库加载了 {RecordList.Count} 条记录");
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"加载报告数据失败: {ex.Message}");
        }
    }

    partial void OnSelectedRecordChanged(RecordEntity? oldValue, RecordEntity? newValue)
    {
        if (newValue != null)
        {
            SelectedProductModel = newValue.ProductModel ?? "";
            SelectedVehicleNumber = newValue.VehicleNumber ?? "";
            SelectedTesterName = newValue.TesterName ?? "";
            SelectedTestTime = newValue.TestTime ?? "";
            SelectedDeviceNumber = newValue.DeviceNumber ?? "";
            SelectedRemarks = newValue.Remarks ?? "";
            
            SelectedLoopOneWithstandVoltage = newValue.LoopOneWithstandVoltage ?? "";
            SelectedLoopOneLeakageCurrent = newValue.LoopOneLeakageCurrent ?? "";
            SelectedLoopOneTestResult = newValue.LoopOneTestResult ?? "";
            SelectedLoopTwoWithstandVoltage = newValue.LoopTwoWithstandVoltage ?? "";
            SelectedLoopTwoLeakageCurrent = newValue.LoopTwoLeakageCurrent ?? "";
            SelectedLoopTwoTestResult = newValue.LoopTwoTestResult ?? "";
            SelectedLoopThreeWithstandVoltage = newValue.LoopThreeWithstandVoltage ?? "";
            SelectedLoopThreeLeakageCurrent = newValue.LoopThreeLeakageCurrent ?? "";
            SelectedLoopThreeTestResult = newValue.LoopThreeTestResult ?? "";
            SelectedLoopFourWithstandVoltage = newValue.LoopFourWithstandVoltage ?? "";
            SelectedLoopFourLeakageCurrent = newValue.LoopFourLeakageCurrent ?? "";
            SelectedLoopFourTestResult = newValue.LoopFourTestResult ?? "";
            SelectedInsulationResistance = newValue.InsulationResistance ?? "";
            SelectedTurnWithstand = newValue.TurnWithstand ?? "";
            SelectedTestResult = newValue.TestResult ?? "";
        }
        else
        {
            ClearForm();
        }
    }

    private void ClearForm()
    {
        SelectedProductModel = "";
        SelectedVehicleNumber = "";
        SelectedTesterName = "";
        SelectedTestTime = "";
        SelectedDeviceNumber = "";
        SelectedRemarks = "";
        
        SelectedLoopOneWithstandVoltage = "";
        SelectedLoopOneLeakageCurrent = "";
        SelectedLoopOneTestResult = "";
        SelectedLoopTwoWithstandVoltage = "";
        SelectedLoopTwoLeakageCurrent = "";
        SelectedLoopTwoTestResult = "";
        SelectedLoopThreeWithstandVoltage = "";
        SelectedLoopThreeLeakageCurrent = "";
        SelectedLoopThreeTestResult = "";
        SelectedLoopFourWithstandVoltage = "";
        SelectedLoopFourLeakageCurrent = "";
        SelectedLoopFourTestResult = "";
        SelectedInsulationResistance = "";
        SelectedTurnWithstand = "";
        SelectedTestResult = "";
    }

    [RelayCommand]
    private void Search()
    {
        NlogHelper.Info($"试验报表搜索 · 产品类={ProductCategory} · 型号={ProductModelFilter} · 时间 {DateStartText}–{DateEndText}");
        LoadRecordData();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadRecordData();
        NlogHelper.Info("报表数据已刷新");
    }

    [RelayCommand]
    private void PrintReport()
    {
        try
        {
            if (SelectedRecord != null && !string.IsNullOrEmpty(SelectedRecord.ReportPath))
            {
                var reportPath = SelectedRecord.ReportPath;
                if (File.Exists(reportPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(reportPath)
                    {
                        UseShellExecute = true,
                        Verb = "print"
                    });
                    NlogHelper.Info($"打印报表: {reportPath}");
                }
                else
                {
                    NlogHelper.Warn($"报表文件不存在: {reportPath}");
                }
            }
            else
            {
                NlogHelper.Warn("未选择报表或报表路径为空");
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"打印报表失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void AddReport()
    {
        try
        {
            var newReport = new Dictionary<string, object>
            {
                { "ProductModel", ProductModelFilter },
                { "VehicleNumber", VehicleNumberFilter },
                { "TesterName", "操作员" },
                { "TestTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "DeviceNumber", "" },
                { "Remarks", "" },
                { "LoopOneWithstandVoltage", "" },
                { "LoopOneLeakageCurrent", "" },
                { "LoopOneTestResult", "" },
                { "LoopTwoWithstandVoltage", "" },
                { "LoopTwoLeakageCurrent", "" },
                { "LoopTwoTestResult", "" },
                { "LoopThreeWithstandVoltage", "" },
                { "LoopThreeLeakageCurrent", "" },
                { "LoopThreeTestResult", "" },
                { "LoopFourWithstandVoltage", "" },
                { "LoopFourLeakageCurrent", "" },
                { "LoopFourTestResult", "" },
                { "InsulationResistance", "100MΩ" },
                { "TurnWithstand", "合格" },
                { "TestResult", "合格" }
            };

            _reportService.UpdateReportData(newReport);
            LoadRecordData();
            NlogHelper.Info("报表数据已添加");
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"添加报表数据失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Delete()
    {
        NlogHelper.Info("删除选中报表（占位）");
    }

    [RelayCommand]
    private void Exit() => NavigateBackToMainTest?.Invoke();
}

public partial class ReportListRow : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _productType = "";
    [ObservableProperty] private string _productModel = "";
    [ObservableProperty] private string _vehicleNumber = "";
    [ObservableProperty] private string _testerName = "";
    [ObservableProperty] private string _testTimeText = "";
}