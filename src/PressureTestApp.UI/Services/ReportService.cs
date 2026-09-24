using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PressureTestApp.UI.Services;

public interface IReportService
{
    bool ReportExists();
    void CreateReportIfNotExists();
    void UpdateReportData(Dictionary<string, object> data);
    Dictionary<string, object> ReadReportData();
    string ExportToFile(string productModel, string vehicleNumber, string testerName = "", string testTime = "", string deviceNumber = "", string remarks = "", string loopOneWithstandVoltage = "", string loopOneLeakageCurrent = "", string loopOneTestResult = "", string loopTwoWithstandVoltage = "", string loopTwoLeakageCurrent = "", string loopTwoTestResult = "", string loopThreeWithstandVoltage = "", string loopThreeLeakageCurrent = "", string loopThreeTestResult = "", string loopFourWithstandVoltage = "", string loopFourLeakageCurrent = "", string loopFourTestResult = "", string insulationResistance = "", string turnWithstand = "", string testResult = "");
    void Write(string cellReference, string value);
    List<List<string>> ReadAllSheetData();
    void OpenWithLibreOffice();
}

public class ReportService : IReportService
{
    private readonly string _reportPath;

    public ReportService()
    {
        string baseDir = AppContext.BaseDirectory;
        string reportDir = Path.Combine(baseDir, "report");
        
        if (!Directory.Exists(reportDir))
        {
            Directory.CreateDirectory(reportDir);
        }
        
        string templatePath = Path.Combine(reportDir, "report_.xlsx");
        
        if (File.Exists(templatePath))
        {
            _reportPath = templatePath;
            Console.WriteLine($"[DEBUG] Using template file directly: {_reportPath}");
        }
        else
        {
            _reportPath = Path.Combine(reportDir, "report.xlsx");
            Console.WriteLine($"[DEBUG] Using working file: {_reportPath}");
        }
        
        Console.WriteLine($"[DEBUG] Report path: {_reportPath}, Exists: {File.Exists(_reportPath)}");
    }

    private string FindValidReportFile(string reportDir)
    {
        Console.WriteLine($"[DEBUG] Searching for report files in: {reportDir}");
        
        string[] possibleFiles = {
            "report_.xlsx",
            "report111.xlsx",
            "report_0530.xlsx",
            "report_backup.xlsx",
            "report - 副本.xlsx",
            "report.xlsx"
        };
        
        foreach (string fileName in possibleFiles)
        {
            string filePath = Path.Combine(reportDir, fileName);
            if (File.Exists(filePath))
            {
                bool isValid = IsValidExcelFile(filePath);
                Console.WriteLine($"[DEBUG] File: {fileName}, Size: {new FileInfo(filePath).Length} bytes, Valid: {isValid}");
                if (isValid)
                {
                    Console.WriteLine($"[DEBUG] Found valid report file: {fileName}");
                    return filePath;
                }
            }
            else
            {
                Console.WriteLine($"[DEBUG] File not found: {fileName}");
            }
        }
        
        return Path.Combine(reportDir, "report.xlsx");
    }

    private void RestoreFromBackup()
    {
        string reportDir = Path.GetDirectoryName(_reportPath);
        string[] backupFiles = Directory.GetFiles(reportDir, "report_*.xlsx");
        
        foreach (string backupFile in backupFiles)
        {
            try
            {
                if (IsValidExcelFile(backupFile))
                {
                    File.Copy(backupFile, _reportPath, true);
                    Console.WriteLine($"[DEBUG] Restored from backup: {backupFile}");
                    return;
                }
            }
            catch
            {
                continue;
            }
        }
        
        File.Delete(_reportPath);
        CreateReportIfNotExists();
        Console.WriteLine("[DEBUG] Created new report file since no valid backup found");
    }

    public bool ReportExists()
    {
        bool exists = File.Exists(_reportPath);
        Console.WriteLine($"[DEBUG] Report path: {_reportPath}, Exists: {exists}");
        return exists;
    }

