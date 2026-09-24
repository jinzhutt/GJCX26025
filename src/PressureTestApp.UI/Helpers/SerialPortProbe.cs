using System;
using System.IO.Ports;

namespace PressureTestApp.UI.Helpers;

/// <summary>检测本机是否有可用串口（用于绝缘测试等入口提示）。</summary>
public static class SerialPortProbe
{
    public static bool HasAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames().Length > 0;
        }
        catch (Exception ex)
        {
            Core.Helpers.NlogHelper.Warn($"枚举串口失败：{ex.Message}");
            return false;
        }
    }

    public static string[] GetPortNamesSafe()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch (Exception ex)
        {
            Core.Helpers.NlogHelper.Warn($"枚举串口失败：{ex.Message}");
            return [];
        }
    }
}
