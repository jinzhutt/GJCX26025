namespace PressureTestApp.Communication.Api;

/// <summary>
/// 协议 4.6：供本机 HTTP 查询的「是否可作业」状态（由主界面根据 PLC/试验状态刷新）。
/// </summary>
public sealed class DeviceWorkReadyState
{
    private readonly object _lock = new();
    private string _deviceId = "";
    private bool _canWork = true;
    private string _reason = "待机";

    public void Configure(string deviceId)
    {
        lock (_lock)
            _deviceId = deviceId?.Trim() ?? "";
    }

    /// <summary>更新是否可作业。status 对外为 "1"/"0"。</summary>
    public void Update(bool canWork, string reason)
    {
        lock (_lock)
        {
            _canWork = canWork;
            _reason = reason ?? "";
        }
    }

    public (string DeviceId, string Status, string Reason) Snapshot()
    {
        lock (_lock)
            return (_deviceId, _canWork ? "1" : "0", _reason);
    }
}
