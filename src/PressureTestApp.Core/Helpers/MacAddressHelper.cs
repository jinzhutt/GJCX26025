using System.Net.NetworkInformation;

namespace PressureTestApp.Core.Helpers;

/// <summary>
/// 本机网卡 MAC 扫描（与 GJCS frmAuthentication 一致）。
/// </summary>
public static class MacAddressHelper
{
    public sealed record MacEntry(string InterfaceName, string MacAddress)
    {
        public string DisplayText => $"网络名称：{InterfaceName}，MAC地址：{MacAddress}";

        public override string ToString() => DisplayText;
    }

    /// <summary>枚举可用有线/无线网卡，跳过 VMware 虚拟网卡。</summary>
    public static IReadOnlyList<MacEntry> ScanAvailable()
    {
        var list = new List<MacEntry>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ni.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
                continue;

            var mac = FormatMacAddress(ni.GetPhysicalAddress());
            if (string.IsNullOrEmpty(mac))
                continue;

            list.Add(new MacEntry(ni.Name, mac));
        }

        return list;
    }

    public static string FormatMacAddress(PhysicalAddress mac)
    {
        var bytes = mac.GetAddressBytes();
        if (bytes.Length == 0)
            return string.Empty;
        return string.Join("-", bytes.Select(b => b.ToString("X2")));
    }
}
