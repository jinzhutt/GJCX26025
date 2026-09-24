using System.Globalization;
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PressureTestApp.UI.ViewModels;

/// <summary>模拟量输出等场景：大图数字编辑弹窗。</summary>
public partial class NumericEditDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _titleBarText = "";// "编辑压力值"、"编辑流量值"等
    [ObservableProperty] private string _editValueText = "";// 绑定到输入框，构造时根据 currentValue、decimalPlaces 初始化为合适的字符串格式
    [ObservableProperty] private string? _errorHint;// 验证失败时显示的错误提示，成功或未验证时为 null

    public double Min { get; }//最小值
    public double Max { get; }//最大值
    public int DecimalPlaces { get; }

    public double ResultValue { get; private set; }
    public bool DialogAccepted { get; private set; }

    public NumericEditDialogViewModel(string titleBarText, double currentValue, double min, double max, int decimalPlaces)// 通过构造参数传入当前值、范围和小数位数，初始化 EditValueText 和其他属性
    {
        TitleBarText = titleBarText;// "编辑压力值"、"编辑流量值"等
        Min = min;
        Max = max;
        DecimalPlaces = decimalPlaces;
        EditValueText = decimalPlaces <= 0
            ? ((int)Math.Round(currentValue)).ToString(CultureInfo.InvariantCulture)
            : currentValue.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);// 初始化输入框文本，整数不显示小数点，小数显示固定小数位
    }

    public bool TryConfirm()// 验证输入是否合法，合法则设置 ResultValue 和 DialogAccepted
    {
        ErrorHint = null;
        var s = EditValueText.Trim().Replace(',', '.');
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            ErrorHint = "请输入有效数字";
            return false;
        }

        if (v < Min || v > Max)
        {
            ErrorHint = DecimalPlaces <= 0
                ? $"允许范围 {(int)Min}～{(int)Max}"
                : $"允许范围 {Min.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture)}～{Max.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture)}";
            return false;
        }

        if (DecimalPlaces <= 0)
            v = Math.Round(v);
        else
            v = Math.Round(v, DecimalPlaces);

        ResultValue = v;
        DialogAccepted = true;
        return true;
    }

    public void MarkCancelled() => DialogAccepted = false;// 取消时不需要验证输入，直接标记 DialogAccepted = false，关闭窗口后调用方根据这个属性判断是否使用 ResultValue
}
