using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressureTestApp.Core.Helpers;
using PressureTestApp.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO.Packaging;

namespace PressureTestApp.UI.ViewModels;

public partial class ReportEditorViewModel : ObservableObject
{
    private readonly IReportService _reportService;

    [ObservableProperty] private string _productModel = "";
    [ObservableProperty] private string _vehicleNumber = "";
    [ObservableProperty] private string _testerName = "";
    [ObservableProperty] private string _testTime = "";
    [ObservableProperty] private string _deviceNumber = "";
    [ObservableProperty] private string _remarks = "";
    [ObservableProperty] private string _loopOneWithstandVoltage = "";
    [ObservableProperty] private string _loopOneLeakageCurrent = "";
    [ObservableProperty] private string _loopOneTestResult = "";
    [ObservableProperty] private string _loopTwoWithstandVoltage = "";
    [ObservableProperty] private string _loopTwoLeakageCurrent = "";
    [ObservableProperty] private string _loopTwoTestResult = "";
    [ObservableProperty] private string _loopThreeWithstandVoltage = "";
    [ObservableProperty] private string _loopThreeLeakageCurrent = "";
    [ObservableProperty] private string _loopThreeTestResult = "";
    [ObservableProperty] private string _loopFourWithstandVoltage = "";
    [ObservableProperty] private string _loopFourLeakageCurrent = "";
    [ObservableProperty] private string _loopFourTestResult = "";
    [ObservableProperty] private string _insulationResistance = "";
    [ObservableProperty] private string _turnWithstand = "";
    [ObservableProperty] private string _testResult = "合格";

    public ObservableCollection<ReportDataRow> ReportRows { get; } = new();

    public Action? CloseWindow { get; set; }

    public ReportEditorViewModel(IReportService reportService)
    {
        _reportService = reportService;
        LoadReportData();
    }

