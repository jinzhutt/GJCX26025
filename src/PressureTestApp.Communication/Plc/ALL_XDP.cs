using NModbus;
using NModbus.Device;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;


namespace PressureTestApp.Communication.Plc
{
    public interface IXdpPlcCommunicator : IDisposable
    {
        bool IsConnected { get; }
        void Stop();
        string ConnectionMessage { get; }
        void MappingPlcFromCSV(string csvPath);
        void WriteTag(string tagName, object value);
        void WriteDataToPlc(string tagName, object value);
        List<XdpPlcDataModel> GetPlcStatusList();
        void Disconnect();
    }

    public class XdpPlcCommunicator : IXdpPlcCommunicator
    {
        public bool IsConnected
        {
            get
            {
                if (tcpClient != null)
                {
                    return tcpClient.Connected;
                }
                return false;
            }
        }

        public string ConnectionMessage
        {
            get { return conStr; }
        }

        public List<XdpPlcDataModel> GetPlcStatusList()
        {
            return PlcStatusList;
        }

        public void Disconnect()
        {
            IsExit = true;
            if (WorkThread != null)
            {
                WorkThread.Join(1000);
            }
            if (modbusMaster is IDisposable disposableMaster)
            {
                disposableMaster.Dispose();
                modbusMaster = null;
            }
            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient = null;
            }
        }

        private TcpClient? tcpClient;
        private IModbusMaster? modbusMaster;
        private byte slaveId = 1;

        public List<XdpPlcDataModel> PlcStatusList;
        public string conStr = "";

        public XdpPlcCommunicator(string ip, int port = 502)
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.ReceiveTimeout = 5000;
                tcpClient.SendTimeout = 5000;
                tcpClient.Connect(ip, port);
                modbusMaster = new ModbusFactory().CreateMaster(tcpClient);

