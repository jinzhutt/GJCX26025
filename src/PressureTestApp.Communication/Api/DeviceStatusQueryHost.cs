using System.Net;
using System.Text;
using System.Text.Json;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;

namespace PressureTestApp.Communication.Api;

/// <summary>
/// 协议 4.6 设备状态查询：本机作为 HTTP 服务，供 MES GET 查询是否可作业。
/// 地址示例：http://本机IP:5089/getDeviceStatus（端口读 ProductionSystemConfig.ini · MES本机状态查询端口）。
/// </summary>
public sealed class DeviceStatusQueryHost : IDisposable
{
    private readonly ProductionSystemConfig _config;
    private readonly DeviceWorkReadyState _state;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public DeviceStatusQueryHost(ProductionSystemConfig config, DeviceWorkReadyState state)
    {
        _config = config;
        _state = state;
    }

    public int Port { get; private set; } = 5089;
    public bool IsRunning => _listener?.IsListening == true;

    public void Start()
    {
        if (_listener != null)
            return;

        _state.Configure(_config.MesDeviceId);

        if (!int.TryParse((_config.MesLocalStatusPort ?? "").Trim(), out var port) || port is <= 0 or > 65535)
            port = 5089;
        Port = port;

        var prefixes = new[]
        {
            $"http://+:{port}/",
            $"http://192.168.40.27:{port}/"
        };

        Exception? lastError = null;
        foreach (var prefix in prefixes)
        {
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Start();
                _listener = listener;
                var queryUrl = (_config.MesDeviceStatusQueryUrl ?? "").Trim();
                if (string.IsNullOrWhiteSpace(queryUrl))
                    queryUrl = $"{prefix.TrimEnd('/')}getDeviceStatus";
                NlogHelper.Info($"MES 4.6 状态查询已启动：{prefix}getDeviceStatus（配置项：{queryUrl}）");
                lastError = null;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                NlogHelper.Warn(ex, $"MES 4.6 监听前缀失败：{prefix}");
            }
        }

        if (_listener == null)
        {
            NlogHelper.Error(lastError!, "MES 4.6 状态查询启动失败（可检查端口占用或 URL 保留）");
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "MES 4.6 状态查询停止异常");
        }
        finally
        {
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            _loop = null;
        }
    }

    public void Dispose() => Stop();

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is { IsListening: true })
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                NlogHelper.Warn(ex, "MES 4.6 接受连接异常");
                continue;
            }

            _ = Task.Run(() => HandleRequest(ctx), ct);
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            var isStatusPath = path.Equals("/getDeviceStatus", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith("/getDeviceStatus", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(ctx.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) || !isStatusPath)
            {
                ctx.Response.StatusCode = 404;
                WriteText(ctx.Response, "Not Found");
                return;
            }

            var (deviceId, status, reason) = _state.Snapshot();
            var payload = new
            {
                deviceId = deviceId,
                status
            };

            var json = JsonSerializer.Serialize(payload);
            NlogHelper.Info($"MES 4.6 查询响应：status={status}，reason={reason}");
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            WriteText(ctx.Response, json);
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "MES 4.6 处理请求异常");
            try
            {
                ctx.Response.StatusCode = 500;
                WriteText(ctx.Response, ex.Message);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static void WriteText(HttpListenerResponse response, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.OutputStream.Close();
    }
}
