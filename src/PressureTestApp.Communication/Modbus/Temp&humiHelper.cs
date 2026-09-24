//温湿度传感器(北京昆仑海岸)。232（2，3，5脚必接）Modbus协议
//Install-Package NModbus
//Install-Package NModbus.Serial
using NModbus;
using NModbus.Device;
using NModbus.Extensions;
using NModbus.IO;
using NModbus.Logging;
using NModbus.Serial;
using NModbus.Utility;  // 可能包含 TcpStreamResource 的实现
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.IO.Ports;
using System.Net.Sockets;

namespace PressureTestApp.Communication.Modbus
{
    public class TemphumiHelper
    {
        IModbusMaster master;

        public bool IsExit = false;
        public float Temp = 0.0f;
        public float RH = 0.0f;
        SerialPort serialPort;

        Thread WorkThread = null;
        /// <summary>
        /// 连接到Modbus 设备
        /// </summary>
        /// <param name="PortName">设备IP地址，例如 "COM1"</param>
        public bool Init(string portName)
        {
            // ==============================================
            // NModbus.Serial 统信UOS .NET8 主机例程（直接运行）
            // ==============================================

            // 1. 配置串口（UOS 下修改为你的串口名）
            serialPort = new SerialPort
            {
                PortName = portName,//"/dev/ttyUSB0",   // UOS/Linux 格式
                                    // PortName = "COM3",        // Windows 调试用
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = 3000,
                WriteTimeout = 1000,
                ReceivedBytesThreshold = 1 // 接收到1个字节就触发接收事件
            };

            //if (!serialPort.IsOpen) return false;


            try
            {

                // 2. 打开串口
                serialPort.Open();
                Console.WriteLine("串口已打开 → " + serialPort.PortName);

                // 清空缓冲区
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                // 3. 启动读取线程
                IsExit = false;
                WorkThread = new Thread(ReadFromCom);
                WorkThread.Start();

                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("错误：" + ex.Message);
            }
            finally
            {
               
            }
            return true;
        }

        void ReadFromCom()
        {
            // 预定义Modbus RTU指令帧（01 04 00 00 00 02 71 CB）
            byte[] sendBytes = new byte[] { 0x01, 0x04, 0x00, 0x00, 0x00, 0x02, 0x71, 0xCB };

            while (!IsExit)
            {
                try
                {
                    if (!serialPort.IsOpen) break;

                    // 清空缓冲区
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();

                    // 等待串口稳定
                    Thread.Sleep(5);

                    // 发送Modbus指令帧
                    serialPort.Write(sendBytes, 0, sendBytes.Length);
                    Console.WriteLine("发送指令：" + BytesToHexString(sendBytes));

                    // 等待应答（9600波特率，10字节应答需要约10ms，这里留足缓冲）
                    Thread.Sleep(100);

                    // 读取应答数据
                    int receiveLength = serialPort.BytesToRead;
                    if (receiveLength > 0)
                    {
                        byte[] recvBytes = new byte[receiveLength];
                        serialPort.Read(recvBytes, 0, receiveLength);
                        string hexResponse = BytesToHexString(recvBytes);
                        Console.WriteLine("从机应答：" + hexResponse);

                        // 解析温湿度数据
                        ParseResponse(recvBytes);
                    }
                    else
                    {
                        Console.WriteLine("未收到应答");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("读取错误：" + ex.Message);
                }

                // 1秒读取一次
                Thread.Sleep(1000);
            }

            // 关闭串口
            if (serialPort.IsOpen)
            {
                serialPort.Close();
                Console.WriteLine("串口已关闭");
            }
        }

        /// <summary>
        /// 解析Modbus应答数据
        /// 应答格式：01 04 04 00 D6 00 CE 9B E8 → 对应温度21.4℃，湿度20.6%RH
        /// </summary>
        /// <param name="responseBytes">应答字节数组</param>
        void ParseResponse(byte[] responseBytes)
        {
            try
            {
                // 验证应答帧有效性（至少8字节，功能码04，从机地址01）
                if (responseBytes.Length < 8 ||
                    responseBytes[0] != 0x01 ||  // 从机地址
                    responseBytes[1] != 0x04)    // 功能码
                {
                    Console.WriteLine("无效的应答帧格式");
                    return;
                }

                // 提取温度数据（第4、5字节 → 00 D6 = 214 → 214/10=21.4℃）
                ushort tempValue = (ushort)((responseBytes[3] << 8) | responseBytes[4]);
                if (tempValue < 10000)
                    Temp = tempValue / 100.0f;
                
                // 提取湿度数据（第6、7字节 → 00 CE = 206 → 206/10=20.6%RH）
                ushort rhValue = (ushort)((responseBytes[5] << 8) | responseBytes[6]);
                if(rhValue < 1000)
                   RH = rhValue / 10.0f;

                // 输出解析结果
                Console.WriteLine($"解析结果 → 温度：{Temp}℃，湿度：{RH}%RH");
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析数据失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 字节数组转16进制字符串（无空格）
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns>16进制字符串</returns>
        private string BytesToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
        }

        /// <summary>
        /// 停止读取并释放资源
        /// </summary>
        public void Stop()
        {
            IsExit = true;
            WorkThread?.Join(2000); // 等待线程结束
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }
    
}

}
