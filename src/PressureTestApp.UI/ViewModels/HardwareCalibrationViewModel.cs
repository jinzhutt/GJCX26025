using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Core.Helpers;
using PressureTestApp.UI.Views;
using PressureTestApp.Communication.Plc;

namespace PressureTestApp.UI.ViewModels;

/// <summary>
/// 硬件校准 — 零点/增益读写 config\rw.ini（与 GJCS UCCalibration 一致）。
/// </summary>
public partial class HardwareCalibrationViewModel : ObservableObject
{
    internal readonly IPlc200Communicator? _plc;

    public ObservableCollection<HardwareCalibrationChannelViewModel> InputChannels { get; } = new();

    public HardwareCalibrationChannelViewModel OutputChannel { get; }

    public Action? NavigateBackToMainTest { get; set; }

    public Window? HostWindow { get; set; }

    public HardwareCalibrationViewModel()
    {
        _plc = App.Services.GetService<IPlc200Communicator>();
        RwIniFile.EnsureExists();

        foreach (var def in RwIniFile.InputChannels)
            InputChannels.Add(new HardwareCalibrationChannelViewModel(def, this));

        OutputChannel = new HardwareCalibrationChannelViewModel(RwIniFile.OutputChannel, this);
    }

    private System.Timers.Timer? _updateTimer;

    public void StartRealTimeUpdate()
    {
        if (_plc == null)
            return;

        _updateTimer = new System.Timers.Timer(100);
        _updateTimer.Elapsed += OnUpdateTimerElapsed;
        _updateTimer.Start();
    }

    public void StopRealTimeUpdate()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _updateTimer = null;
    }

    private void OnUpdateTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_plc == null || !_plc.IsConnected)
            return;

        try
        {
            var plcData = _plc.GetPlcStatusList();
            if (plcData == null || plcData.Count == 0)
                return;

            var data = plcData.ToArray();
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var channel in InputChannels)
                    channel.UpdateMeasuredValue(data);

                OutputChannel.UpdateMeasuredValue(data);
            });
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "硬件校准·实时更新失败");
        }
    }

    [RelayCommand]
    private async Task LoadFromPlc()
    {
        if (_plc == null || !_plc.IsConnected)
        {
            await ShowNoticeAsync("PLC 未连接");
            return;
        }

        try
        {
            var plcData = await Task.Run(() => _plc.GetPlcStatusList());
            if (plcData == null || plcData.Count == 0)
            {
                await ShowNoticeAsync("未能从 PLC 读取数据");
                return;
            }

            foreach (var channel in InputChannels)
                await channel.LoadFromPlcAsync(plcData);

            await OutputChannel.LoadFromPlcAsync(plcData);

            NlogHelper.Info("硬件校准·已从 PLC 加载");
            await ShowNoticeAsync("已从 PLC 加载校准参数");
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "硬件校准·从 PLC 加载失败");
            await ShowNoticeAsync($"加载失败：{ex.Message}");
        }
    }

    private async Task ShowNoticeAsync(string message)
    {
        if (HostWindow != null)
            await new InfoNoticeWindow(message).ShowDialog(HostWindow);
    }

    [RelayCommand]
    private void Close() => NavigateBackToMainTest?.Invoke();
}

public partial class HardwareCalibrationChannelViewModel : ObservableObject
{
    private readonly RwCalibrationChannelDef _def;
    private readonly HardwareCalibrationViewModel _parent;

    public string IniSection => _def.IniSection;

    [ObservableProperty] private string _rowTitle;

    [ObservableProperty] private string _measuredText = "0";
    [ObservableProperty] private string _zeroText = "0";
    [ObservableProperty] private string _gainText = "1";

    private string MeasuredTag => _def.IsOutput switch
    {
        true => "PUB.P00",
        false => _def.PlcIndex switch
        {
            0 => "AI.MAI00",
            1 => "AI.MAI01",
            2 => "AI.MAI02",
            3 => "AI.MAI03",
            _ => ""
        }
    };

    private (string ZeroTag, string GainTag) GetPlcTags()
    {
        return _def.IsOutput switch
        {
            true => ("PUB.P03", "PUB.P04"),
            false => _def.PlcIndex switch
            {
                0 => ("AI.Zero00", "AI.Gain00"),
                1 => ("AI.Zero01", "AI.Gain01"),
                2 => ("AI.Zero02", "AI.Gain02"),
                3 => ("AI.Zero03", "AI.Gain03"),
                _ => ("", "")
            }
        };
    }

