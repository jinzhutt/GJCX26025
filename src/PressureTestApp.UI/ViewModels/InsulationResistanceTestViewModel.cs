using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressureTestApp.Communication.Equip;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PressureTestApp.UI.ViewModels;

public partial class InsulationResistanceTestViewModel : ObservableObject
{
    private readonly Window? _owner;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    [ObservableProperty]
    private ObservableCollection<string> _comPorts = new();

    [ObservableProperty]
    private string? _selectedComPort;

    [ObservableProperty]
    private string _connectionState = "未连接";

    [ObservableProperty]
    private IImmutableSolidColorBrush _connectionStateColor = Brushes.Red;

    [ObservableProperty]
    private bool _canConnect = true;

    [ObservableProperty]
    private bool _canDisconnect = false;

    [ObservableProperty]
    private bool _canStartTest = false;

    [ObservableProperty]
    private bool _canStopTest = false;

    [ObservableProperty]
    private double _voltage = 50;

    [ObservableProperty]
    private double _testTime = 30;

    [ObservableProperty]
    private double _resistanceLimit = 100;

    [ObservableProperty]
    private ObservableCollection<string> _testTypes = new() { "绝缘电阻", "绝缘耐压" };

    private string _selectedTestType = "绝缘电阻";
    public string SelectedTestType
    {
        get => _selectedTestType;
        set
        {
            if (SetProperty(ref _selectedTestType, value))
            {
                UpdateUIByTestType(value);
            }
        }
    }

    [ObservableProperty]
    private string _mainTitle = "绝缘电阻测试";

    [ObservableProperty]
    private string _paramTitle = "绝缘电阻测试参数";

    [ObservableProperty]
    private string _voltageUnit = "测试电压DC (V)";

    [ObservableProperty]
    private string _limitLabel = "电阻下限 (MΩ)";

    [ObservableProperty]
    private string _startButtonText = "开始绝缘电阻测试";

    [ObservableProperty]
    private string _stopButtonText = "停止绝缘电阻测试";

    [ObservableProperty]
    private string _stateLabel = "绝缘测试状态:";

    [ObservableProperty]
    private string _valueLabel = "绝缘电阻测试值:";

    [ObservableProperty]
    private string _resultLabel = "绝缘电阻测试结果:";

    [ObservableProperty]
    private string _testState = "就绪";

    private void UpdateUIByTestType(string testType)
    {
        switch (testType)
        {
            case "绝缘电阻":
                MainTitle = "绝缘电阻测试";
                ParamTitle = "绝缘电阻测试参数";
                VoltageUnit = "测试电压DC (V)";
                LimitLabel = "电阻下限 (MΩ)";
                StartButtonText = "开始绝缘电阻测试";
                StopButtonText = "停止绝缘电阻测试";
                StateLabel = "绝缘测试状态:";
                ValueLabel = "绝缘电阻测试值:";
                ResultLabel = "绝缘电阻测试结果:";
                break;

            case "绝缘耐压":
                MainTitle = "绝缘耐压测试";
                ParamTitle = "绝缘耐压测试参数";
                VoltageUnit = "测试电压AC (V)";
                LimitLabel = "漏电流上限 (mA)";
                StartButtonText = "开始绝缘耐压测试";
                StopButtonText = "停止绝缘耐压测试";
                StateLabel = "耐压测试状态:";
                ValueLabel = "漏电流测试值:";
                ResultLabel = "绝缘耐压测试结果:";
                break;
        }
    }

    [ObservableProperty]
    private string _resistanceValue = "";

    [ObservableProperty]
    private string _testConclusion = "";

    [ObservableProperty]
    private string _countDown = "00:00:00";

    [ObservableProperty]
    private IImmutableSolidColorBrush _testConclusionColor = Brushes.Gray;

    private bool _isTesting = false;
    private int _testDuration;
    private int _elapsedTime;
    private DispatcherTimer? _timer;

    public InsulationResistanceTestViewModel(Window? owner = null)
    {
        _owner = owner;
        LoadComPorts();

        var configPort = GetInsulationConfigPort();
        if (!string.IsNullOrEmpty(configPort) && ComPorts.Contains(configPort))
        {
            SelectedComPort = configPort;
        }

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
    }

