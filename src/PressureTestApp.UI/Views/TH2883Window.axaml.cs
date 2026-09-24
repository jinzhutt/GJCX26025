using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media; // 必须加，否则 Brushes 报错
using PressureTestApp.UI.Helpers;
using PressureTestApp.Communication.Equip;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.ImageFilters;
using PressureTestApp.Core.Configs;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // 新增：用于INotifyPropertyChanged
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PressureTestApp.UI.Views;

public partial class TH2883Window : Window, INotifyPropertyChanged
{

    public ResultAll ra = new ResultAll();
    // 全局串口对象
    private SerialPort _serialPort;
    TH2883ParaConfig para;
    private ISeries[] _series;
    public ISeries[] Series
    {
        get => _series;
        set
        {
            _series = value;
            OnPropertyChanged(nameof(Series)); // 通知UI更新
        }
    }

    //public ObservableCollection<ISeries> Series { get; set; } = new ObservableCollection<ISeries>();
    // 👇 这一段就是你要的【支持清空 + 通知UI更新】的正确定义
    //private ObservableCollection<ISeries> _series = new ObservableCollection<ISeries>();
    //public ObservableCollection<ISeries> Series
    //{
    //    get => _series;
    //    set
    //    {
    //        _series = value;
    //        OnPropertyChanged();
    //    }
    //}
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    //// 实现INotifyPropertyChanged接口
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    // 你必须有这个方法（Avalonia 通知界面刷新）
    //public event PropertyChangedEventHandler? PropertyChanged;
    //protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    //{
    //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    //}
    public TH2883Window()
    {
        InitializeComponent();
        para = new TH2883ParaConfig("unchanged");
        LoadComPorts();

        var configPort = GetTurnConfigPort();
        if (!string.IsNullOrEmpty(configPort) && cmbComPort.Items.Contains(configPort))
        {
            cmbComPort.SelectedItem = configPort;
        }

        txtSet4.Text = para.set4.ToString();
        InitChartSeries();
        DataContext = this;
    }

