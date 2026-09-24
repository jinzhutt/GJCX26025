using System.Net.Sockets;

namespace PressureTestApp.Communication.Api;

/// <summary>MES 连通性快速探测：TCP 连不上时约 2 秒内失败，避免遮罩/界面干等十几秒。</summary>
public static class MesReachability
{
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

    /// <summary>解析 baseUrl 并尝试 TCP 连通；失败返回原因文案。</summary>
    public static async Task<(bool Ok, string? Error)> TryReachAsync(
        string? baseUrl,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return (false, "未配置 MES 地址。");

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
            return (false, $"MES 地址无效：{baseUrl}");

        var port = uri.IsDefaultPort
            ? (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : uri.Port;

        var limit = timeout ?? ProbeTimeout;
        try
        {
            using var client = new TcpClient();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(limit);
            await client.ConnectAsync(uri.Host, port, linked.Token);
            return (true, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, $"无法连接 MES（{uri.Host}:{port}，{limit.TotalSeconds:0} 秒超时）。请检查网络或地址。");
        }
        catch (Exception ex)
        {
            return (false, $"无法连接 MES（{uri.Host}:{port}）：{ex.Message}");
        }
    }
}