    private void LoadReportData()
    {
        try
        {
            ReportRows.Clear();
            var data = _reportService.ReadReportData();
            
            foreach (var item in data.Values)
            {
                if (item is System.Collections.Generic.Dictionary<string, object> rowData)
                {
                    ReportRows.Add(new ReportDataRow
                    {
                        ProductModel = rowData.TryGetValue("ProductModel", out var model) ? model?.ToString() ?? "" : "",
                        VehicleNumber = rowData.TryGetValue("VehicleNumber", out var vehicle) ? vehicle?.ToString() ?? "" : "",
                        TesterName = rowData.TryGetValue("TesterName", out var tester) ? tester?.ToString() ?? "" : "",
                        TestTime = rowData.TryGetValue("TestTime", out var time) ? time?.ToString() ?? "" : "",
                        DeviceNumber = rowData.TryGetValue("DeviceNumber", out var device) ? device?.ToString() ?? "" : "",
                        Remarks = rowData.TryGetValue("Remarks", out var remark) ? remark?.ToString() ?? "" : "",
                        LoopOneWithstandVoltage = rowData.TryGetValue("LoopOneWithstandVoltage", out var loopOneVoltage) ? loopOneVoltage?.ToString() ?? "" : "",
                        LoopOneLeakageCurrent = rowData.TryGetValue("LoopOneLeakageCurrent", out var loopOneLeakage) ? loopOneLeakage?.ToString() ?? "" : "",
                        LoopOneTestResult = rowData.TryGetValue("LoopOneTestResult", out var loopOneResult) ? loopOneResult?.ToString() ?? "" : "",
                        LoopTwoWithstandVoltage = rowData.TryGetValue("LoopTwoWithstandVoltage", out var loopTwoVoltage) ? loopTwoVoltage?.ToString() ?? "" : "",
                        LoopTwoLeakageCurrent = rowData.TryGetValue("LoopTwoLeakageCurrent", out var loopTwoLeakage) ? loopTwoLeakage?.ToString() ?? "" : "",
                        LoopTwoTestResult = rowData.TryGetValue("LoopTwoTestResult", out var loopTwoResult) ? loopTwoResult?.ToString() ?? "" : "",
                        LoopThreeWithstandVoltage = rowData.TryGetValue("LoopThreeWithstandVoltage", out var loopThreeVoltage) ? loopThreeVoltage?.ToString() ?? "" : "",
                        LoopThreeLeakageCurrent = rowData.TryGetValue("LoopThreeLeakageCurrent", out var loopThreeLeakage) ? loopThreeLeakage?.ToString() ?? "" : "",
                        LoopThreeTestResult = rowData.TryGetValue("LoopThreeTestResult", out var loopThreeResult) ? loopThreeResult?.ToString() ?? "" : "",
                        LoopFourWithstandVoltage = rowData.TryGetValue("LoopFourWithstandVoltage", out var loopFourVoltage) ? loopFourVoltage?.ToString() ?? "" : "",
                        LoopFourLeakageCurrent = rowData.TryGetValue("LoopFourLeakageCurrent", out var loopFourLeakage) ? loopFourLeakage?.ToString() ?? "" : "",
                        LoopFourTestResult = rowData.TryGetValue("LoopFourTestResult", out var loopFourResult) ? loopFourResult?.ToString() ?? "" : "",
                        InsulationResistance = rowData.TryGetValue("InsulationResistance", out var resistance) ? resistance?.ToString() ?? "" : "",
                        TurnWithstand = rowData.TryGetValue("TurnWithstand", out var turn) ? turn?.ToString() ?? "" : "",
                        TestResult = rowData.TryGetValue("TestResult", out var result) ? result?.ToString() ?? "" : ""
                    });
                }
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"加载报告数据失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void AddRow()
    {
        try
        {
            var newRow = new ReportDataRow
            {
                ProductModel = ProductModel,
                VehicleNumber = VehicleNumber,
                TesterName = TesterName,
                TestTime = string.IsNullOrEmpty(TestTime) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : TestTime,
                DeviceNumber = DeviceNumber,
                Remarks = Remarks,
                LoopOneWithstandVoltage = LoopOneWithstandVoltage,
                LoopOneLeakageCurrent = LoopOneLeakageCurrent,
                LoopOneTestResult = LoopOneTestResult,
                LoopTwoWithstandVoltage = LoopTwoWithstandVoltage,
                LoopTwoLeakageCurrent = LoopTwoLeakageCurrent,
                LoopTwoTestResult = LoopTwoTestResult,
                LoopThreeWithstandVoltage = LoopThreeWithstandVoltage,
                LoopThreeLeakageCurrent = LoopThreeLeakageCurrent,
                LoopThreeTestResult = LoopThreeTestResult,
                LoopFourWithstandVoltage = LoopFourWithstandVoltage,
                LoopFourLeakageCurrent = LoopFourLeakageCurrent,
                LoopFourTestResult = LoopFourTestResult,
                InsulationResistance = InsulationResistance,
                TurnWithstand = TurnWithstand,
                TestResult = TestResult
            };

            ReportRows.Add(newRow);

            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                { "ProductModel", newRow.ProductModel },
                { "VehicleNumber", newRow.VehicleNumber },
                { "TesterName", newRow.TesterName },
                { "TestTime", newRow.TestTime },
                { "DeviceNumber", newRow.DeviceNumber },
                { "Remarks", newRow.Remarks },
                { "LoopOneWithstandVoltage", newRow.LoopOneWithstandVoltage },
                { "LoopOneLeakageCurrent", newRow.LoopOneLeakageCurrent },
                { "LoopOneTestResult", newRow.LoopOneTestResult },
                { "LoopTwoWithstandVoltage", newRow.LoopTwoWithstandVoltage },
                { "LoopTwoLeakageCurrent", newRow.LoopTwoLeakageCurrent },
                { "LoopTwoTestResult", newRow.LoopTwoTestResult },
                { "LoopThreeWithstandVoltage", newRow.LoopThreeWithstandVoltage },
                { "LoopThreeLeakageCurrent", newRow.LoopThreeLeakageCurrent },
                { "LoopThreeTestResult", newRow.LoopThreeTestResult },
                { "LoopFourWithstandVoltage", newRow.LoopFourWithstandVoltage },
                { "LoopFourLeakageCurrent", newRow.LoopFourLeakageCurrent },
                { "LoopFourTestResult", newRow.LoopFourTestResult },
                { "InsulationResistance", newRow.InsulationResistance },
                { "TurnWithstand", newRow.TurnWithstand },
                { "TestResult", newRow.TestResult }
            };

            _reportService.UpdateReportData(data);
            ClearInputFields();

            NlogHelper.Info("报告数据已添加");
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"添加报告数据失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveAll()
    {
        try
        {
            foreach (var row in ReportRows)
            {
                var data = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "ProductModel", row.ProductModel },
                    { "VehicleNumber", row.VehicleNumber },
                    { "TesterName", row.TesterName },
                    { "TestTime", row.TestTime },
                    { "DeviceNumber", row.DeviceNumber },
                    { "Remarks", row.Remarks },
                    { "LoopOneWithstandVoltage", row.LoopOneWithstandVoltage },
                    { "LoopOneLeakageCurrent", row.LoopOneLeakageCurrent },
                    { "LoopOneTestResult", row.LoopOneTestResult },
                    { "LoopTwoWithstandVoltage", row.LoopTwoWithstandVoltage },
                    { "LoopTwoLeakageCurrent", row.LoopTwoLeakageCurrent },
                    { "LoopTwoTestResult", row.LoopTwoTestResult },
                    { "LoopThreeWithstandVoltage", row.LoopThreeWithstandVoltage },
                    { "LoopThreeLeakageCurrent", row.LoopThreeLeakageCurrent },
                    { "LoopThreeTestResult", row.LoopThreeTestResult },
                    { "LoopFourWithstandVoltage", row.LoopFourWithstandVoltage },
                    { "LoopFourLeakageCurrent", row.LoopFourLeakageCurrent },
                    { "LoopFourTestResult", row.LoopFourTestResult },
                    { "InsulationResistance", row.InsulationResistance },
                    { "TurnWithstand", row.TurnWithstand },
                    { "TestResult", row.TestResult }
                };
                _reportService.UpdateReportData(data);
            }
            LoadReportData();
            NlogHelper.Info("所有报告数据已保存到模板文件");
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"保存报告数据失败: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ExportReport()
    {
        try
        {
            if (string.IsNullOrEmpty(ProductModel) || string.IsNullOrEmpty(VehicleNumber))
            {
                NlogHelper.Warn("导出失败：车型型号和车型车号不能为空");
                return;
            }

            string exportPath = _reportService.ExportToFile(
                ProductModel,
                VehicleNumber,
                TesterName,
                TestTime,
                DeviceNumber,
                Remarks,
                LoopOneWithstandVoltage,
                LoopOneLeakageCurrent,
                LoopOneTestResult,
                LoopTwoWithstandVoltage,
                LoopTwoLeakageCurrent,
                LoopTwoTestResult,
                LoopThreeWithstandVoltage,
                LoopThreeLeakageCurrent,
                LoopThreeTestResult,
                LoopFourWithstandVoltage,
                LoopFourLeakageCurrent,
                LoopFourTestResult,
                InsulationResistance,
                TurnWithstand,
                TestResult
            );

            //_reportService.Write("CXXH", ProductModel);
            NlogHelper.Info($"报告已导出到: {exportPath}");
        }
        catch (Exception ex)
        {
            NlogHelper.Error($"导出报告失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadReportData();
    }

    [RelayCommand]
    private void Close()
    {
        CloseWindow?.Invoke();
    }

    private void ClearInputFields()
    {
        ProductModel = "";
        VehicleNumber = "";
        TesterName = "";
        TestTime = "";
        DeviceNumber = "";
        Remarks = "";
        LoopOneWithstandVoltage = "";
        LoopOneLeakageCurrent = "";
        LoopOneTestResult = "";
        LoopTwoWithstandVoltage = "";
        LoopTwoLeakageCurrent = "";
        LoopTwoTestResult = "";
        LoopThreeWithstandVoltage = "";
        LoopThreeLeakageCurrent = "";
        LoopThreeTestResult = "";
        LoopFourWithstandVoltage = "";
        LoopFourLeakageCurrent = "";
        LoopFourTestResult = "";
        InsulationResistance = "";
        TurnWithstand = "";
        TestResult = "合格";
    }
}

public partial class ReportDataRow : ObservableObject
{
    [ObservableProperty] private string _productModel = "";
    [ObservableProperty] private string _vehicleNumber = "";
    [ObservableProperty] private string _testerName = "";
    [ObservableProperty] private string _testTime = "";
    [ObservableProperty] private string _deviceNumber = "";
    [ObservableProperty] private string _remarks = "";
    [ObservableProperty] private string _loopOneWithstandVoltage = "";
    [ObservableProperty] private string _loopOneLeakageCurrent = "";
    [ObservableProperty] private string _loopOneTestResult = "";
    [ObservableProperty] private string _loopTwoWithstandVoltage = "";
    [ObservableProperty] private string _loopTwoLeakageCurrent = "";
    [ObservableProperty] private string _loopTwoTestResult = "";
    [ObservableProperty] private string _loopThreeWithstandVoltage = "";
    [ObservableProperty] private string _loopThreeLeakageCurrent = "";
    [ObservableProperty] private string _loopThreeTestResult = "";
    [ObservableProperty] private string _loopFourWithstandVoltage = "";
    [ObservableProperty] private string _loopFourLeakageCurrent = "";
    [ObservableProperty] private string _loopFourTestResult = "";
    [ObservableProperty] private string _insulationResistance = "";
    [ObservableProperty] private string _turnWithstand = "";
    [ObservableProperty] private string _testResult = "";
}