    private string? GetInsulationConfigPort()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppSettings>(json);
                return config?.InsulationConfig?.PortName;
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "读取绝缘测试配置失败");
        }
        return null;
    }

    private class AppSettings
    {
        public InsulationConfig? InsulationConfig { get; set; }
    }

    private void LoadComPorts()
    {
        try
        {
            ComPorts.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                ComPorts.Add(port);
            }
            if (ComPorts.Count > 0)
            {
                SelectedComPort = ComPorts[0];
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "加载串口失败");
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(SelectedComPort))
        {
            await ShowMessageAsync("请选择串口!", "提示");
            return;
        }

        try
        {
            if (TH2683A.serialPort2683AB == null)
            {
                TH2683A.serialPort2683AB = new SerialPort();
            }

            if (TH2683A.serialPort2683AB.IsOpen)
            {
                TH2683A.serialPort2683AB.Close();
            }

            TH2683A.serialPort2683AB.PortName = SelectedComPort;
            TH2683A.serialPort2683AB.BaudRate = 9600;
            TH2683A.serialPort2683AB.DataBits = 8;
            TH2683A.serialPort2683AB.StopBits = StopBits.One;
            TH2683A.serialPort2683AB.Parity = Parity.None;
            TH2683A.serialPort2683AB.ReadTimeout = 5000;
            TH2683A.serialPort2683AB.WriteTimeout = 5000;

            string result = TH2683A.IsComConnect();

            if (!string.IsNullOrEmpty(result) && result != "通信失败" && result != "连接失败")
            {
                ConnectionState = "已连接";
                ConnectionStateColor = Brushes.Green;
                CanConnect = false;
                CanDisconnect = true;
                CanStartTest = true;
                NlogHelper.Info($"TH2683A连接成功: {result}");
                await ShowMessageAsync($"连接成功!\n设备信息: {result}", "提示");
            }
            else
            {
                ConnectionState = "连接失败";
                ConnectionStateColor = Brushes.Red;

                if (TH2683A.serialPort2683AB.IsOpen)
                {
                    TH2683A.serialPort2683AB.Close();
                }

                await ShowMessageAsync($"通信连接失败: {result}\n建议:\n1. 检查线路是否连接\n2. 设备是否上电\n3. 串口设置是否正确", "提示");
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "TH2683A连接异常");
            ConnectionState = "未连接";
            ConnectionStateColor = Brushes.Red;
            await ShowMessageAsync($"连接失败: {ex.Message}", "错误");
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (_isTesting)
        {
            await ShowMessageAsync("测试进行中,请先停止测试!", "提示");
            return;
        }

        try
        {
            if (TH2683A.serialPort2683AB != null && TH2683A.serialPort2683AB.IsOpen)
            {
                TH2683A.serialPort2683AB.Close();
            }

            ConnectionState = "未连接";
            ConnectionStateColor = Brushes.Red;
            CanConnect = true;
            CanDisconnect = false;
            CanStartTest = false;

            await ShowMessageAsync("已断开连接", "提示");
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "TH2683A断开连接异常");
            await ShowMessageAsync($"断开连接失败: {ex.Message}", "错误");
        }
    }

    [RelayCommand]
    private async Task StartTestAsync()
    {
        if (TH2683A.serialPort2683AB == null || !TH2683A.serialPort2683AB.IsOpen)
        {
            await ShowMessageAsync("请先连接设备!", "提示");
            return;
        }

        try
        {
            string voltage = Voltage.ToString();
            string time = TestTime.ToString();
            double resistanceLimit = ResistanceLimit;

            CanStartTest = false;
            CanStopTest = true;
            CanDisconnect = false;
            _isTesting = true;

            TestState = "准备测试...";
            ResistanceValue = "";
            TestConclusion = "";
            TestConclusionColor = Brushes.Gray;

            _testDuration = (int)TestTime;
            _elapsedTime = 0;

            await Task.Run(() => PerformTest(voltage, time, resistanceLimit, _cancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "开始测试失败");
            ResetUI();
            await ShowMessageAsync($"开始测试失败: {ex.Message}", "错误");
        }
    }

    private void PerformTest(string voltage, string time, double resistanceLimit, CancellationToken token)
    {
        try
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                TestState = "正在发送测试参数...";
            });

            TH2683A.TH2683B_Test(voltage, time);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                TestState = "测试中...";
                _timer?.Start();
            });

            bool testComplete = false;
            int timeout = 0;
            int maxTimeout = (int)((Convert.ToSingle(time) + 10.0) * 1000.0);
            //int maxTimeout = (int)(TestTime + 10) * 1000;
            Thread.Sleep(5000);
            while (!testComplete && timeout < maxTimeout && !token.IsCancellationRequested)
            {
                Thread.Sleep(1000);
                timeout += 1000;
                //_elapsedTime = timeout/1000;
                try
                {
                    if (TH2683A.serialPort2683AB.IsOpen)
                    {
                        TH2683A.serialPort2683AB.DiscardInBuffer();
                        TH2683A.serialPort2683AB.DiscardOutBuffer();
                        Thread.Sleep(50);
                        TH2683A.serialPort2683AB.WriteLine("SYSTem:STATus?");
                        Thread.Sleep(500);
                        string status = TH2683A.serialPort2683AB.ReadExisting();

                        if (status.Contains("test complete"))
                        {
                            testComplete = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"查询状态异常: {ex.Message}");
                }
            }

            if (token.IsCancellationRequested)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TestState = "测试取消";
                    _timer?.Stop();
                    ResetUI();
                });
                return;
            }

            if (!testComplete)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TestState = "测试超时";
                    _timer?.Stop();
                    ResetUI();
                });
                return;
            }

            string resistanceValue = TH2683A.TH2683B_ReadData();

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _timer?.Stop();
                TestState = "测试完成";

                if (!string.IsNullOrEmpty(resistanceValue) && double.TryParse(resistanceValue, out double resistance))
                {
                    ResistanceValue = $"{resistanceValue} MΩ";

                    if (resistance >= resistanceLimit)
                    {
                        TestConclusion = "合格 - PASS";
                        TestConclusionColor = Brushes.Green;
                    }
                    else
                    {
                        TestConclusion = "不合格 - FAIL";
                        TestConclusionColor = Brushes.Red;
                    }
                }
                else
                {
                    ResistanceValue = "读取失败";
                    TestConclusion = "测试异常";
                    TestConclusionColor = Brushes.Orange;
                }

                ResetUI();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _timer?.Stop();
                TestState = "测试异常";
                ResetUI();
            });
            NlogHelper.Error(ex, "测试过程异常");
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _elapsedTime++;
        CountDown = $"{_elapsedTime / 3600:D2}:{(_elapsedTime % 3600) / 60:D2}:{_elapsedTime % 60:D2}";

        if (_elapsedTime >= _testDuration +10)
        {
            _timer?.Stop();
            _isTesting = false;
            CountDown = "00:00:00";
        }
    }

    private void ResetUI()
    {
        _isTesting = false;
        CanStartTest = true;
        CanStopTest = false;
        CanDisconnect = true;
        _elapsedTime = 0;
        CountDown = "00:00:00";
    }

    [RelayCommand]
    private async Task StopTestAsync()
    {
        if (TH2683A.serialPort2683AB == null || !TH2683A.serialPort2683AB.IsOpen)
        {
            await ShowMessageAsync("请先连接设备!", "提示");
            return;
        }

        try
        {
            _cancellationTokenSource.Cancel();
            _timer?.Stop();

            await Task.Run(() =>
            {
                try
                {
                    TH2683A.TestStop();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"停止测试异常: {ex.Message}");
                }
            });

            TestState = "测试已停止";
            ResetUI();
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "停止测试失败");
            await ShowMessageAsync($"停止测试失败: {ex.Message}", "错误");
        }
    }

    private async Task ShowMessageAsync(string message, string title)
    {
        if (_owner != null)
        {
            await new InfoNoticeWindow(message, title).ShowDialog(_owner);
        }
    }
}