using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// 心跳后台服务（接口地址来自 ProductionSystemConfig.ini「心跳接口」，间隔按 API 文档默认 300 秒）。
/// </summary>
public class HeartbeatService : IDisposable
{
    /// <summary>与接口文档 3.2 一致：5 分钟</summary>
    public const int DefaultHeartbeatSeconds = 300;

    private readonly EpeApiClient _api;
    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    public int HeartbeatIntervalSeconds { get; set; } = DefaultHeartbeatSeconds;

    public bool IsRunning { get; private set; }
    public DateTime? LastHeartbeatAt { get; private set; }
    public bool LastHeartbeatOk { get; private set; }

    public event EventHandler<bool>? HeartbeatStateChanged;

    public HeartbeatService(EpeApiClient api)
    {
        _api = api;
    }

    public void Start(string personCode)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _runningTask = Task.Run(() => RunLoopAsync(personCode, _cts.Token));
        IsRunning = true;
        NlogHelper.Info($"心跳服务启动 → 间隔 {HeartbeatIntervalSeconds}s");
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _runningTask?.Wait(2000);
        }
        catch { /* 忽略 */ }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _runningTask = null;
            IsRunning = false;
        }
    }

    private async Task RunLoopAsync(string personCode, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var resp = await _api.HeartbeatAsync(personCode);
                var ok = resp.IsSuccess;
                if (ok != LastHeartbeatOk)
                {
                    NlogHelper.Info($"心跳状态变化：{LastHeartbeatOk} → {ok}");
                    HeartbeatStateChanged?.Invoke(this, ok);
                }
                LastHeartbeatOk = ok;
                LastHeartbeatAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                NlogHelper.Warn(ex, "心跳异常");
                LastHeartbeatOk = false;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose() => Stop();
}
