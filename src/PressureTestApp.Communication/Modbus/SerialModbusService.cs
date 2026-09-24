using System.IO.Ports;
using System.Text.Json;
using NModbus;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Services;

namespace PressureTestApp.Communication.Modbus;

/// <summary>
/// 单仪器寄存器映射
/// </summary>
public class ModbusRegister
{
    public string Name { get; set; } = "";
    /// <summary>
    /// Float32 / Int16 / UInt16 / Int32 / UInt32
    /// </summary>
    public string Type { get; set; } = "Float32";
    public ushort Address { get; set; }
    public double Scale { get; set; } = 1.0;
    public string? Unit { get; set; }
}

/// <summary>
/// 单仪器配置
/// </summary>
public class InstrumentConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Port { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Parity Parity { get; set; } = Parity.None;
    public byte SlaveId { get; set; } = 1;
    public int PollIntervalMs { get; set; } = 500;
    public bool Enabled { get; set; } = true;
    public List<ModbusRegister> Registers { get; set; } = new();
}

/// <summary>
/// 串口仪器配置文件
/// </summary>
public class InstrumentsConfigFile
{
    public List<InstrumentConfig> Instruments { get; set; } = new();

    public static string FilePath => Path.Combine(AppConstants.ConfigDir, "instruments.json");

    public static InstrumentsConfigFile Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonSerializer.Deserialize<InstrumentsConfigFile>(json,
                    new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
                if (data != null) return data;
            }
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "加载串口仪器配置失败");
        }

        // 默认两路仪器
        return new InstrumentsConfigFile
        {
            Instruments =
            {
                new InstrumentConfig
                {
                    Id = "instrument-1", Name = "高压表 #1", Port = "COM3", Enabled = false,
                    Registers =
                    {
                        new ModbusRegister { Name = "实测电压", Type = "Float32", Address = 0, Scale = 0.001, Unit = "kV" },
                        new ModbusRegister { Name = "实测电流", Type = "Float32", Address = 2, Scale = 1.0,   Unit = "mA" }
                    }
                },
                new InstrumentConfig
                {
                    Id = "instrument-2", Name = "温湿度仪", Port = "COM4", Enabled = false,
                    Registers =
                    {
                        new ModbusRegister { Name = "温度", Type = "Float32", Address = 0, Scale = 0.1, Unit = "℃" },
                        new ModbusRegister { Name = "湿度", Type = "Float32", Address = 2, Scale = 0.1, Unit = "%" }
                    }
                }
            }
        };
    }

    public void Save()
    {
        Directory.CreateDirectory(AppConstants.ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });
        File.WriteAllText(FilePath, json);
    }
}

/// <summary>
/// 串口 Modbus 仪器服务（预留两路）
/// </summary>
public class SerialModbusService : ISerialInstrumentService
{
    private readonly Dictionary<string, (SerialPort Port, IModbusMaster Master)> _connections = new();
    private readonly InstrumentsConfigFile _config;

    public event EventHandler<InstrumentDataEventArgs>? DataReceived;

    public SerialModbusService(InstrumentsConfigFile config) => _config = config;

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public Task<bool> ConnectAsync(string instrumentId)
    {
        var ins = _config.Instruments.FirstOrDefault(x => x.Id == instrumentId);
        if (ins == null)
        {
            NlogHelper.Warn($"未找到仪器配置：{instrumentId}");
            return Task.FromResult(false);
        }

        try
        {
            var port = new SerialPort(ins.Port, ins.BaudRate, ins.Parity, ins.DataBits, ins.StopBits)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            port.Open();

            var factory = new ModbusFactory();
            var master = factory.CreateRtuMaster((NModbus.IO.IStreamResource)port);
            _connections[instrumentId] = (port, master);

            NlogHelper.Info($"仪器 {ins.Name}（{ins.Port}@{ins.BaudRate}）已连接");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, $"仪器 {ins.Name} 连接失败");
            return Task.FromResult(false);
        }
    }

    public Task DisconnectAsync(string instrumentId)
    {
        if (_connections.TryGetValue(instrumentId, out var c))
        {
            try { c.Master.Dispose(); c.Port.Close(); c.Port.Dispose(); } catch { /* ignore */ }
            _connections.Remove(instrumentId);
        }
        return Task.CompletedTask;
    }

    public bool IsConnected(string instrumentId) =>
        _connections.TryGetValue(instrumentId, out var c) && c.Port.IsOpen;

    public async Task<Dictionary<string, double>> ReadAllRegistersAsync(string instrumentId)
    {
        var result = new Dictionary<string, double>();
        var ins = _config.Instruments.FirstOrDefault(x => x.Id == instrumentId);
        if (ins == null || !_connections.TryGetValue(instrumentId, out var c))
            return result;

        foreach (var reg in ins.Registers)
        {
            try
            {
                ushort count = reg.Type.Contains("32") ? (ushort)2 : (ushort)1;
                var raw = await Task.Run(() => c.Master.ReadHoldingRegisters(ins.SlaveId, reg.Address, count));
                double value = ParseRegister(raw, reg.Type) * reg.Scale;
                result[reg.Name] = value;
            }
            catch (Exception ex)
            {
                NlogHelper.Warn(ex, $"读取 {ins.Name}.{reg.Name} 失败");
            }
        }

        DataReceived?.Invoke(this, new InstrumentDataEventArgs
        {
            InstrumentId = instrumentId,
            Values = result
        });

        return result;
    }

    private static double ParseRegister(ushort[] raw, string type)
    {
        if (raw.Length == 0) return 0;

        return type switch
        {
            "Int16"   => (short)raw[0],
            "UInt16"  => raw[0],
            "Int32"   => raw.Length >= 2 ? (raw[0] << 16) | raw[1] : raw[0],
            "UInt32"  => raw.Length >= 2 ? ((uint)raw[0] << 16) | raw[1] : raw[0],
            "Float32" => raw.Length >= 2
                ? BitConverter.ToSingle(new[]
                {
                    (byte)(raw[1] & 0xFF), (byte)((raw[1] >> 8) & 0xFF),
                    (byte)(raw[0] & 0xFF), (byte)((raw[0] >> 8) & 0xFF)
                }, 0)
                : 0,
            _ => raw[0]
        };
    }

    public void Dispose()
    {
        foreach (var c in _connections.Values)
        {
            try { c.Master.Dispose(); c.Port.Close(); c.Port.Dispose(); } catch { /* ignore */ }
        }
        _connections.Clear();
    }
}
