using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;
using PressureTestApp.Core.Services;

namespace PressureTestApp.Communication.Excel;

/// <summary>
/// Excel 模板导入服务（NPOI）。
/// 模板列顺序：A=任务编号, B=车型, C=列号, D=辆号, E=修程, F=检测项,
/// G=回路1电压, H=回路2电压, I=回路3电压, J=回路4电压, K=试验时间(秒), L=保护电流(mA)
/// </summary>
public class ExcelImportService : IExcelImportService
{
    public async Task<ExcelImportResult> ImportAsync(string filePath)
    {
        var result = new ExcelImportResult { FileName = Path.GetFileName(filePath) };

        await Task.Run(() =>
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                IWorkbook wb = WorkbookFactory.Create(fs);
                ISheet sheet = wb.GetSheet("任务列表") ?? wb.GetSheetAt(0);
                if (sheet == null)
                {
                    result.Errors.Add("Excel 文件中找不到有效工作表");
                    return;
                }

                // 假设第 0 行是表头，从第 1 行开始读
                for (int rowIdx = 1; rowIdx <= sheet.LastRowNum; rowIdx++)
                {
                    var row = sheet.GetRow(rowIdx);
                    if (row == null) continue;

                    // 任务编号为空 → 跳过整行（允许中间空行）
                    var taskId = GetString(row.GetCell(0));
                    if (string.IsNullOrWhiteSpace(taskId)) continue;

                    try
                    {
                        var task = new TestTask
                        {
                            TaskId   = taskId,
                            CarType  = GetString(row.GetCell(1)),
                            TrainNo  = GetString(row.GetCell(2)),
                            CarNo    = GetString(row.GetCell(3)),
                            Process  = GetString(row.GetCell(4)),
                            TestItem = GetString(row.GetCell(5)),
                            LoopVoltages = new[]
                            {
                                GetDouble(row.GetCell(6)),
                                GetDouble(row.GetCell(7)),
                                GetDouble(row.GetCell(8)),
                                GetDouble(row.GetCell(9))
                            },
                            DurationSeconds  = (int)GetDouble(row.GetCell(10), 60),
                            ProtectCurrentMa = GetDouble(row.GetCell(11), 500),
                            Source           = TaskSource.Excel
                        };

                        // 必填校验
                        var miss = new List<string>();
                        if (string.IsNullOrEmpty(task.CarType))  miss.Add("车型");
                        if (string.IsNullOrEmpty(task.TrainNo))  miss.Add("列号");
                        if (string.IsNullOrEmpty(task.CarNo))    miss.Add("辆号");
                        if (string.IsNullOrEmpty(task.TestItem)) miss.Add("检测项");
                        if (task.LoopVoltages[0] <= 0)           miss.Add("回路1电压");
                        if (miss.Any())
                        {
                            result.Errors.Add($"第 {rowIdx + 1} 行缺少：{string.Join(",", miss)}");
                            continue;
                        }

                        // 范围校验
                        for (int i = 0; i < 4; i++)
                        {
                            if (task.LoopVoltages[i] < 0 || task.LoopVoltages[i] > 15)
                            {
                                result.Errors.Add($"第 {rowIdx + 1} 行回路{i + 1}电压超出范围 0-15kV");
                                break;
                            }
                        }

                        result.Tasks.Add(task);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"第 {rowIdx + 1} 行解析失败：{ex.Message}");
                    }
                }

                NlogHelper.Info($"Excel 导入完成：{result.Tasks.Count} 条成功，{result.Errors.Count} 条错误");
            }
            catch (Exception ex)
            {
                NlogHelper.Error(ex, "Excel 导入失败");
                result.Errors.Add($"Excel 文件读取失败：{ex.Message}");
            }
        });

        return result;
    }

    public async Task ExportTemplateAsync(string filePath)
    {
        await Task.Run(() =>
        {
            using var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("任务列表");

            // 表头
            var headers = new[]
            {
                "任务编号", "车型", "列号", "辆号", "修程", "检测项",
                "回路1电压(kV)", "回路2电压(kV)", "回路3电压(kV)", "回路4电压(kV)",
                "试验时间(秒)", "保护电流(mA)"
            };
            var headerRow = sheet.CreateRow(0);
            var headerStyle = wb.CreateCellStyle();
            var headerFont = wb.CreateFont();
            headerFont.IsBold = true;
            headerStyle.SetFont(headerFont);

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
                sheet.SetColumnWidth(i, 18 * 256);
            }

            // 示例数据
            var sample = sheet.CreateRow(1);
            sample.CreateCell(0).SetCellValue("T20260506-001");
            sample.CreateCell(1).SetCellValue("CR400AF");
            sample.CreateCell(2).SetCellValue("0518");
            sample.CreateCell(3).SetCellValue("03");
            sample.CreateCell(4).SetCellValue("大修");
            sample.CreateCell(5).SetCellValue("牵引变压器交流耐压");
            sample.CreateCell(6).SetCellValue(10);
            sample.CreateCell(7).SetCellValue(10);
            sample.CreateCell(8).SetCellValue(10);
            sample.CreateCell(9).SetCellValue(10);
            sample.CreateCell(10).SetCellValue(60);
            sample.CreateCell(11).SetCellValue(500);

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            wb.Write(fs);
            NlogHelper.Info($"导出 Excel 模板到：{filePath}");
        });
    }

    private static string GetString(ICell? cell)
    {
        if (cell == null) return "";
        return cell.CellType switch
        {
            CellType.String  => cell.StringCellValue?.Trim() ?? "",
            CellType.Numeric => cell.NumericCellValue.ToString("0.######"),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => cell.CachedFormulaResultType switch
            {
                CellType.String  => cell.StringCellValue?.Trim() ?? "",
                CellType.Numeric => cell.NumericCellValue.ToString("0.######"),
                _ => cell.ToString()?.Trim() ?? ""
            },
            _ => cell.ToString()?.Trim() ?? ""
        };
    }

    private static double GetDouble(ICell? cell, double defaultValue = 0)
    {
        if (cell == null) return defaultValue;
        try
        {
            return cell.CellType switch
            {
                CellType.Numeric => cell.NumericCellValue,
                CellType.String  => double.TryParse(cell.StringCellValue, out var v) ? v : defaultValue,
                CellType.Formula => cell.CachedFormulaResultType == CellType.Numeric ? cell.NumericCellValue : defaultValue,
                _ => defaultValue
            };
        }
        catch { return defaultValue; }
    }
}