                if (tcpClient.Connected)
                {
                    Console.WriteLine("✅ XDP PLC连接成功！");
                    conStr = "✅ XDP PLC连接成功！";
                }
                else
                {
                    Console.WriteLine("❌ XDP PLC连接失败！");
                    conStr = "❌ XDP PLC连接失败！";
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("❌ 网络错误：" + ex.Message);
                conStr = "❌ 网络错误：" + ex.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ 通信出错：" + ex.Message);
                conStr = "❌ 通信出错：" + ex.Message;
            }
        }

        Thread WorkThread = null;
        List<XdpPlcDataModel> plcDataList;
        public bool IsExit = false;

        public void MappingPlcFromCSV(string path)
        {
            if (!IsConnected)
                throw new InvalidOperationException("PLC 未连接");

            plcDataList = ParseCsv(path);
            PlcStatusList = ReadDataFromPlc(plcDataList);
            IsExit = false;
            WorkThread = new Thread(ReadFromPLC);
            if (WorkThread != null)
                WorkThread.Start();
        }

        void ReadFromPLC()
        {
            if (plcDataList == null) return;
            while (true)
            {
                PlcStatusList = ReadDataFromPlc(plcDataList);
                if (IsExit) break;
                Thread.Sleep(50);
            }
        }

        public List<XdpPlcDataModel> ParseCsv(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("CSV 文件不存在", filePath);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                MissingFieldFound = args => { }
            };

            using (var reader = new StreamReader(filePath, System.Text.Encoding.UTF8))
            {
                using (var csv = new CsvReader(reader, config))
                {
                    csv.Context.RegisterClassMap<XdpPlcDataModelMap>();

                    try
                    {
                        var plcDataList = csv.GetRecords<XdpPlcDataModel>().ToList();
                        Console.WriteLine("=== XDP CSV 解析完成 ===");
                        Console.WriteLine("总解析条数：" + plcDataList.Count + " 条");
                        if (plcDataList.Count > 0)
                        {
                            Console.WriteLine("示例数据（第一条）：");
                            Console.WriteLine("  变量名称：" + plcDataList[0].TagName);
                            Console.WriteLine("  PLC 地址：" + plcDataList[0].PlcAddress);
                            Console.WriteLine("  数据类型：" + plcDataList[0].ValueType);
                            Console.WriteLine("  解析后类型：" + plcDataList[0].AddressType + "（地址：" + plcDataList[0].StartAddress + "）");
                        }

                        return plcDataList;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("CSV 解析失败：" + ex.Message, ex);
                    }
                }
            }
        }

        public List<XdpPlcDataModel> ReadDataFromPlc(List<XdpPlcDataModel> plcDataList)
        {
            if (modbusMaster == null)
                return plcDataList;

            foreach (var data in plcDataList)
            {
                try
                {
                    switch (data.AddressType)
                    {
                        case XdpAddressType.Coil:
                            bool[] coilValue = modbusMaster.ReadCoils(slaveId, data.StartAddress, 1);
                            data.Value = coilValue[0];
                            break;

                        case XdpAddressType.HoldingRegister:
                            if (data.ValueType == XdpDataValueType.Float)
                            {
                                ushort[] regs = modbusMaster.ReadHoldingRegisters(slaveId, data.StartAddress, 2);
                                byte[] bytes = new byte[4];
                                Buffer.BlockCopy(regs, 0, bytes, 0, 4);
                                data.Value = BitConverter.ToSingle(bytes, 0);
                            }
                            else if (data.ValueType == XdpDataValueType.DWord)
                            {
                                ushort[] regs = modbusMaster.ReadHoldingRegisters(slaveId, data.StartAddress, 2);
                                byte[] bytes = new byte[4];
                                Buffer.BlockCopy(regs, 0, bytes, 0, 4);
                                data.Value = BitConverter.ToInt32(bytes, 0);
                            }
                            else if (data.ValueType == XdpDataValueType.Word)
                            {
                                ushort[] regs = modbusMaster.ReadHoldingRegisters(slaveId, data.StartAddress, 1);
                                data.Value = regs[0];
                            }
                            else if (data.ValueType == XdpDataValueType.Boolean)
                            {
                                bool[] holdingCoilValue = modbusMaster.ReadCoils(slaveId, data.StartAddress, 1);
                                data.Value = holdingCoilValue[0];
                            }
                            break;

                        case XdpAddressType.InputRegister:
                            if (data.ValueType == XdpDataValueType.Float)
                            {
                                ushort[] regs = modbusMaster.ReadInputRegisters(slaveId, data.StartAddress, 2);
                                byte[] bytes = new byte[4];
                                Buffer.BlockCopy(regs, 0, bytes, 0, 4);
                                data.Value = BitConverter.ToSingle(bytes, 0);
                            }
                            else
                            {
                                ushort[] regs = modbusMaster.ReadInputRegisters(slaveId, data.StartAddress, 1);
                                data.Value = regs[0];
                            }
                            break;

                        case XdpAddressType.DiscreteInput:
                            bool[] discreteValue = modbusMaster.ReadInputs(slaveId, data.StartAddress, 1);
                            data.Value = discreteValue[0];
                            break;
                    }

                    data.UpdateTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("读取失败 " + data.Description + "：" + ex.Message);
                }
            }
            return plcDataList;
        }

        public void WriteDataToPlc(XdpPlcDataModel data)
        {
            if (!IsConnected || modbusMaster == null)
                throw new InvalidOperationException("PLC 未连接");

            try
            {
                switch (data.AddressType)
                {
                    case XdpAddressType.Coil:
                        if (data.Value is bool)
                        {
                            bool boolValue = (bool)data.Value;
                            modbusMaster.WriteSingleCoil(slaveId, data.StartAddress, boolValue);
                        }
                        break;

                    case XdpAddressType.HoldingRegister:
                        if (data.ValueType == XdpDataValueType.Float)
                        {
                            float floatValue = Convert.ToSingle(data.Value);
                            byte[] floatBytes = BitConverter.GetBytes(floatValue);
                            ushort low = BitConverter.ToUInt16(floatBytes, 0);
                            ushort high = BitConverter.ToUInt16(floatBytes, 2);
                            modbusMaster.WriteMultipleRegisters(slaveId, data.StartAddress, new ushort[] { low, high });
                        }
                        else if (data.ValueType == XdpDataValueType.DWord)
                        {
                            int dwordValue = Convert.ToInt32(data.Value);
                            byte[] dwordBytes = BitConverter.GetBytes(dwordValue);
                            ushort low = BitConverter.ToUInt16(dwordBytes, 0);
                            ushort high = BitConverter.ToUInt16(dwordBytes, 2);
                            modbusMaster.WriteMultipleRegisters(slaveId, data.StartAddress, new ushort[] { low, high });
                        }
                        else if (data.ValueType == XdpDataValueType.Word)
                        {
                            ushort wordValue = Convert.ToUInt16(data.Value);
                            modbusMaster.WriteSingleRegister(slaveId, data.StartAddress, wordValue);
                        }
                        else if (data.ValueType == XdpDataValueType.Boolean)
                        {
                            if (data.Value is bool)
                            {
                                bool boolValue = (bool)data.Value;
                                modbusMaster.WriteSingleCoil(slaveId, data.StartAddress, boolValue);
                            }
                        }
                        break;
                }

                Console.WriteLine("成功写入：" + data.Description + "（" + data.PlcAddress + " = " + data.Value + "）");
            }
            catch (Exception ex)
            {
                Console.WriteLine("写入失败 " + data.Description + "：" + ex.Message);
            }
        }

        public void WriteTag(string tag, object value)
        {
            List<XdpPlcDataModel> WorkPlc = PlcStatusList;

            foreach (XdpPlcDataModel info in WorkPlc)
            {
                if (string.IsNullOrEmpty(info.TagName))
                {
                    continue;
                }
                if (tag == info.TagName)
                {
                    info.Value = value;
                    WriteDataToPlc(info);
                    break;
                }
                if (IsExit) break;
            }
        }

        public void WriteDataToPlc(string tagName, object value)
        {
            WriteTag(tagName, value);
        }

        public void Stop()
        {
            IsExit = true;
            if (WorkThread != null && WorkThread.IsAlive)
            {
                WorkThread.Join(2000);
            }
        }

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                IsExit = true;
                if (WorkThread != null && WorkThread.IsAlive)
                {
                    WorkThread.Join(2000);
                }
            }

            if (modbusMaster is IDisposable disposableMaster)
            {
                disposableMaster.Dispose();
                modbusMaster = null;
            }
            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient = null;
            }

            _disposed = true;
        }
    }

    public enum XdpAddressType
    {
        [Description("线圈（Coil）")]
        Coil,
        [Description("保持寄存器（Holding Register）")]
        HoldingRegister,
        [Description("输入寄存器（Input Register）")]
        InputRegister,
        [Description("离散输入（Discrete Input）")]
        DiscreteInput
    }

    public enum XdpDataValueType
    {
        [Description("布尔型（位数据）")]
        Boolean,
        [Description("字（16位）")]
        Word,
        [Description("双字（32位）")]
        DWord,
        [Description("浮点型（32位）")]
        Float
    }

    public class XdpPlcDataModel
    {
        public string TagName { get; set; }

        public string PlcAddress { get; set; }

        public XdpAddressType AddressType { get; set; }

        public ushort StartAddress { get; set; }

        public XdpDataValueType ValueType { get; set; }

        public object Value { get; set; }

        public string Description { get; set; }

        public DateTime UpdateTime { get; set; }
    }

    public class XdpPlcDataModelMap : ClassMap<XdpPlcDataModel>
    {
        public XdpPlcDataModelMap()
        {
            Map(m => m.TagName).Name("Tag Name");
            Map(m => m.PlcAddress).Name("Address");
            Map(m => m.Description).Name("Description");

            Map(m => m.ValueType)
                .Name("Data Type")
                .Convert(args =>
                {
                    string dataType = args.Row.GetField<string>("Data Type").ToLower();
                    switch (dataType)
                    {
                        case "boolean":
                        case "布尔":
                            return XdpDataValueType.Boolean;
                        case "word":
                        case "字":
                            return XdpDataValueType.Word;
                        case "dword":
                        case "双字":
                            return XdpDataValueType.DWord;
                        case "float":
                        case "浮点":
                            return XdpDataValueType.Float;
                        default:
                            throw new ArgumentException("不支持的数据类型：" + dataType + "（行，变量" + args.Row.GetField<string>("Tag Name") + "）");
                    }
                });

            Map(m => m.AddressType)
                .Convert(args =>
                {
                    string address = args.Row.GetField<string>("Address").TrimStart();
                    if (string.IsNullOrEmpty(address))
                        throw new ArgumentException("地址不能为空");

                    if (address.StartsWith("HD", StringComparison.OrdinalIgnoreCase))
                        return XdpAddressType.HoldingRegister;
                    if (address.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                        return XdpAddressType.HoldingRegister;
                    if (address.StartsWith("HM", StringComparison.OrdinalIgnoreCase))
                        return XdpAddressType.Coil;
                    if (address.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                        return XdpAddressType.Coil;
                    if (address.StartsWith("Y", StringComparison.OrdinalIgnoreCase))
                        return XdpAddressType.Coil;
                    if (address.StartsWith("X", StringComparison.OrdinalIgnoreCase))
                        return XdpAddressType.DiscreteInput;

                    return XdpAddressType.HoldingRegister;
                });

            Map(m => m.StartAddress)
                .Convert(args =>
                {
                    string address = args.Row.GetField<string>("Address");
                    if (string.IsNullOrEmpty(address))
                        throw new ArgumentException("地址不能为空");

                    ushort offset = 0;
                    string numPart = "";

                    if (address.StartsWith("HD", StringComparison.OrdinalIgnoreCase))
                    {
                        offset = 0xA080;
                        numPart = new string(address.Skip(2).TakeWhile(c => char.IsDigit(c)).ToArray());
                    }
                    else if (address.StartsWith("HM", StringComparison.OrdinalIgnoreCase))
                    {
                        offset = 0xC100;
                        numPart = new string(address.Skip(2).TakeWhile(c => char.IsDigit(c)).ToArray());
                    }
                    else if (address.StartsWith("Y", StringComparison.OrdinalIgnoreCase))
                    {
                        offset = 0x6000;
                        numPart = new string(address.Skip(1).TakeWhile(c => char.IsDigit(c)).ToArray());
                    }
                    else if (address.StartsWith("X", StringComparison.OrdinalIgnoreCase))
                    {
                        offset = 0x5000;
                        numPart = new string(address.Skip(1).TakeWhile(c => char.IsDigit(c)).ToArray());
                    }
                    else if (address.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                    {
                        numPart = new string(address.Skip(1).TakeWhile(c => char.IsDigit(c)).ToArray());
                    }
                    else if (address.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                    {
                        numPart = new string(address.Skip(1).TakeWhile(c => char.IsDigit(c)).ToArray());
                    }
                    else
                    {
                        numPart = new string(address.TakeWhile(c => char.IsDigit(c)).ToArray());
                    }

                    ushort addrNum;
                    if (!ushort.TryParse(numPart, out addrNum))
                        throw new ArgumentException("地址格式错误，无法解析起始地址：" + address + "（行，变量" + args.Row.GetField<string>("Tag Name") + "）");

                    return (ushort)(offset + addrNum);
                });

            Map(m => m.Value)
                .Ignore();

            Map(m => m.UpdateTime)
                .Convert(args => DateTime.Now);
        }
    }

    public class MockXdpPlcCommunicator : IXdpPlcCommunicator
    {
        private readonly List<XdpPlcDataModel> _mockData = new List<XdpPlcDataModel>();
        private readonly Random _random = new Random();
        private bool _initialized;

        public bool IsConnected
        {
            get { return false; }
        }

        public string ConnectionMessage
        {
            get { return "PLC 未连接（使用模拟数据）"; }
        }

        public void MappingPlcFromCSV(string csvPath)
        {
            if (_initialized || !File.Exists(csvPath))
                return;

            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ",",
                    MissingFieldFound = args => { }
                };

                using (var reader = new StreamReader(csvPath, System.Text.Encoding.UTF8))
                {
                    using (var csv = new CsvReader(reader, config))
                    {
                        csv.Context.RegisterClassMap<XdpPlcDataModelMap>();

                        var plcDataList = csv.GetRecords<XdpPlcDataModel>().ToList();

                        foreach (var data in plcDataList)
                        {
                            var mockItem = new XdpPlcDataModel
                            {
                                TagName = data.TagName,
                                PlcAddress = data.PlcAddress,
                                AddressType = data.AddressType,
                                StartAddress = data.StartAddress,
                                ValueType = data.ValueType,
                                Description = data.Description,
                                Value = GenerateMockValue(data),
                                UpdateTime = DateTime.Now
                            };
                            _mockData.Add(mockItem);
                        }

                        _initialized = true;
                        Console.WriteLine("✅ Mock XDP PLC 模拟数据已加载：" + _mockData.Count + " 个变量");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Mock XDP PLC 初始化失败：" + ex.Message);
            }
        }

        private object GenerateMockValue(XdpPlcDataModel data)
        {
            switch (data.ValueType)
            {
                case XdpDataValueType.Boolean:
                    return _random.NextDouble() > 0.3;
                case XdpDataValueType.Word:
                    return (ushort)_random.Next(65536);
                case XdpDataValueType.DWord:
                    return _random.Next(int.MinValue, int.MaxValue);
                case XdpDataValueType.Float:
                    return (float)(_random.NextDouble() * 1000);
                default:
                    return 0;
            }
        }

        public void WriteTag(string tagName, object value)
        {
            var item = _mockData.FirstOrDefault(d => string.Equals(d.TagName, tagName, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.Value = value;
                item.UpdateTime = DateTime.Now;
            }
        }

        public void WriteDataToPlc(string tagName, object value)
        {
            WriteTag(tagName, value);
        }

        public List<XdpPlcDataModel> GetPlcStatusList()
        {
            UpdateMockValues();
            return new List<XdpPlcDataModel>(_mockData);
        }

        private void UpdateMockValues()
        {
            foreach (var item in _mockData)
            {
                if (item.ValueType == XdpDataValueType.Float)
                {
                    float current = (float)item.Value;
                    float variation = (float)(_random.NextDouble() - 0.5) * current * 0.02f;
                    item.Value = Math.Max(0f, current + variation);
                }
                else if (item.ValueType == XdpDataValueType.Boolean && _random.NextDouble() > 0.95)
                {
                    item.Value = !(bool)item.Value;
                }

                item.UpdateTime = DateTime.Now;
            }
        }

        public void Disconnect()
        {
            _mockData.Clear();
            _initialized = false;
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            _mockData.Clear();
            _initialized = false;
        }
    }
}