    public void UpdateMeasuredValue(PressureTestApp.Communication.Plc.PlcDataModel[] plcData)
    {
        var tag = MeasuredTag;
        if (string.IsNullOrEmpty(tag))
            return;

        var item = plcData.FirstOrDefault(d =>
            string.Equals(d.TagName, tag, StringComparison.OrdinalIgnoreCase));
        if (item != null && item.Value != null)
        {
            MeasuredText = Convert.ToDecimal(item.Value).ToString("#0.00");
        }
        else
        {
            MeasuredText = "0";
        }
    }

    public HardwareCalibrationChannelViewModel(RwCalibrationChannelDef def, HardwareCalibrationViewModel parent)
    {
        _def = def;
        _parent = parent;
        _rowTitle = def.DisplayTitle;
        LoadFromIni();
    }

    public void LoadFromIni()
    {
        var v = RwIniFile.LoadChannel(_def.IniSection, _def.PlcIndex);
        ZeroText = Format(v.Zero);
        GainText = Format(v.Gain);
    }

    public async Task LoadFromPlcAsync(System.Collections.Generic.List<PlcDataModel> plcData)
    {
        var (zeroTag, gainTag) = GetPlcTags();
        if (string.IsNullOrEmpty(zeroTag) || string.IsNullOrEmpty(gainTag))
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ZeroText = GetPlcFloatValue(plcData, zeroTag, "0");
            GainText = GetPlcFloatValue(plcData, gainTag, "1");
        });
    }

    private static string GetPlcFloatValue(System.Collections.Generic.List<PlcDataModel> plcData, string tagName, string defaultValue)
    {
        var item = plcData.FirstOrDefault(d =>
            string.Equals(d.TagName, tagName, StringComparison.OrdinalIgnoreCase));
        if (item != null && item.Value != null)
        {
            return item.Value.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (!TryParseZeroGain(out var zero, out var gain, out var err))
        {
            await ShowNoticeAsync(err);
            return;
        }

        try
        {
            RwIniFile.SaveChannel(_def.IniSection, zero, gain, _def.PlcIndex);
            var path = RwIniFile.ResolvePath();

            if (_parent._plc != null && _parent._plc.IsConnected)
            {
                await SaveToPlcAsync(zero, gain);
            }

            NlogHelper.Info($"硬件校准·确定：{RowTitle} 零点={zero} 增益={gain} → {path}");
            var msg = $"已保存到 rw.ini\r";
            if (_parent._plc?.IsConnected == true)
                msg += "\n已同步到 PLC";
            await ShowNoticeAsync(msg);
        }
        catch (Exception ex)
        {
            NlogHelper.Info($"硬件校准·保存失败：{ex.Message}");
            await ShowNoticeAsync($"保存失败：{ex.Message}");
        }
    }

    private async Task SaveToPlcAsync(double zero, double gain)
    {
        if (_parent._plc == null || !_parent._plc.IsConnected)
            return;

        var (zeroTag, gainTag) = GetPlcTags();
        if (string.IsNullOrEmpty(zeroTag) || string.IsNullOrEmpty(gainTag))
            return;

        try
        {
            await Task.Run(() =>
            {
                _parent._plc.WriteTag(zeroTag, (float)zero);
                _parent._plc.WriteTag(gainTag, (float)gain);
            });
            NlogHelper.Info($"硬件校准·已写入 PLC：{RowTitle} 零点={zero} 增益={gain}");
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, $"硬件校准·写入 PLC 失败：{RowTitle}");
            throw;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        ZeroText = "0";
        GainText = "1";
        NlogHelper.Info($"硬件校准·重置：{RowTitle}（仅界面，点「确定」后写入文件）");
    }

    private async Task ShowNoticeAsync(string message)
    {
        var owner = _parent.HostWindow;
        if (owner != null)
            await new InfoNoticeWindow(message).ShowDialog(owner);
    }

    private bool TryParseZeroGain(out double zero, out double gain, out string error)
    {
        zero = 0;
        gain = 1;
        error = "";

        if (!TryParseDouble(ZeroText, out zero))
        {
            error = $"「{RowTitle}」零点值格式无效。";
            return false;
        }

        if (!TryParseDouble(GainText, out gain))
        {
            error = $"「{RowTitle}」增益值格式无效。";
            return false;
        }

        return true;
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        value = 0;
        var s = (text ?? "").Trim();
        if (string.IsNullOrEmpty(s) || s == "-")
            return true;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               || double.TryParse(s, out value);
    }

    private static string Format(double v) =>
        v.ToString(CultureInfo.InvariantCulture);
}