    private string? GetTurnConfigPort()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<TurnAppSettings>(json);
                return config?.TurnConfig?.PortName;
            }
        }
        catch (Exception ex)
        {
            GenericDialog.ShowMsg(this, "读取匝间耐压配置失败","错误");
        }
        return null;
    }

    private class TurnAppSettings
    {
        public TurnConfig? TurnConfig { get; set; }
    }

    private void InitChartSeries()
    {
        Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "U-V",
                Values = new double[] { 1,3,5,3,1,-1,-3,-5,-3,-1,1,3,5 },
                Stroke = new SolidColorPaint(SKColors.Cyan, 2),
                GeometrySize = 0,
                Fill = null
            },
            new LineSeries<double>
            {
                Name = "V-W",
                Values = new double[] { -5,-3,-1,1,3,5,3,1,-1,-3,-5,-3,-1 },
                Stroke = new SolidColorPaint(SKColors.Magenta, 2),
                GeometrySize = 0,
                Fill = null
            },
            new LineSeries<double>
            {
                Name = "U-W",
                Values = new double[] { 3,5,3,1,-1,-3,-5,-3,-1,1,3,5,3 },
                Stroke = new SolidColorPaint(SKColors.Yellow, 2),
                GeometrySize = 0,
                Fill = null
            }
        };

        // X 轴
        XAxes = new Axis[]
        {
            new Axis
            {
                Name = "Time (s)",
                NamePaint = new SolidColorPaint(SKColors.White),
                LabelsPaint = new SolidColorPaint(SKColors.White),
            }
        };

        // Y 轴
        YAxes = new Axis[]
        {
            new Axis
            {
                Name = "Voltage (V)",
                NamePaint = new SolidColorPaint(SKColors.White),
                LabelsPaint = new SolidColorPaint(SKColors.White),
            }
        };
    }
    /// <summary>
    /// 加载可用串口到 cbPort 下拉框
    /// </summary>
    private void LoadComPorts()
    {
        try
        {
            cmbComPort.Items.Clear();
            string[] ports = SerialPort.GetPortNames();

            foreach (string port in ports)
            {
                cmbComPort.Items.Add(port);
            }

            if (cmbComPort.Items.Count > 0)
            {
                cmbComPort.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            // Avalonia 错误提示
            GenericDialog.ShowMsg(this, $"加载串口失败：{ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 连接按钮点击事件
    /// </summary>
    /// <summary>
    /// 连接按钮点击事件
    /// </summary>
    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {

        try
        {
            if (cmbComPort.SelectedItem == null)
            {
                GenericDialog.ShowMsg(this, "请选择串口!", "错误");
                return;
            }

            string portName = cmbComPort.SelectedItem.ToString();

            // 初始化串口
            if (TH2883.serialPortTH2883 == null)
            {
                TH2883.serialPortTH2883 = new SerialPort();
            }

            if (TH2883.serialPortTH2883.IsOpen)
            {
                TH2883.serialPortTH2883.Close();
            }

            // 配置串口参数(根据TH2683A手册配置)
            TH2883.serialPortTH2883.PortName = portName;
            TH2883.serialPortTH2883.BaudRate = 19200;// 9600;  // 根据实际设备调整
            TH2883.serialPortTH2883.DataBits = 8;
            TH2883.serialPortTH2883.StopBits = StopBits.One;
            TH2883.serialPortTH2883.Parity = Parity.None;
            TH2883.serialPortTH2883.ReadTimeout = 30000;
            TH2883.serialPortTH2883.WriteTimeout = 30000;

            // 测试通讯
            string result = TH2883.IsComConnect();

            if (!string.IsNullOrEmpty(result) && result != "通讯失败" && result != "串口打开失败")
            {
                tbConnState.Text = "已连接";
                tbConnState.Foreground = Brushes.Green;
                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
                cmbComPort.IsEnabled = false;
                btnStart.IsEnabled = true;
                //grpParams.Enabled = true;

                GenericDialog.ShowMsg(this, $"连接成功!\n设备信息: {result}", "提示");
            }
            else
            {
                tbConnState.Text = "连接失败";
                tbConnState.Foreground = Brushes.Red;
                GenericDialog.ShowMsg(this, $"通讯测试失败: {result}\n请检查:\n1. 串口连接是否正常\n2. 设备是否开机\n3. 串口参数是否正确");

                if (TH2883.serialPortTH2883.IsOpen)
                {
                    TH2883.serialPortTH2883.Close();
                }
            }
        }
        catch (Exception ex)
        {
            GenericDialog.ShowMsg(this, $"连接失败: {ex.Message}", "错误");
            tbConnState.Text = "未连接";
            tbConnState.Foreground = Brushes.Red;
        }

    }
    /// <summary>
    /// 断开按钮点击事件
    /// </summary>
    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            //if (isTesting)
            //{
            //    GenericDialog.ShowMsg(this, "测试进行中,请先停止测试!", "提示");
            //    return;
            //}

            if (TH2883.serialPortTH2883 != null && TH2883.serialPortTH2883.IsOpen)
            {
                TH2883.serialPortTH2883.Close();
            }

            tbConnState.Text = "未连接";
            tbConnState.Foreground = Brushes.Red;
            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            cmbComPort.IsEnabled = true;
            btnStart.IsEnabled = false;
            // grpParams.Enabled = false;

            GenericDialog.ShowMsg(this, "已断开连接", "提示");
        }
        catch (Exception ex)
        {
            GenericDialog.ShowMsg(this, $"断开连接失败: {ex.Message}", "错误");
        }

    }
    TH2883 th2883;
    string twave1, twave2, twave3;
    /// <summary>
    /// 匝间耐压阻测试按钮点击事件
    /// </summary>
    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Debug.Write("开始测试!\r\n");
            Console.WriteLine("开始测试!\r\n");
            if (TH2883.serialPortTH2883 == null || !TH2883.serialPortTH2883.IsOpen)
            {
                GenericDialog.ShowMsg(this, "请先连接设备!", "提示");
                Debug.Write("请先连接设备!\r\n");
                Console.WriteLine("请先连接设备!\r\n");
                return;
            }
            // 🔥 开始前清空所有曲线（最重要）
            //if (Series != null)
            //{
            //    Array.Clear(Series, 0, Series.Length);
            //}

            //TH2883.Init(100,0,2000,2,"");
            
            th2883 = new TH2883();
            th2883.Init((int)para.set4, 100, 200, 1);
            //GenericDialog.ShowMsg(this, "请连接U-V");
            GenericDialog.ShowMsg(this, "请连接U-V", "提示");
            Thread.Sleep(500);
            if ((bool)radBiaoZhun.IsChecked && string.IsNullOrEmpty(para.set6))
            {
                GenericDialog.ShowMsg(this, "未能找到该型号标准波形，请先设置标准波形");
                Debug.Write("The interturn test is over");

                return;
            }
            else if ((bool)radBiaoZhun.IsChecked && !string.IsNullOrEmpty(para.set6))
            {
                Debug.Write("Importing the [U-V] standard waveform...");
                th2883.SetWave2(para.set6);
                Debug.Write("Importing the [U-V] waveform succeeded");
            }
            Thread.Sleep(500);
            //调用匝间方法
            th2883.TestStart();

            if ((bool)radBeiCe.IsChecked)
            {
                Thread.Sleep(500);
                //将UV的波形设置成标准波形
                th2883.SetWave();
                lblUV.Text = "Standard wave";
                lblUV.Foreground = Brushes.Black;// Color.WhiteSmoke;
                Thread.Sleep(1000);
            }
            //画曲线
            twave1 = th2883.GetWave();
            twave1 = twave1.TrimEnd(';');
            List<double> listRst = Get10jz(twave1);
            listRst = Transformation(listRst);
            // ========== 核心新增：更新U-V系列数据 ==========
            UpdateUVSeriesData(listRst);

            if ((bool)radBeiCe.IsChecked)
            {
                ra.ZJ_UV = "Standard wave";
            }
            else if ((bool)radBiaoZhun.IsChecked)
            {
                string sUVOK = th2883.GetResult();
                if (string.IsNullOrEmpty(sUVOK))
                {
                    Debug.Write("[U-V] result acquisition failed");
                }
                else
                {
                    string[] temp = sUVOK.Split(',');
                    if (temp[0] == "1")
                    {
                        lblUV.Text = "OK";
                        //lblUV.BackColor = Color.LawnGreen;
                        lblUV.Foreground = Brushes.Green;
                        ra.ZJ_UV = "OK";
                    }
                    else
                    {
                        lblUV.Text = "NG";
                        lblUV.Foreground = Brushes.Red;
                        ra.ZJ_UV = "NG";
                    }
                }
            }
            //GenericDialog.ShowMsg(this, "请连接V‑W");
            GenericDialog.ShowMsg(this, "请连接V‑W", "提示");
            Thread.Sleep(500);
            if ((bool)radBiaoZhun.IsChecked && string.IsNullOrEmpty(para.set6))
            {
                GenericDialog.ShowMsg(this, "未能找到该型号标准波形，请先设置标准波形");
                Debug.Write("The interturn test is over");

                return;
            }
            else if ((bool)radBiaoZhun.IsChecked && !string.IsNullOrEmpty(para.set6))
            {
                Debug.Write("Importing the [V-W] standard waveform...");
                th2883.SetWave2(para.set6);
                Debug.Write("Importing the [V-W] waveform succeeded");
            }
            Thread.Sleep(500);
            //调用匝间方法
            th2883.TestStart();

            if ((bool)radBeiCe.IsChecked)
            {
                Thread.Sleep(500);
                //将UV的波形设置成标准波形
                th2883.SetWave();
                lblVW.Text = "Standard wave";
                lblVW.Foreground = Brushes.Black;// Color.WhiteSmoke;
                Thread.Sleep(1000);
            }
            //画曲线
            twave2 = th2883.GetWave();
            twave2 = twave2.TrimEnd(';');
            List<double> listRst2 = Get10jz(twave2);
            listRst2 = Transformation(listRst2);
            // ========== 核心新增：更新V-W系列数据 ==========
            UpdateUVSeriesData(listRst2);

            if ((bool)radBeiCe.IsChecked)
            {
                ra.ZJ_VW = "Standard wave";
            }
            else if ((bool)radBiaoZhun.IsChecked)
            {
                string sVWOK = th2883.GetResult();
                if (string.IsNullOrEmpty(sVWOK))
                {
                    Debug.Write("[V-W] result acquisition failed");
                }
                else
                {
                    string[] temp = sVWOK.Split(',');
                    if (temp[0] == "1")
                    {
                        lblVW.Text = "OK";
                        //lblUV.BackColor = Color.LawnGreen;
                        lblVW.Foreground = Brushes.Green;
                        ra.ZJ_VW = "OK";
                    }
                    else
                    {
                        lblVW.Text = "NG";
                        lblVW.Foreground = Brushes.Red;
                        ra.ZJ_VW = "NG";
                    }
                }
            }

            //GenericDialog.ShowMsg(this, "请连接U‑W");
            GenericDialog.ShowMsg(this, "请连接U‑W", "提示");
            Thread.Sleep(500);
            if ((bool)radBiaoZhun.IsChecked && string.IsNullOrEmpty(para.set6))
            {
                GenericDialog.ShowMsg(this, "未能找到该型号标准波形，请先设置标准波形");
                Debug.Write("The interturn test is over");

                return;
            }
            else if ((bool)radBiaoZhun.IsChecked && !string.IsNullOrEmpty(para.set6))
            {
                Debug.Write("Importing the [U-W] standard waveform...");
                th2883.SetWave2(para.set6);
                Debug.Write("Importing the [U-W] waveform succeeded");
            }
            Thread.Sleep(500);
            //调用匝间方法
            th2883.TestStart();

            if ((bool)radBeiCe.IsChecked)
            {
                Thread.Sleep(500);
                //将UV的波形设置成标准波形
                th2883.SetWave();
                lblUW.Text = "Standard wave";
                lblUW.Foreground = Brushes.Black;// Color.WhiteSmoke;
                Thread.Sleep(1000);
            }
            //画曲线
            twave3 = th2883.GetWave();
            twave3 = twave1.TrimEnd(';');
            List<double> listRst3 = Get10jz(twave3);
            listRst3 = Transformation(listRst3);
            // ========== 核心新增：更新U-W系列数据 ==========
            UpdateUVSeriesData(listRst3);

            if ((bool)radBeiCe.IsChecked)
            {
                ra.ZJ_UW = "Standard wave";
            }
            else if ((bool)radBiaoZhun.IsChecked)
            {
                string sUWOK = th2883.GetResult();
                if (string.IsNullOrEmpty(sUWOK))
                {
                    Debug.Write("[U-W] result acquisition failed");
                }
                else
                {
                    string[] temp = sUWOK.Split(',');
                    if (temp[0] == "1")
                    {
                        lblUW.Text = "OK";
                        lblUW.Foreground = Brushes.Green;
                        ra.ZJ_UW = "OK";
                    }
                    else
                    {
                        lblUW.Text = "NG";
                        lblUW.Foreground = Brushes.Red;
                        ra.ZJ_UW = "NG";
                    }
                }
            }

        }
        catch (Exception ex)
        {
            GenericDialog.ShowMsg(this, $"开始测试失败: {ex.Message}", "错误");

        }
    }
    // 新增：更新U-V系列数据的方法
    private void UpdateUVSeriesData(List<double> newData)
    {
        // 遍历Series找到U-V系列
        for (int i = 0; i < Series.Length; i++)
        {
            if (Series[i] is LineSeries<double> lineSeries && lineSeries.Name == "U-V")
            {
                lineSeries.Values = Array.Empty<double>();

                // 替换为新数据
                lineSeries.Values = newData.ToArray();
                // 触发Series属性变更，通知UI刷新
                Series = Series.Clone() as ISeries[];
                break;
            }
            if (Series[i] is LineSeries<double> lineSeries2 && lineSeries2.Name == "V-W")
            {
                lineSeries2.Values = Array.Empty<double>();

                // 替换为新数据
                lineSeries2.Values = newData.ToArray();
                // 触发Series属性变更，通知UI刷新
                Series = Series.Clone() as ISeries[];
                break;
            }

            if (Series[i] is LineSeries<double> lineSeries3 && lineSeries3.Name == "U-W")
            {
                lineSeries3.Values = Array.Empty<double>();

                // 替换为新数据
                lineSeries3.Values = newData.ToArray();
                // 触发Series属性变更，通知UI刷新
                Series = Series.Clone() as ISeries[];
                break;
            }
        }
    }
    /// <summary>
    /// 停止匝间耐压测试按钮点击事件
    /// </summary>
    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Debug.Write("停止测试!\r\n");
            Console.WriteLine("停止测试!\r\n");
        }
        catch (Exception ex)
        {
            GenericDialog.ShowMsg(this, $"停止测试失败: {ex.Message}", "错误");
        }
    }
    private List<double> Get10jz(string sTwave)
    {
        List<string> listStr = new List<string>();
        for (int i = 0; i < ((sTwave.Length % 2) ==0 ? sTwave.Length : sTwave.Length-1);)
        {
            listStr.Add(sTwave.Substring(i, 2));
            i += 2;
        }
        List<double> listRst = new List<double>();
        for (int i = 0; i < listStr.Count; i++)
        {
            int d = Convert.ToInt32(listStr[i], 16);
            listRst.Add(d);
        }
        return listRst;
    }

    /// <summary>
    /// 通过倍率换算成实际电压
    /// </summary>
    public List<double> Transformation(List<double> list)
    {
        List<double> listGet = new List<double>();
        double f = 0;
        if (para.set4 < 1200)
        {
            f = 3000 / 255.0;
        }
        else if (para.set4 < 1800)
        {
            f = 4500 / 255.0;
        }
        else if (para.set4 < 2400)
        {
            f = 6000 / 255.0;
        }
        else if (para.set4 < 3600)
        {
            f = 9000 / 255.0;
        }
        else if (para.set4 < 4800)
        {
            f = 12000 / 255.0;
        }
        else if (para.set4 < 7200)
        {
            f = 18000 / 255.0;
        }
        else
        {
            f = 25000 / 255;
        }
        foreach (int item in list)
        {
            listGet.Add((item - 128) * f);
        }
        return listGet;
    }
}