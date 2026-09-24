using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Communication;
using PressureTestApp.Communication.Api;
using PressureTestApp.Communication.Models;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.UI.Helpers;
using PressureTestApp.UI.ViewModels;
using S7.Net;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;

namespace PressureTestApp.UI.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<TextBox, string> _mainTestParamFocusValues = new();
    private ObservableCollection<string>? _testProcessLogSubscription;
    private bool _speedModeFast;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnMainWindowDataContextChanged;
        Closed += OnMainWindowClosed;
    }

    private void OnMainWindowDataContextChanged(object? sender, EventArgs e)
    {
        DetachTestProcessLogAutoScroll();
        if (DataContext is MainViewModel vm)
        {
            _testProcessLogSubscription = vm.TestProcessLogLines;
            vm.TestProcessLogLines.CollectionChanged += OnTestProcessLogLinesCollectionChanged;
            ScrollTestProcessLogToEnd();
            vm.NotifyProcessInterfaceLoaded();
        }
    }

    private void DetachTestProcessLogAutoScroll()
    {
        if (_testProcessLogSubscription != null)
        {
            _testProcessLogSubscription.CollectionChanged -= OnTestProcessLogLinesCollectionChanged;
            _testProcessLogSubscription = null;
        }
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        DetachTestProcessLogAutoScroll();

        // 释放 ViewModel 资源
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void OnTestProcessLogLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add
            or NotifyCollectionChangedAction.Remove
            or NotifyCollectionChangedAction.Replace
            or NotifyCollectionChangedAction.Reset
            or NotifyCollectionChangedAction.Move)
            ScrollTestProcessLogToEnd();
    }

    private void ScrollTestProcessLogToEnd()
    {
        if (TestProcessLogScrollViewer is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (TestProcessLogScrollViewer is not { } sv) return;
            sv.ScrollToEnd();
        }, DispatcherPriority.Render);
    }

    private void OnMainTestParameterGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox tb)
            _mainTestParamFocusValues[tb] = tb.Text ?? "";
    }

    private void OnMainTestParameterLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not string tag || DataContext is not MainViewModel vm)
            return;
        if (!vm.CanEditInfoAndParameters)
            return;
        var now = tb.Text ?? "";
        if (_mainTestParamFocusValues.TryGetValue(tb, out var was) && was != now)
            vm.AppendTestProcessLog($"修改参数「{tag}」");
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.ClickCount == 1)
        {
            BeginMoveDrag(e);
        }
        else if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;
        }
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private async void OnLogoutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.AppendTestProcessLog("使用「退出系统」");
            try { await vm.NotifyMesShutdownAsync(); }
            catch (Exception ex) { Core.Helpers.NlogHelper.Warn(ex, "MES 关机上报失败"); }
            vm.LogoutCommand.Execute(null);
        }

        var login = new LoginWindow
        {
            DataContext = App.Services.GetRequiredService<LoginViewModel>()
        };
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = login;
            login.Show();
        }
        Close();
    }

    /// <summary>
    /// Excel 重新导入：弹出文件选择
    /// </summary>
    private async void OnReimportExcelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainVm)
            mainVm.AppendTestProcessLog("使用「重新导入 Excel」");
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择 Excel 任务模板",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Excel 文件")
                    {
                        Patterns = new[] { "*.xlsx", "*.xls" }
                    }
                }
            });

            if (files.Count == 0) return;
            var file = files[0];

            if (DataContext is MainViewModel vm && file.Path.LocalPath is string path)
            {
                await vm.ImportExcelCommand.ExecuteAsync(path);
            }
        }
        catch (Exception ex)
        {
            Core.Helpers.NlogHelper.Error(ex, "Excel 导入失败");
        }
    }

    /// <summary>信息录入：车型选择弹窗</summary>
    private async void OnVehicleModelSelectClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel main)
        {
            if (!main.CanEditInfoAndParameters)
                return;
            main.AppendTestProcessLog("使用「车型选择」");
        }
        var vmDlg = new VehicleModelSelectViewModel();
        var win = new VehicleModelSelectWindow { DataContext = vmDlg };
        vmDlg.RequestClose += () =>
        {
            try { win.Close(); }
            catch { /* 已关闭 */ }
        };
        vmDlg.OnConfirmed = row =>
        {
            if (DataContext is not MainViewModel main) return;
            // 车型取数据库型号名；不按车型加载 Para.ini（工艺参数单独用「参数」下拉）
            main.InfoEntryBearingTypeText = row.ProductModel;
            main.UpdateReportVehicleInfo(row.ProductType, row.ProductModel);
            NlogHelper.Info($"车型选择：{row.ProductType} · {row.ProductModel}");
        };
        await win.ShowDialog(this);
    }

    private async Task WithAnalogEditOverlayAsync(Func<Task> action)
    {
        if (DataContext is not MainViewModel vm)
        {
            await action();
            return;
        }

        vm.IsAnalogEditOverlayVisible = true;
        try
        {
            await Task.Yield();
            await action();
        }
        finally
        {
            vm.IsAnalogEditOverlayVisible = false;
        }
    }

    private static bool IsNumericUpDownSpinnerSource(object? source)
    {
        if (source is not Visual v)
            return false;
        for (var p = v; p != null; p = p.GetVisualParent())
        {
            if (p is RepeatButton)
                return true;
        }
        return false;
    }

    private async void OnAnalogNumericTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not NumericUpDown { Tag: string key } || DataContext is not MainViewModel main)
            return;
        if (IsNumericUpDownSpinnerSource(e.Source))
            return;

        await WithAnalogEditOverlayAsync(async () =>
        {
            var dlgVm = CreateAnalogEditDialogViewModel(key, main);
            if (dlgVm is null)
                return;

            var dlg = new NumericEditDialogWindow { DataContext = dlgVm };
            await dlg.ShowDialog(this);
            if (!dlgVm.DialogAccepted)
                return;

            switch (key)
            {
                case "Press":
                    main.PlcPubP00 = dlgVm.ResultValue;
                    main.WritePlcTag("PUB.P00", main.PlcPubP00);
                    break;
                case "DisplacementOffset":
                    main.PlcPubP04 = dlgVm.ResultValue;
                    main.WritePlcTag("PUB.P04", main.PlcPubP04);
                    break;
                case "DecelPressure":
                    main.PlcPubP01 = dlgVm.ResultValue;
                    main.WritePlcTag("PUB.P01", main.PlcPubP01);
                    break;
                case "HighPressureLimit":
                    main.PlcPubP14 = dlgVm.ResultValue;
                    main.WritePlcTag("PUB.P14", main.PlcPubP14);
                    break;
                case "ReverseTime":
                    main.PlcPubP15 = dlgVm.ResultValue;
                    main.WritePlcTag("PUB.P15", (ushort)Math.Clamp((int)Math.Round(main.PlcPubP15), 0, 65535));
                    break;
            }
        });
    }
    private bool _isPullRodUpRunning;
    private bool _isPullRodDownRunning;
    private void OnPullRodUpPressed(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("OnPullRodUpPressed 被调用");
        if (DataContext is not MainViewModel main || !main.IsProcessControlEnabled)
            return;
        if (sender is Border border)
        { 
            border.Background = Brushes.Gray; // 按下时改变背景颜色
        }
        main.WritePlcTag("DO.CDO00", true);
        main.AppendTestProcessLog("拉杆上升");
    }

    private void OnPullRodUpReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MainViewModel main || !main.IsProcessControlEnabled)
            return;
        if (sender is Border border) 
        {
            border.Background = Brushes.Green;; // 松开时恢复背景颜色
        }
        main.WritePlcTag("DO.CDO00", false);
        main.AppendTestProcessLog("拉杆上行停止");
    }

    private void OnPullRodDownPressed(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main || !main.IsProcessControlEnabled)
            return;
        if (sender is Border border)
            border.Background = Brushes.Gray;

        main.WritePlcTag("DO.CDO01", true);
        main.AppendTestProcessLog("拉杆下降");
    }

    private void OnPullRodDownReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MainViewModel main || !main.IsProcessControlEnabled)
            return;
        if (sender is Border border)
            border.Background = Brushes.Green;

        main.WritePlcTag("DO.CDO01", false);
        main.AppendTestProcessLog("拉杆下行停止");
    }

    private void OnSpeedModeToggleClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main)
            return;
        if (!main.IsProcessControlEnabled)
        {
            main.AppendTestProcessLog("急停或液位极限报警中，无法切换速度");
            return;
        }
        main.CurrentSpeedModeIsFast = !main.CurrentSpeedModeIsFast;

        // bool nextFast = !main.CurrentSpeedModeIsFast;
        main.WritePlcTag("DO.CDO02", main.CurrentSpeedModeIsFast);
        main.AppendTestProcessLog(main.CurrentSpeedModeIsFast
          ? "请求切换为快速（M421=1）"
        : "请求切换为慢速（M421=0）");


    }

    private async void OnRestoreDefaultParamsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main)
            return;
        if (!main.IsProcessControlEnabled)
        {
            main.AppendTestProcessLog("急停或液位极限报警中，无法恢复默认参数");
            return;
        }

        //弹出确认框
        var confirmed = await ConfirmDialogWindow.ShowConfirmAsync(this, "确认恢复默认参数吗？", "恢复默认参数");
        if (!confirmed)
        {
            return;
        }
        main.PlcPubP00 = 10.0;//目标压力默认值10MPa
        main.PlcPubP04 = 0.0;//位移补偿默认值0mm
        main.PlcPubP01 = 5.0;//开始减速压力默认值0bar
        main.PlcPubP14 = 0.5;//高压浮动限制T默认值0
        main.PlcPubP15 = 2;//反向时间默认值0S   
                           // 写 PLC（在线模式才真正生效，仿真模式写了也没用但不报错）
        main.WritePlcTag("DI.MDI01", true);
        main.AppendTestProcessLog("触发恢复默认参数(M403=1)");
        //等待500ms后再将M403置为0
        await Task.Delay(500);
        main.WritePlcTag("DI.MDI01", false);
        main.AppendTestProcessLog("恢复默认参数完成(M403=0)");
    }

    private static NumericEditDialogViewModel? CreateAnalogEditDialogViewModel(string key, MainViewModel main) =>
        key switch
        {
            "Press" => new NumericEditDialogViewModel(
                "目标压力设定(1~30MPa)[最大值 30]",
                main.PlcPubP00, 1, 30, 1),
            "DisplacementOffset" => new NumericEditDialogViewModel(
                "位移补充值(0~400mm)[最大值 400]",
                main.PlcPubP04, 0, 400, 1),
            "DecelPressure" => new NumericEditDialogViewModel(
                "开始减速压力(0~500bar)[最大值 500]",
                main.PlcPubP01, 0, 500, 1),
            "HighPressureLimit" => new NumericEditDialogViewModel(
                "高压浮动限制T(0~999)[最大值 999]",
                main.PlcPubP14, 0, 999, 1),
            "ReverseTime" => new NumericEditDialogViewModel(
                "反向时间(0~65535S)[最大值 65535]",
                main.PlcPubP15, 0, 65535, 0),
            _ => null
        };

    /// <summary>
    /// 任务区打开模态子窗时显示主窗半透明遮罩；关闭后移除。
    /// </summary>
    private async Task WithTaskModalOverlayAsync(Func<Task> action)
    {
        if (DataContext is not MainViewModel vm)
        {
            await action();
            return;
        }

        vm.IsTaskModalOverlayVisible = true;
        try
        {
            await Task.Yield();
            await action();
        }
        finally
        {
            vm.IsTaskModalOverlayVisible = false;
        }
    }

    /// <summary>任务下载：与 GJCS ucHMI.BtnTaskDownload_Click 一致，先拉列表再打开选择窗体。</summary>
    private async void OnTaskDownloadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await WithTaskModalOverlayAsync(async () =>
        {
            MainViewModel? main = DataContext as MainViewModel;
            main?.AppendTestProcessLog("使用「任务下载」");

            if (string.IsNullOrWhiteSpace(VarHelper.CurrentUserId))
            {
                await new InfoNoticeWindow("未登录，无法下载任务。").ShowDialog(this);
                return;
            }

            var taskDownload = App.Services.GetRequiredService<TaskDownloadService>();
            MainTaskListResult? taskData;
            try
            {
                main?.AppendTestProcessLog("正在获取任务列表…");
                NlogHelper.Info("开始获取任务列表");
                taskData = await taskDownload.GetMainTaskListAsync(VarHelper.CurrentUserId!);
            }
            catch (Exception ex)
            {
                NlogHelper.Error(ex, "任务列表获取失败");
                main?.AppendTestProcessLog($"任务列表获取失败：{ex.Message}");
                await new InfoNoticeWindow($"获取任务列表失败：{ex.Message}").ShowDialog(this);
                return;
            }

            if (taskData == null)
            {
                await new InfoNoticeWindow("获取任务列表失败！").ShowDialog(this);
                main?.AppendTestProcessLog("任务列表获取失败");
                return;
            }

            if (taskData.List == null || taskData.List.Count == 0)
            {
                await new InfoNoticeWindow("没有可下载的任务！").ShowDialog(this);
                main?.AppendTestProcessLog("没有可下载的任务");
                return;
            }

            main?.AppendTestProcessLog($"获取到 {taskData.List.Count} 个主任务");
            NlogHelper.Info($"成功获取任务列表，共 {taskData.List.Count} 个主任务");

            var vm = App.Services.GetRequiredService<TaskDownloadSelectViewModel>();
            vm.Initialize(taskData);
            var win = new TaskSelectDownloadWindow { DataContext = vm };
            await win.ShowDialog(this);

            if (vm.DialogResultOk)
            {
                var count = vm.SelectedTasks?.Count ?? 0;
                main?.AppendTestProcessLog($"任务下载完成！共下载 {count} 个任务");
                await new InfoNoticeWindow($"任务下载完成！共下载 {count} 个任务。").ShowDialog(this);
                NlogHelper.Info($"任务下载完成，共下载 {count} 个任务");
            }
            else
            {
                main?.AppendTestProcessLog("取消任务下载");
                NlogHelper.Info("用户取消了任务下载");
            }
        });
    }

    /// <summary>任务查看：本地库 isTested=false → 选择当前试验步骤</summary>
    private async void OnTaskViewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await WithTaskModalOverlayAsync(async () =>
        {
            var main = DataContext as MainViewModel;
            main?.AppendTestProcessLog("使用「任务查看」");
            var vm = App.Services.GetRequiredService<TaskViewViewModel>();
            vm.Configure(TaskViewDialogMode.TaskViewOnly);
            vm.HostMain = main;
            var win = new TaskViewWindow { DataContext = vm };
            await win.ShowDialog(this);
            if (vm.ConfirmedSelection is { } sel)
                main?.ApplyOnlineTaskSelection(sel);
        });
    }

    /// <summary>任务上传：打开「任务回传」界面。</summary>
    private async void OnTaskUploadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await WithTaskModalOverlayAsync(async () =>
        {
            var main = DataContext as MainViewModel;
            main?.AppendTestProcessLog("使用「任务上传」");
            var vm = App.Services.GetRequiredService<TaskViewViewModel>();
            vm.Configure(TaskViewDialogMode.TaskUpload);
            vm.HostMain = main;
            var win = new TaskViewWindow { DataContext = vm };
            await win.ShowDialog(this);
        });
    }

    /// <summary>临时联调：依次测 4.4 设备状态、4.5 设备故障、4.3 数据回传。</summary>
    private async void OnMesInterfaceTestClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.IsEnabled = false;

        var main = DataContext as MainViewModel;
        main?.AppendTestProcessLog("使用「MES联调」：4.4 状态 → 4.5 故障 → 4.3 回传…");
        NlogHelper.Info("点击：MES联调（4.4 / 4.5 / 4.3）");

        try
        {
            var base64Image = main is { HasPendingHvCurve: true }
                ? main.PendingCurveBase64
                : null;

            var (statusResult, faultResult, uploadResult) =
                await MesInterfaceTest.RunStatusFaultUploadAsync(base64Image);

            static string Format(MesTestResult r) =>
                $"{r.InterfaceName}：{(r.Success ? "成功" : "失败")}，code={r.Code?.ToString() ?? "null"}，msg={r.Message}";

            main?.AppendTestProcessLog(Format(statusResult));
            main?.AppendTestProcessLog(Format(faultResult));
            main?.AppendTestProcessLog(Format(uploadResult));

            var mesCfg = App.Services.GetRequiredService<ProductionSystemConfig>();
            var target = (mesCfg.MesBaseUrl ?? "").Trim().TrimEnd('/');

            var summary =
                $"{Format(statusResult)}\r\n" +
                $"{Format(faultResult)}\r\n" +
                $"{Format(uploadResult)}\r\n\r\n" +
                $"4.3 实际请求 JSON（看这里，不是调试器里的 Dictionary.ToString）：\r\n" +
                $"{Truncate(uploadResult.RequestBodyJson, 800)}\r\n\r\n" +
                $"4.4 响应：{Truncate(statusResult.RawResponse)}\r\n" +
                $"4.5 响应：{Truncate(faultResult.RawResponse)}\r\n" +
                $"4.3 响应：{Truncate(uploadResult.RawResponse)}\r\n\r\n" +
                $"（联调三项：状态 / 故障 / 回传，配置前缀：{target}）";

            await new InfoNoticeWindow(summary, "MES联调结果").ShowDialog(this);
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "MES联调异常");
            main?.AppendTestProcessLog($"MES联调异常：{ex.Message}");
            await new InfoNoticeWindow($"MES联调异常：{ex.Message}", "MES联调结果").ShowDialog(this);
        }
        finally
        {
            if (sender is Button b)
                b.IsEnabled = true;
        }

        static string Truncate(string text, int max = 300)
            => string.IsNullOrEmpty(text) ? "(空)"
                : text.Length <= max ? text
                : text[..max] + "…";
    }

    /// <summary>协议 4.5：设备故障上报（异常/告警文字，不阻止作业的故障说明）。</summary>
    private async void OnMesDeviceFaultClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.IsEnabled = false;

        var main = DataContext as MainViewModel;
        main?.AppendTestProcessLog("使用「故障上报」：调用 MES 4.5…");
        NlogHelper.Info("点击：设备故障上报（4.5）");

        try
        {
            var mes = App.Services.GetRequiredService<MesApiClient>();
            var (ok, error) = await mes.CanReachAsync();
            if (!ok)
            {
                var msg = error ?? "无法连接 MES";
                main?.AppendTestProcessLog($"MES 4.5 失败：{msg}");
                await new InfoNoticeWindow(msg, "设备故障上报").ShowDialog(this);
                return;
            }

            var payload = new MesDeviceFaultUploadRequest
            {
                ErrorMessage = $"联调模拟故障：感应器故障，无法作业（{DateTime.Now:HH:mm:ss}）",
                OccurTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var resp = await mes.UploadDeviceFaultAsync(payload);
            var summary =
                $"4.5 设备故障上报：{(resp.IsOk ? "成功" : "失败")}\r\n" +
                $"code={resp.Code}，msg={resp.StatusText}\r\n\r\n" +
                $"上报内容：\r\n" +
                $"· deviceId = {mes.DeviceId}\r\n" +
                $"· occurTime = {payload.OccurTime}\r\n" +
                $"· errorMessage = {payload.ErrorMessage}\r\n\r\n" +
                "说明：这是设备故障文字说明推送（协议 4.5），\r\n" +
                "与 4.4「状态=异常」不同；无故障时不必上报。";

            main?.AppendTestProcessLog(
                $"MES 4.5：{(resp.IsOk ? "成功" : "失败")}，code={resp.Code}，msg={resp.StatusText}");
            await new InfoNoticeWindow(summary, "设备故障上报").ShowDialog(this);
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "MES 4.5 上报异常");
            main?.AppendTestProcessLog($"MES 4.5 异常：{ex.Message}");
            await new InfoNoticeWindow($"上报异常：{ex.Message}", "设备故障上报").ShowDialog(this);
        }
        finally
        {
            if (sender is Button b)
                b.IsEnabled = true;
        }
    }

    /// <summary>绝缘测试：无可用串口时先提示（图一），再打开绝缘电阻测试仪（图二）；有串口则直接进入图二。</summary>
    private async void OnInsulationTestClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await WithTaskModalOverlayAsync(async () =>
        {
            MainViewModel? main = DataContext as MainViewModel;
            //if (DataContext is MainViewModel main)
            main.AppendTestProcessLog("使用「绝缘测试」");
            NlogHelper.Info("点击：绝缘测试");

            if (!SerialPortProbe.HasAvailablePorts())
            {
                var warn = new SerialPortWarningDialog();
                await warn.ShowDialog(this);
            }

            main.WritePlcTag("DO.CDO10", false);//主回路合闸控制
            main.WritePlcTag("DO.CDO11", false);//匝间试验切换
            main.WritePlcTag("DO.CDO15", true);//绝缘试验切换

            var vm = new InsulationResistanceTestViewModel();
            var win = new InsulationResistanceTestWindow { DataContext = vm };
            await win.ShowDialog(this);
        });
    }
    /// <summary>
    /// 获取主窗口
    /// </summary>
    private Window? GetMainWindow()
    {

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
    private async void OnWindingTestClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await WithTaskModalOverlayAsync(async () =>
        {
            MainViewModel? main = DataContext as MainViewModel;
            //if (DataContext is MainViewModel main)
            main.AppendTestProcessLog("使用「匝间耐压测试」");
            NlogHelper.Info("点击：匝间耐压测试");

            main.WritePlcTag("DO.CDO10", false);//主回路合闸控制
            main.WritePlcTag("DO.CDO11", true);//匝间试验切换
            main.WritePlcTag("DO.CDO15", false);//绝缘试验切换
            //if (!SerialPortProbe.HasAvailablePorts())
            //{
            //    var warn = new SerialPortWarningDialog();
            //    await warn.ShowDialog(this);
            //}

            ////var vm = new TH2883Window();
            //var win = new TH2883Window {  };
            //await win.ShowDialog(this);
            {
                var mainWindow = GetMainWindow();
                // 关键：校验窗口是否存在且未关闭
                if (mainWindow != null && mainWindow.IsVisible)
                {
                    var th2883Window = new TH2883Window();
                    await th2883Window.ShowDialog(mainWindow);
                }
                else
                {
                    new TH2883Window().Show();
                }
            }
        });
    }

    private void OnSidebarHistoryReportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel main)
            main.AppendTestProcessLog("使用「历史报表」");
    }

    private void OnSidebarSerialInstrumentClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel main)
            main.AppendTestProcessLog("使用「串口仪器」");
    }

    private void OnSafetyAlarmOverlayClicked(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel main)
            main.IsSafetyAlarmFlashing = false;
    }

    private void OnPullRodUpPressed(object? sender, PointerPressedEventArgs e)
    {
    }

    private void OnPullRodDownPressed(object? sender, PointerPressedEventArgs e)
    {
    }

    /// <summary>右键：导出曲线 PNG Base64；本地可备份文件，回传走协议顶层 base64Image。</summary>
    private async void OnSaveWithstandCurveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main)
            return;

        try
        {
            var chart = this.FindControl<CartesianChart>("WithstandCurveChart");
            if (chart is null)
            {
                await new InfoNoticeWindow("未找到压力曲线控件。", "保存曲线").ShowDialog(this);
                return;
            }

            if (main.WithstandVoltagePoints.Count == 0 && main.WithstandCurrentPoints.Count == 0)
            {
                await new InfoNoticeWindow("当前没有曲线数据，请先完成试验后再保存。", "保存曲线").ShowDialog(this);
                return;
            }

            var (base64, pngBytes) = ExportWithstandChartToPngBase64(chart);
            if (pngBytes.Length == 0 || string.IsNullOrEmpty(base64))
            {
                await new InfoNoticeWindow("导出曲线图片失败。", "保存曲线").ShowDialog(this);
                return;
            }

            var mesCfg = App.Services.GetRequiredService<ProductionSystemConfig>();
            var curvesDir = mesCfg.ResolveMesCurveImageDir();
            Directory.CreateDirectory(curvesDir);
            var fileName = $"bearing_curve_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.GetFullPath(Path.Combine(curvesDir, fileName));
            await File.WriteAllBytesAsync(filePath, pngBytes);

            main.SetPendingCurveBase64(base64, filePath);
            await new InfoNoticeWindow(
                $"曲线已保存。\n\n本地备份：{filePath}\n\n回传时写入顶层字段 base64Image（Base64，不传路径）。",
                "保存曲线").ShowDialog(this);
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "保存压力曲线失败");
            main.AppendTestProcessLog($"保存压力曲线失败：{ex.Message}");
            await new InfoNoticeWindow($"保存失败：{ex.Message}", "保存曲线").ShowDialog(this);
        }
    }

    private void OnClearSavedWithstandCurveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel main)
            main.ClearPendingHvCurve();
    }

    /// <summary>将当前 LiveCharts 曲线图导出为 PNG 字节与 Base64（与借鉴项目 ExportWithstandChartToPngBase64 一致）。</summary>
    private static (string Base64, byte[] PngBytes) ExportWithstandChartToPngBase64(CartesianChart chartControl)
    {
        var w = chartControl.Bounds.Width > 10 ? (int)chartControl.Bounds.Width : 900;
        var h = chartControl.Bounds.Height > 10 ? (int)chartControl.Bounds.Height : 480;

        var skChart = new SKCartesianChart(chartControl)
        {
            Width = Math.Max(w, 640),
            Height = Math.Max(h, 360)
        };

        using var image = skChart.GetImage();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        var pngBytes = data.ToArray();
        var base64 = Convert.ToBase64String(pngBytes);
        return (base64, pngBytes);
    }
}