    public void CreateReportIfNotExists()
    {
        var reportDir = Path.GetDirectoryName(_reportPath);
        if (!Directory.Exists(reportDir))
        {
            Directory.CreateDirectory(reportDir);
        }

        // 只在文件不存在时创建新模板，保留用户原有的模板及其命名范围
        if (!File.Exists(_reportPath))
        {
            using (SpreadsheetDocument package = SpreadsheetDocument.Create(_reportPath, SpreadsheetDocumentType.Workbook))
            {
            WorkbookPart workbookPart = package.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            Sheets sheets = package.WorkbookPart.Workbook.AppendChild(new Sheets());
            Sheet sheet = new Sheet()
            {
                Id = package.WorkbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "试验报告"
            };
            sheets.Append(sheet);

            DefinedNames definedNames = new DefinedNames();
            
            definedNames.Append(new DefinedName { Name = "CXXH", Text = "='试验报告'!$C$2" });
            definedNames.Append(new DefinedName { Name = "CXCH", Text = "='试验报告'!$D$2" });
            definedNames.Append(new DefinedName { Name = "SY员", Text = "='试验报告'!$F$2" });
            definedNames.Append(new DefinedName { Name = "SY时间", Text = "='试验报告'!$B$3" });
            definedNames.Append(new DefinedName { Name = "SB编号", Text = "='试验报告'!$D$3" });
            definedNames.Append(new DefinedName { Name = "BZXX", Text = "='试验报告'!$F$3" });
            definedNames.Append(new DefinedName { Name = "sydy1", Text = "='试验报告'!$D$7" });
            definedNames.Append(new DefinedName { Name = "sydl1", Text = "='试验报告'!$E$7" });
            definedNames.Append(new DefinedName { Name = "result1", Text = "='试验报告'!$F$7" });
            definedNames.Append(new DefinedName { Name = "sydy2", Text = "='试验报告'!$D$8" });
            definedNames.Append(new DefinedName { Name = "sydl2", Text = "='试验报告'!$E$8" });
            definedNames.Append(new DefinedName { Name = "result2", Text = "='试验报告'!$F$8" });
            definedNames.Append(new DefinedName { Name = "sydy3", Text = "='试验报告'!$D$9" });
            definedNames.Append(new DefinedName { Name = "sydl3", Text = "='试验报告'!$E$9" });
            definedNames.Append(new DefinedName { Name = "result3", Text = "='试验报告'!$F$9" });
            definedNames.Append(new DefinedName { Name = "sydy4", Text = "='试验报告'!$D$10" });
            definedNames.Append(new DefinedName { Name = "sydl4", Text = "='试验报告'!$E$10" });
            definedNames.Append(new DefinedName { Name = "result4", Text = "='试验报告'!$F$10" });
            definedNames.Append(new DefinedName { Name = "JY电阻", Text = "='试验报告'!$D$11" });
            definedNames.Append(new DefinedName { Name = "ZJNY", Text = "='试验报告'!$D$12" });
            definedNames.Append(new DefinedName { Name = "测试结果", Text = "='试验报告'!$F$11" });
            
            workbookPart.Workbook.AppendChild(definedNames);

            SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

            Row headerRow = new Row();
            string[] headers = { "车型型号", "车型车号", "试验员", "试验时间", "设备编号", "备注信息", "回路一试验电压(KV)", "回路一泄漏电流(mA)", "回路一测试结果", "回路二试验电压(KV)", "回路二泄漏电流(mA)", "回路二测试结果", "", "回路三试验电压(KV)", "回路三泄漏电流(mA)", "回路三测试结果", "回路四试验电压(KV)", "回路四泄漏电流(mA)", "回路四测试结果", "绝缘电阻", "匝间耐压", "测试结果" };
            
            foreach (string header in headers)
            {
                Cell cell = new Cell();
                cell.DataType = CellValues.String;
                cell.CellValue = new CellValue(header);
                headerRow.Append(cell);
            }
            sheetData.Append(headerRow);

            workbookPart.Workbook.Save();
            }
        }
    }

    public void UpdateReportData(Dictionary<string, object> data)
    {
        CreateReportIfNotExists();

        using (SpreadsheetDocument package = SpreadsheetDocument.Open(_reportPath, true))
        {
            WorkbookPart workbookPart = package.WorkbookPart;
            WorksheetPart worksheetPart = workbookPart.WorksheetParts.FirstOrDefault();
            
            if (worksheetPart == null)
            {
                worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData());
                Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
                Sheet sheet = new Sheet()
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "试验报告"
                };
                sheets.Append(sheet);
            }

            SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            
            Row newRow = new Row();
            
