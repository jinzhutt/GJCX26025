using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Services;
using S7.Net;

namespace PressureTestApp.Communication.Plc;

/// <summary>
/// 西门子 S7 PLC 通信实现（替代原 OPC + KEPServerEx）。
///
/// 地址格式（与 S7NetPlus 一致）：
///   "DB1.DBX0.0"  → 数据块 1，字节 0，位 0（Bool）
///   "DB1.DBW2"    → 数据块 1，字节 2 起的 Word（Int16）
///   "DB1.DBD4"    → 数据块 1，字节 4 起的 DWord（Int32 / Real）
///   "M0.0"        → M 区位
///   "I0.0"/"Q0.0" → 输入/输出位
/// </summary>
public class S7Communicator : IPlcCommunicator
{
    private readonly PlcConfig _config;
    private S7.Net.Plc? _plc;
    private bool _isConnected;

    public bool IsConnected => _isConnected && _plc != null && _plc.IsConnected;

    public event EventHandler<PlcDataChangedEventArgs>? DataChanged;
    public event EventHandler<bool>? ConnectionChanged;

    public S7Communicator(PlcConfig config) => _config = config;

    public async Task<bool> ConnectAsync()
    {
        try
        {
            var cpu = _config.CpuType.ToUpperInvariant() switch
            {
                "S7200" => CpuType.S7200,
                "S7300" => CpuType.S7300,
                "S7400" => CpuType.S7400,
                "S71200" => CpuType.S71200,
                "S71500" => CpuType.S71500,
                _ => CpuType.S71200
            };

            _plc = new S7.Net.Plc(cpu, _config.IP, (short)_config.Rack, (short)_config.Slot);

            using var cts = new CancellationTokenSource(_config.ConnectTimeout);
            await _plc.OpenAsync(cts.Token);

            _isConnected = _plc.IsConnected;
            ConnectionChanged?.Invoke(this, _isConnected);

            NlogHelper.Info(_isConnected
                ? $"PLC 已连接：{cpu} @ {_config.IP}"
                : $"PLC 连接失败：{_config.IP}");

            return _isConnected;
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "PLC 连接异常");
            _isConnected = false;
            ConnectionChanged?.Invoke(this, false);
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        try
        {
            _plc?.Close();
            _isConnected = false;
            ConnectionChanged?.Invoke(this, false);
            NlogHelper.Info("PLC 已断开");
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "PLC 断开异常");
        }
        return Task.CompletedTask;
    }

    public async Task<bool> ReadBoolAsync(string address)
    {
        EnsureConnected();
        var v = await _plc!.ReadAsync(address);
        return Convert.ToBoolean(v);
    }

    public async Task<short> ReadInt16Async(string address)
    {
        EnsureConnected();
        var v = await _plc!.ReadAsync(address);
        return Convert.ToInt16(v);
    }

    public async Task<int> ReadInt32Async(string address)
    {
        EnsureConnected();
        var v = await _plc!.ReadAsync(address);
        return Convert.ToInt32(v);
    }

    public async Task<float> ReadFloatAsync(string address)
    {
        EnsureConnected();
        var v = await _plc!.ReadAsync(address);
        // S7NetPlus 读取 DWord 后需要按 IEEE754 解释为 float
        return v switch
        {
            float f => f,
            uint u => BitConverter.ToSingle(BitConverter.GetBytes(u), 0),
            int i => BitConverter.ToSingle(BitConverter.GetBytes(i), 0),
            _ => Convert.ToSingle(v)
        };
    }

    public async Task WriteBoolAsync(string address, bool value)
    {
        EnsureConnected();
        await _plc!.WriteAsync(address, value);
        DataChanged?.Invoke(this, new PlcDataChangedEventArgs { Address = address, Value = value });
    }

    public async Task WriteInt16Async(string address, short value)
    {
        EnsureConnected();
        await _plc!.WriteAsync(address, value);
        DataChanged?.Invoke(this, new PlcDataChangedEventArgs { Address = address, Value = value });
    }

    public async Task WriteFloatAsync(string address, float value)
    {
        EnsureConnected();
        await _plc!.WriteAsync(address, value);
        DataChanged?.Invoke(this, new PlcDataChangedEventArgs { Address = address, Value = value });
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("PLC 未连接");
    }

    public void Dispose()
    {
        try { _plc?.Close(); } catch { /* ignore */ }
        _plc = null;
        _isConnected = false;
    }
}

/// <summary>
/// 模拟 PLC（无设备调试用）—— 返回随机数据，不做真实通信
/// </summary>
public class MockPlcCommunicator : IPlcCommunicator
{
    private bool _connected;
    private readonly Random _rng = new();

    public bool IsConnected => _connected;
    public event EventHandler<PlcDataChangedEventArgs>? DataChanged;
    public event EventHandler<bool>? ConnectionChanged;

    public Task<bool> ConnectAsync()
    {
        _connected = true;
        ConnectionChanged?.Invoke(this, true);
        NlogHelper.Info("[Mock] PLC 已连接");
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        _connected = false;
        ConnectionChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public Task<bool> ReadBoolAsync(string address) => Task.FromResult(_rng.Next(2) == 1);
    public Task<short> ReadInt16Async(string address) => Task.FromResult((short)_rng.Next(0, 1000));
    public Task<int> ReadInt32Async(string address) => Task.FromResult(_rng.Next(0, 100000));
    public Task<float> ReadFloatAsync(string address) => Task.FromResult((float)(_rng.NextDouble() * 15.0));

    public Task WriteBoolAsync(string address, bool value) { DataChanged?.Invoke(this, new() { Address = address, Value = value }); return Task.CompletedTask; }
    public Task WriteInt16Async(string address, short value) { DataChanged?.Invoke(this, new() { Address = address, Value = value }); return Task.CompletedTask; }
    public Task WriteFloatAsync(string address, float value) { DataChanged?.Invoke(this, new() { Address = address, Value = value }); return Task.CompletedTask; }

    public void Dispose() => _connected = false;
}
