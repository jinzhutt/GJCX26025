//绝缘电阻测试仪
using System;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using PressureTestApp.Core.Helpers;

namespace PressureTestApp.Communication.Equip
{
    public class TH2683A
    {
        public static SerialPort serialPort2683AB;

        /// <summary>
        ///  发送*idn?，有返回则说明通讯正常。 无返回则通讯异常。
        /// </summary>
        /// <returns></returns>
        public static string IsComConnect()
        {
            string str = "";
            try
            {

                if (!serialPort2683AB.IsOpen)
                {
                    serialPort2683AB.Open();
                }
                Thread.Sleep(500);
                if (serialPort2683AB.IsOpen)
                {
                    Thread.Sleep(50);
                    serialPort2683AB.DiscardInBuffer();
                    serialPort2683AB.DiscardOutBuffer();
                    Thread.Sleep(50);
                    serialPort2683AB.WriteLine("*idn?\n"); //查询产品信息，判断是否有返回
                    Thread.Sleep(1000);
                    str = serialPort2683AB.ReadExisting();
                    return str;
                }
                else
                    return "";
            }
            catch (Exception ex)
            {
                return "";
            }
        }


        public static void TH2683B_Test(string sDC, string sTime)
        {
            if (!serialPort2683AB.IsOpen)
            {
                serialPort2683AB.Open();
            }
            if (serialPort2683AB.IsOpen)
            {
                Thread.Sleep(50);
                serialPort2683AB.DiscardInBuffer();
                serialPort2683AB.DiscardOutBuffer();
                Thread.Sleep(100);
                serialPort2683AB.WriteLine("FUNCtion:MTIMe " + sTime + "\n"); //持续时间
                Thread.Sleep(100);
                serialPort2683AB.WriteLine("FUNCtion:DTIMe 5\n");       //放电时间
                Thread.Sleep(100);
                serialPort2683AB.WriteLine("FUNCtion:CTIMe 5\n");      //充电时间
                Thread.Sleep(100);
                serialPort2683AB.WriteLine("FUNCtion:OVOL " + sDC + "\n");
                Thread.Sleep(100);
                serialPort2683AB.WriteLine("FUNCtion:MMOD SINGle\n");
                Thread.Sleep(100);
                TH2683B_TestStart();

            }
        }
        public static void TH2683B_TestStart()
        {
            if (!serialPort2683AB.IsOpen)
            {
                serialPort2683AB.Open();
            }
            Thread.Sleep(500);
            if (serialPort2683AB.IsOpen)
            {
                Thread.Sleep(50);
                serialPort2683AB.DiscardInBuffer();
                serialPort2683AB.DiscardOutBuffer();
                Thread.Sleep(50);
                serialPort2683AB.WriteLine("TRIG\n");
            }
        }


        public static string TH2683B_ReadData()
        {
            try
            {
                string str = "";
                //System.Threading.Thread.Sleep(10000);现场测试，需要等待10秒左右，才能读取到结果。
                //System.Threading.Thread.Sleep(10000);现场测试，需要等待10秒左右，才能读取到结果。
                //System.Threading.Thread.Sleep(10000);现场测试，需要等待10秒左右，才能读取到结果。
                //10s等待放到界面等待。
                serialPort2683AB.DiscardInBuffer();
                serialPort2683AB.DiscardOutBuffer();
                Thread.Sleep(1000);
                serialPort2683AB.WriteLine("FETC?\n");
                Thread.Sleep(1000);
                str = serialPort2683AB.ReadExisting();
                serialPort2683AB.WriteLine("FUNCtion: DTIMe 5.0\n");
                string[] err = str.Split(',');
                string sTemp = err[0];
                sTemp = sTemp.Trim();
                if (!string.IsNullOrEmpty(sTemp))
                {
                    float fTemp = Convert.ToSingle(sTemp);
                    fTemp = fTemp / 1000000;
                    return fTemp.ToString();  // 换算后单位 MΩ
                }
                else
                {
                    return sTemp;
                }

            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                //Debug.WriteLine("TH2683A/B通讯有误，" + ex.Message);
                NlogHelper.Info($"TH2683A/B通讯有误，" + ex.Message.ToString());
                return "";
            }
        }


        public static void TestStop()
        {
            try
            {
               
                Thread.Sleep(10000);
                serialPort2683AB.DiscardInBuffer();
                serialPort2683AB.DiscardOutBuffer();
                Thread.Sleep(5000);
                serialPort2683AB.WriteLine("DISCharge\n");
               

            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                //Debug.WriteLine("TH2683A/B通讯有误，" + ex.Message);
                NlogHelper.Info($"TH2683A/B通讯有误，" + ex.Message.ToString());

            }
        }

    }
}