            newRow.Append(CreateCell(data.TryGetValue("ProductModel", out var model) ? model.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("VehicleNumber", out var vehicle) ? vehicle.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("TesterName", out var tester) ? tester.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("TestTime", out var time) ? time.ToString() : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            newRow.Append(CreateCell(data.TryGetValue("DeviceNumber", out var device) ? device.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("Remarks", out var remark) ? remark.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopOneWithstandVoltage", out var loopOneVoltage) ? loopOneVoltage.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopOneLeakageCurrent", out var loopOneLeakage) ? loopOneLeakage.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopOneTestResult", out var loopOneResult) ? loopOneResult.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopTwoWithstandVoltage", out var loopTwoVoltage) ? loopTwoVoltage.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopTwoLeakageCurrent", out var loopTwoLeakage) ? loopTwoLeakage.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopTwoTestResult", out var loopTwoResult) ? loopTwoResult.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopThreeWithstandVoltage", out var loopThreeVoltage) ? loopThreeVoltage.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopThreeLeakageCurrent", out var loopThreeLeakage) ? loopThreeLeakage.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopThreeTestResult", out var loopThreeResult) ? loopThreeResult.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopFourWithstandVoltage", out var loopFourVoltage) ? loopFourVoltage.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopFourLeakageCurrent", out var loopFourLeakage) ? loopFourLeakage.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("LoopFourTestResult", out var loopFourResult) ? loopFourResult.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("InsulationResistance", out var resistance) ? resistance.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("TurnWithstand", out var turn) ? turn.ToString() : string.Empty));
            newRow.Append(CreateCell(data.TryGetValue("TestResult", out var result) ? result.ToString() : string.Empty));

            sheetData.Append(newRow);

            workbookPart.Workbook.Save();
        }
    }

    public Dictionary<string, object> ReadReportData()
    {
        if (!ReportExists())
        {
            return new Dictionary<string, object>();
        }

        var data = new Dictionary<string, object>();

        using (SpreadsheetDocument package = SpreadsheetDocument.Open(_reportPath, false))
        {
            WorkbookPart workbookPart = package.WorkbookPart;
            WorksheetPart worksheetPart = workbookPart.WorksheetParts.FirstOrDefault();
            
            if (worksheetPart == null)
            {
                return data;
            }

            SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            if (sheetData == null)
            {
                return data;
            }

            int rowIndex = 0;
            foreach (Row row in sheetData.Elements<Row>())
            {
                if (rowIndex == 0)
                {
                    rowIndex++;
                    continue;
                }

                var rowData = new Dictionary<string, object>();
                Cell[] cells = row.Elements<Cell>().ToArray();
                
                rowData["ProductModel"] = GetCellValue(cells, 0);
                rowData["VehicleNumber"] = GetCellValue(cells, 1);
                rowData["TesterName"] = GetCellValue(cells, 2);
                rowData["TestTime"] = GetCellValue(cells, 3);
                rowData["DeviceNumber"] = GetCellValue(cells, 4);
                rowData["Remarks"] = GetCellValue(cells, 5);
                rowData["LoopOneWithstandVoltage"] = GetCellValue(cells, 6);
                rowData["LoopOneLeakageCurrent"] = GetCellValue(cells, 7);
                rowData["LoopOneTestResult"] = GetCellValue(cells, 8);
                rowData["LoopTwoWithstandVoltage"] = GetCellValue(cells, 9);
                rowData["LoopTwoLeakageCurrent"] = GetCellValue(cells, 10);
                rowData["LoopTwoTestResult"] = GetCellValue(cells, 11);
                rowData["LoopThreeWithstandVoltage"] = GetCellValue(cells, 12);
                rowData["LoopThreeLeakageCurrent"] = GetCellValue(cells, 13);
                rowData["LoopThreeTestResult"] = GetCellValue(cells, 14);
                rowData["LoopFourWithstandVoltage"] = GetCellValue(cells, 15);
                rowData["LoopFourLeakageCurrent"] = GetCellValue(cells, 16);
                rowData["LoopFourTestResult"] = GetCellValue(cells, 17);
                rowData["InsulationResistance"] = GetCellValue(cells, 18);
                rowData["TurnWithstand"] = GetCellValue(cells, 19);
                rowData["TestResult"] = GetCellValue(cells, 20);

                data[$"Row{rowIndex}"] = rowData;
                rowIndex++;
            }
        }

        return data;
    }

    public string ExportToFile(string productModel, string vehicleNumber, string testerName = "", string testTime = "", string deviceNumber = "", string remarks = "", string loopOneWithstandVoltage = "", string loopOneLeakageCurrent = "", string loopOneTestResult = "", string loopTwoWithstandVoltage = "", string loopTwoLeakageCurrent = "", string loopTwoTestResult = "", string loopThreeWithstandVoltage = "", string loopThreeLeakageCurrent = "", string loopThreeTestResult = "", string loopFourWithstandVoltage = "", string loopFourLeakageCurrent = "", string loopFourTestResult = "", string insulationResistance = "", string turnWithstand = "", string testResult = "")
    {
        CreateReportIfNotExists();

        var saveDir = Path.Combine(AppContext.BaseDirectory, "save");
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{productModel}_{vehicleNumber}_{timestamp}.xlsx";
        var exportPath = Path.Combine(saveDir, fileName);

        if (IsValidExcelFile(_reportPath))
        {
            File.Copy(_reportPath, exportPath, true);
        }
        else
        {
            CreateNewReportFile(exportPath);
        }

        using (SpreadsheetDocument package = SpreadsheetDocument.Open(exportPath, true))
        {
            WorkbookPart workbookPart = package.WorkbookPart;
            WorksheetPart worksheetPart = workbookPart.WorksheetParts.FirstOrDefault();
            
            if (worksheetPart != null)
            {
                UpdateCellValue(package, "CXXH", productModel);
                UpdateCellValue(package, "CXCH", vehicleNumber);
                UpdateCellValue(package, "SYY", testerName);
                UpdateCellValue(package, "SYSJ", string.IsNullOrEmpty(testTime) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : testTime);
                UpdateCellValue(package, "SBBH", deviceNumber);
                UpdateCellValue(package, "BZ", remarks);
                UpdateCellValue(package, "sydy1", loopOneWithstandVoltage);
                UpdateCellValue(package, "sydl1", loopOneLeakageCurrent);
                UpdateCellValue(package, "result1", loopOneTestResult);
                UpdateCellValue(package, "sydy2", loopTwoWithstandVoltage);
                UpdateCellValue(package, "sydl2", loopTwoLeakageCurrent);
                UpdateCellValue(package, "result2", loopTwoTestResult);
                UpdateCellValue(package, "sydy3", loopThreeWithstandVoltage);
                UpdateCellValue(package, "sydl3", loopThreeLeakageCurrent);
                UpdateCellValue(package, "result3", loopThreeTestResult);
                UpdateCellValue(package, "sydy4", loopFourWithstandVoltage);
                UpdateCellValue(package, "sydl4", loopFourLeakageCurrent);
                UpdateCellValue(package, "result4", loopFourTestResult);
                UpdateCellValue(package, "JY电阻", insulationResistance);
                UpdateCellValue(package, "ZJNY", turnWithstand);
                UpdateCellValue(package, "测试结果", testResult);
            }

            workbookPart.Workbook.Save();
        }

        return exportPath;
    }

    private void UpdateCellValue(SpreadsheetDocument package, string cellReference, string value)
    {
        string actualCellReference = ResolveNamedRange(package, cellReference);
        
        WorkbookPart workbookPart = package.WorkbookPart;
        WorksheetPart worksheetPart = workbookPart.WorksheetParts.FirstOrDefault();
        
        if (worksheetPart == null)
            return;

        SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        if (sheetData == null)
            return;

        foreach (Row row in sheetData.Elements<Row>())
        {
            foreach (Cell cell in row.Elements<Cell>())
            {
                if (cell.CellReference != null && cell.CellReference.Value.Equals(actualCellReference, StringComparison.OrdinalIgnoreCase))
                {
                    cell.CellValue = new CellValue(value);
                    cell.DataType = CellValues.String;
                    return;
                }
            }
        }

        int rowIndex = GetRowIndex(actualCellReference);
        
        Row targetRow = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex == rowIndex);
        if (targetRow == null)
        {
            targetRow = new Row { RowIndex = (UInt32)rowIndex };
            sheetData.Append(targetRow);
        }

        Cell newCell = new Cell
        {
            CellReference = actualCellReference,
            DataType = CellValues.String,
            CellValue = new CellValue(value)
        };
        
        targetRow.Append(newCell);
    }

    private string ResolveNamedRange(SpreadsheetDocument package, string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        //if (IsValidCellReference(name))
        //    return name;

        WorkbookPart workbookPart = package.WorkbookPart;
        if (workbookPart.Workbook.DefinedNames != null)
        {
            foreach (DefinedName definedName in workbookPart.Workbook.DefinedNames)
            {
                if (definedName.Name != null && definedName.Name.Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    string formula = definedName.Text;
                    string cellRef = ExtractCellReferenceFromFormula(formula);
                    if (!string.IsNullOrEmpty(cellRef))
                        return cellRef;
                }
            }
        }

        return name;
    }

    private bool IsValidCellReference(string reference)
    {
        if (string.IsNullOrEmpty(reference))
            return false;
        
        int i = 0;
        while (i < reference.Length && char.IsLetter(reference[i]))
        {
            i++;
        }
        
        if (i == 0)
            return false;
        
        while (i < reference.Length && char.IsDigit(reference[i]))
        {
            i++;
        }
        
        return i == reference.Length;
    }

    private string ExtractCellReferenceFromFormula(string formula)
    {
        if (string.IsNullOrEmpty(formula))
            return null;

        formula = formula.Trim();
        
        if (formula.StartsWith("="))
            formula = formula.Substring(1);

        int exclamationIndex = formula.IndexOf('!');
        if (exclamationIndex >= 0)
        {
            formula = formula.Substring(exclamationIndex + 1);
        }

        Match match = System.Text.RegularExpressions.Regex.Match(formula, @"\$?[A-Za-z]+\$?\d+");
        if (match.Success)
        {
            string cellRef = match.Value;
            return cellRef.Replace("$", "");
        }

        return null;
    }

    private int GetRowIndex(string cellReference)
    {
        string rowPart = new string(cellReference.Where(c => char.IsDigit(c)).ToArray());
        return int.TryParse(rowPart, out int index) ? index : 1;
    }

    private string GetColumnName(string cellReference)
    {
        return new string(cellReference.Where(c => char.IsLetter(c)).ToArray());
    }

    private bool IsValidExcelFile(string filePath)
    {
        try
        {
            using (SpreadsheetDocument.Open(filePath, false))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private void CreateNewReportFile(string filePath)
    {
        using (SpreadsheetDocument package = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbookPart = package.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            Sheets sheets = package.WorkbookPart.Workbook.AppendChild(new Sheets());
            Sheet sheet = new Sheet()
            {
                Id = package.WorkbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "试验报告"
            };
            sheets.Append(sheet);

            DefinedNames definedNames = new DefinedNames();
            
            definedNames.Append(new DefinedName { Name = "CXXH", Text = "='试验报告'!$C$2" });
            definedNames.Append(new DefinedName { Name = "CXCH", Text = "='试验报告'!$D$2" });
            definedNames.Append(new DefinedName { Name = "SY员", Text = "='试验报告'!$F$2" });
            definedNames.Append(new DefinedName { Name = "SY时间", Text = "='试验报告'!$B$3" });
            definedNames.Append(new DefinedName { Name = "SB编号", Text = "='试验报告'!$D$3" });
            definedNames.Append(new DefinedName { Name = "BZXX", Text = "='试验报告'!$F$3" });
            definedNames.Append(new DefinedName { Name = "sydy1", Text = "='试验报告'!$D$7" });
            definedNames.Append(new DefinedName { Name = "sydl1", Text = "='试验报告'!$E$7" });
            definedNames.Append(new DefinedName { Name = "result1", Text = "='试验报告'!$F$7" });
            definedNames.Append(new DefinedName { Name = "sydy2", Text = "='试验报告'!$D$8" });
            definedNames.Append(new DefinedName { Name = "sydl2", Text = "='试验报告'!$E$8" });
            definedNames.Append(new DefinedName { Name = "result2", Text = "='试验报告'!$F$8" });
            definedNames.Append(new DefinedName { Name = "sydy3", Text = "='试验报告'!$D$9" });
            definedNames.Append(new DefinedName { Name = "sydl3", Text = "='试验报告'!$E$9" });
            definedNames.Append(new DefinedName { Name = "result3", Text = "='试验报告'!$F$9" });
            definedNames.Append(new DefinedName { Name = "sydy4", Text = "='试验报告'!$D$10" });
            definedNames.Append(new DefinedName { Name = "sydl4", Text = "='试验报告'!$E$10" });
            definedNames.Append(new DefinedName { Name = "result4", Text = "='试验报告'!$F$10" });
            definedNames.Append(new DefinedName { Name = "JY电阻", Text = "='试验报告'!$D$11" });
            definedNames.Append(new DefinedName { Name = "ZJNY", Text = "='试验报告'!$D$12" });
            definedNames.Append(new DefinedName { Name = "测试结果", Text = "='试验报告'!$F$11" });
            
            workbookPart.Workbook.AppendChild(definedNames);

            SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

            Row headerRow = new Row();
            string[] headers = { "车型型号", "车型车号", "试验员", "试验时间", "设备编号", "备注信息", "回路一试验电压(KV)", "回路一泄漏电流(mA)", "回路一测试结果", "回路二试验电压(KV)", "回路二泄漏电流(mA)", "回路二测试结果", "", "回路三试验电压(KV)", "回路三泄漏电流(mA)", "回路三测试结果", "回路四试验电压(KV)", "回路四泄漏电流(mA)", "回路四测试结果", "绝缘电阻", "匝间耐压", "测试结果" };
            
            foreach (string header in headers)
            {
                Cell cell = new Cell();
                cell.DataType = CellValues.String;
                cell.CellValue = new CellValue(header);
                headerRow.Append(cell);
            }
            sheetData.Append(headerRow);

            workbookPart.Workbook.Save();
        }
    }

    private Cell CreateCell(string value)
    {
        Cell cell = new Cell();
        cell.DataType = CellValues.String;
        cell.CellValue = new CellValue(value ?? string.Empty);
        return cell;
    }

    private object GetCellValue(Cell[] cells, int index)
    {
        if (index >= cells.Length)
        {
            return string.Empty;
        }

        Cell cell = cells[index];
        if (cell.CellValue != null)
        {
            return cell.CellValue.Text;
        }
        return string.Empty;
    }

    public void Write(string cellReference, string value)
    {
        if (!ReportExists())
        {
            CreateReportIfNotExists();
        }

        using (SpreadsheetDocument package = SpreadsheetDocument.Open(_reportPath, true))
        {
            UpdateCellValue(package, cellReference, value);
            package.WorkbookPart.Workbook.Save();
        }
    }

    public List<List<string>> ReadAllSheetData()
    {
        return GetTestReportData();
    }

    private List<List<string>> GetTestReportData()
    {
        var data = new List<List<string>>();
        data.Add(new List<string> { "", "", "试验报告", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" });
        data.Add(new List<string> { "车型型号", "", "车型车号", "", "试验员", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" });
        data.Add(new List<string> { "试验时间", "", "设备编号", "", "备注信息", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" });
        data.Add(new List<string> { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" });
        data.Add(new List<string> { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" });
        data.Add(new List<string> { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" });
        return data;
    }

    private int GetColumnIndex(string cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
            return 0;
        
        string columnPart = string.Concat(cellReference.TakeWhile(c => !char.IsDigit(c)));
        int result = 0;
        foreach (char c in columnPart)
        {
            result = result * 26 + (c - 'A' + 1);
        }
        return result - 1;
    }

    private string GetCellTextValue(SpreadsheetDocument package, Cell cell)
    {
        if (cell == null)
            return string.Empty;

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            SharedStringTablePart sharedStringPart = package.WorkbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
            if (sharedStringPart != null)
            {
                string cellText = cell.CellValue != null ? cell.CellValue.Text : cell.InnerText;
                if (int.TryParse(cellText, out int index))
                {
                    var sharedStringItem = sharedStringPart.SharedStringTable.ElementAtOrDefault(index);
                    if (sharedStringItem != null)
                    {
                        return sharedStringItem.InnerText;
                    }
                }
            }
        }

        if (cell.CellValue != null)
        {
            return cell.CellValue.Text;
        }

        return cell.InnerText;
    }

    public void OpenWithLibreOffice()
    {
        if (!ReportExists())
        {
            CreateReportIfNotExists();
        }

        try
        {
            string libreOfficePath = FindLibreOfficePath();
            
            if (!string.IsNullOrEmpty(libreOfficePath) && File.Exists(libreOfficePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = libreOfficePath,
                    Arguments = $"\"{_reportPath}\"",
                    UseShellExecute = true
                });
            }
            else if (TryStartLibreOfficeFromPath())
            {
                return;
            }
            else
            {
                OpenWithDefaultProgram();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("无法打开报表文件", ex);
        }
    }

    private bool TryStartLibreOfficeFromPath()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "libreoffice",
                Arguments = $"\"{_reportPath}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OpenWithDefaultProgram()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _reportPath,
            UseShellExecute = true
        });
    }

    private string FindLibreOfficePath()
    {
        string[] possiblePaths = new[]
        {
            "/usr/bin/libreoffice",
            "/usr/local/bin/libreoffice",
            "/opt/libreoffice/program/soffice",
            "/usr/lib/libreoffice/program/soffice",
            "/Applications/LibreOffice.app/Contents/MacOS/soffice",
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        };

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}