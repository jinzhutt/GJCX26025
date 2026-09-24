using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace PressureTestApp.Communication.Equip
{
    public class TH9110A
    {
        public static bool IsStart { get; set; } //绝缘电阻是否开始试验

        static object locker = new object();

        public static SerialPort serialPortTH9110;

        public static void TH9320_SetPara(int JYVolt1, int NYVolt1, int JueYuan_Qian, int NaiYa, int JueYuan_Hou)
        {
            if (!serialPortTH9110.IsOpen)
            {
                serialPortTH9110.Open();
            }
            Thread.Sleep(500);
            if (serialPortTH9110.IsOpen)
            {
                Thread.Sleep(50);
                serialPortTH9110.DiscardInBuffer();
                serialPortTH9110.DiscardOutBuffer();
                Thread.Sleep(50);
                //耐压前 绝缘电阻电压
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 1:IR:VOLT " + JYVolt1 + "\n");
                Thread.Sleep(50);
                //耐压前 绝缘电阻上限
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 1:IR:UPPR 0" + "\n");
                Thread.Sleep(50);
                //耐压前 绝缘电阻下限
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 1:IR:LOWR " + JueYuan_Qian + "\n");
                Thread.Sleep(50);
                //耐压中 工频耐压电压
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 2:AC:VOLT " + NYVolt1 + "\n");
                Thread.Sleep(50);
                //耐压中 工频耐压上限
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 2:AC:UPPC " + NaiYa + "\n");
                Thread.Sleep(50);
                //耐压中 工频耐压下限
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 2:AC:LOWC 0 \n");
                Thread.Sleep(50);
                //耐压后 绝缘电阻电压
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 3:IR:VOLT " + JYVolt1 + "\n");
                Thread.Sleep(50);
                //耐压后 绝缘电阻上限
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 3:IR:UPPR 0" + "\n");
                Thread.Sleep(50);
                //耐压后 绝缘电阻下限
                serialPortTH9110.WriteLine("FUNC:SOUR:STEP 3:IR:LOWR " + JueYuan_Hou + "\n");

            }
        }

        /// <summary>
        /// TH9320绝缘耐压测试，串口指令。NY_Voltage:耐压电压V ,NY_Time 耐压时间：秒。
        /// </summary>
        /// <param name="serialPortNYY">串口控件</param>
        /// <param name="NY_Voltage">耐压AC电压,单位：V</param>
        /// <param name="NY_Time">耐压AC时间，单位：S 秒</param>
        /// <param name="dlUper">漏电流上限，mA，0-20mA</param> 
        public static void NaiYaTest(string NY_Voltage, string NY_Time, string dlUper)
        {
            try
            {
                if (!serialPortTH9110.IsOpen)
                {
                    serialPortTH9110.Open();
                }

                TestType = 1;
                Thread.Sleep(500);
                if (serialPortTH9110.IsOpen)
                {
                    Thread.Sleep(50);
                    serialPortTH9110.DiscardInBuffer();
                    serialPortTH9110.DiscardOutBuffer();
                    Thread.Sleep(50);
                    serialPortTH9110.WriteLine(":FUNC:SOUR:STEP 1:AC:VOLT " + NY_Voltage + "\n"); //:FUNC:SOUR:STEP 1:AC:VOLT 100
                    Thread.Sleep(50);
                    serialPortTH9110.WriteLine(":FUNC:SOUR:STEP 1:AC:TTIM " + NY_Time + "\n");
                    Thread.Sleep(50);
                    serialPortTH9110.WriteLine(":FUNC:SOUR:STEP 1:AC:UPPC " + dlUper + "\n");

                    Thread.Sleep(50);
                    serialPortTH9110.WriteLine(":FUNC:SOUR:STEP 1:AC:LOWC 0" + "\n");

                    //下位机给DO点控制开始，不用命令
                    TestStart();
                }
            }
            catch
            { }
        }



        /// <summary>
        /// 绝缘电阻测试V1.2
        /// <para>serialPortJYNY,串口控件</para>
        /// <para>dzDC,绝缘电压DC500V，单位：伏特V</para>
        /// <para>dzTime,绝缘电阻测试时间20s，单位：秒s</para>
        /// <para>dzIRLow,绝缘电阻下限，单位：兆欧 MΩ</para>       
        /// </summary>
        public static void JueYuanDZ_IR( string dzDC, object dzTime, object dzIRLow)
        {
            //try
            //{
            if (!serialPortTH9110.IsOpen)
            {
                serialPortTH9110.Open();
            }

            TestType = 0;

            Thread.Sleep(500);
            if (serialPortTH9110.IsOpen)
            {
                Thread.Sleep(50);
                serialPortTH9110.DiscardInBuffer();
                serialPortTH9110.DiscardOutBuffer();

                Thread.Sleep(50);
                //serialPortNYY.WriteLine("FUNC:SOUR:STEP 1:DC:VOLT " + dzDC);
                serialPortTH9110.WriteLine(":FUNC:SOUR:STEP 1:IR:VOLT " + dzDC + "\n");


                Thread.Sleep(50);
                serialPortTH9110.WriteLine(":FUNC:SOUR:STEP 1:IR:TTIM " + dzTime + "\n");

                Thread.Sleep(50);

                //V1.0版本LOWR，单位为Ω
                //serialPortNYY.WriteLine(":FUNC:SOUR:STEP 1:IR:LOWR " + dzIRLow);

                //V1.2版本LOWC，单位为MΩ
                serialPortTH9110.WriteLine(":FUNC:SOUR:STEP 1:IR:LOWC " + dzIRLow + "\n");

                //V1.2版本UPPC，单位为MΩ
                serialPortTH9110.WriteLine(":FUNC:SOUR:STEP 1:IR:UPPC 0" + "\n"); // 0 为关闭上限


                TestStart();
            }
            //}
            //catch
            //{ }
        }



        /// <summary>
        /// 开始测试
        /// </summary>
        [DisplayName("开始测试")]
        public static void TestStart()
        {
            try
            {
                if (!serialPortTH9110.IsOpen)
                {
                    serialPortTH9110.Open();
                }

                /*
                见TH9320v1.0说明书，第58页。《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.0.doc》
                 * 
                 * 绝缘电阻测试，仪表设定电阻下限，例如5MΩ，测试完成后，绝缘电阻下限不小于设定值，认为测试通过。
                 */
                if (serialPortTH9110.IsOpen)
                {
                    Thread.Sleep(50);
                    serialPortTH9110.DiscardInBuffer();
                    serialPortTH9110.DiscardOutBuffer();
                    Thread.Sleep(50);
                    serialPortTH9110.WriteLine("FUNC:START" + "\n");
                   
                }
            }
            catch (Exception ex)
            {
                string err = ex.Message;
                Debug.WriteLine(err);
            }
        }

        /// <summary>
        /// 停止测试
        /// </summary>
        [DisplayName("停止测试")]
        public static void TestStop()
        {
            try
            {
                if (!serialPortTH9110.IsOpen)
                {
                    serialPortTH9110.Open();
                }
                /*
                见TH9320v1.0说明书，第58页。《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.0.doc》
                 * 
                 * 绝缘电阻测试，仪表设定电阻下限，例如5MΩ，测试完成后，绝缘电阻下限不小于设定值，认为测试通过。
                 */
                Thread.Sleep(500);
                if (serialPortTH9110.IsOpen)
                {
                    Thread.Sleep(50);
                    serialPortTH9110.DiscardInBuffer();
                    serialPortTH9110.DiscardOutBuffer();
                    Thread.Sleep(50);
                    serialPortTH9110.WriteLine("*STOP"+ "\n");
                }
                serialPortTH9110.Close();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public void SetTset()
        {
            try
            {
                if (!serialPortTH9110.IsOpen)
                {
                    serialPortTH9110.Open();
                }
                Thread.Sleep(50);
                serialPortTH9110.WriteLine("DISP:PAGE TEST" + "\n");
            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// 获取测试的结果
        /// </summary>
        /// <param name="serialPortTH9320"></param>
        /// <returns></returns>
        [DisplayName("获取结果")]
        public static string[][] GetJYDZRst()
        {
            string[][] dTemp = new string[3][];
            dTemp[0] = new string[3];
            dTemp[1] = new string[3];
            dTemp[2] = new string[3];
            string[] rst1 = new string[3];
            try
            {
                lock (locker)
                {
                    if (!serialPortTH9110.IsOpen)
                    {
                        serialPortTH9110.Open();
                    }

                    /*
                     --返回信息
                         查询命令: fetch?\r\n
                    返回格式：“IR,0.499,140173.094,PASS”

                     * 
                     * 绝缘电阻测试，测试结果值，不小于电阻下限则为通过。返回格式：“IR,0.499,140173.094,PASS”
                     * 
                     */
                    Thread.Sleep(500);
                    if (serialPortTH9110.IsOpen)
                    {
                        Thread.Sleep(50);
                        serialPortTH9110.DiscardInBuffer();
                        serialPortTH9110.DiscardOutBuffer();
                        Thread.Sleep(50);

                        //《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.0.doc》第63页
                        serialPortTH9110.WriteLine("FETCh?\n");

                        Thread.Sleep(100);

                        //绝缘电阻返回格式“IR,0.499,140173.094,PASS” 
                        //绝缘耐压返回格式“AC,0.053,0.000,PASS”
                        string outStr = serialPortTH9110.ReadLine();
                        string[] sTemp = outStr.Split(';');

                        rst1[0] = AnalysisValue(sTemp[0]);
                        rst1[1] = AnalysisValue(sTemp[1]);
                        rst1[2] = AnalysisValue(sTemp[2]);

                        string[] tmp = sTemp[0].Split(',');
                        dTemp[0][0] = tmp[1];
                        dTemp[0][2] = tmp[3];
                        tmp = sTemp[1].Split(',');
                        dTemp[1][0] = tmp[1];
                        dTemp[1][2] = tmp[3];
                        tmp = sTemp[2].Split(',');
                        dTemp[2][0] = tmp[1];
                        dTemp[2][2] = tmp[3];

                        dTemp[0][1] = StrToDou(rst1[0]).ToString();
                        dTemp[1][1] = StrToDou(rst1[1]).ToString();
                        dTemp[2][1] = StrToDou(rst1[2]).ToString();
                    }
                    serialPortTH9110.Close();
                }
            }
            catch (Exception ex)
            {
                return null;
            }
            return dTemp;
        }



        private static string AnalysisValue(string dzPara)
        {
            //绝缘电阻返回格式“IR,0.499,140173.094,PASS” 
            //绝缘耐压返回格式“AC,0.053,0.000,PASS”
            string Value;
            try
            {
                if (dzPara.IndexOf(',') < 0)
                    return "";
                string[] tmp = dzPara.Split(',');
                if (tmp == null || tmp.Length < 4)
                    return "";
                Value = tmp[2];
                string temp1 = tmp[2].Substring(0, 5);
                string temp2 = tmp[2].Substring(7, 2);
                //Value = (Convert.ToSingle(temp1) * Convert.ToSingle(temp2)).ToString();

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return Value;
        }

        public static double StrToDou(string str)
        {
            string sTemp = str.Substring(0, str.IndexOf('e'));
            string sTemp2 = str.Substring(str.IndexOf('e') + 2);
            string sTemp3 = str.Substring(str.IndexOf('e') + 1, 1);
            double B = 0;
            if (sTemp3 == "+")
            {
                if (Convert.ToInt32(sTemp2) > 7)
                {
                    B = 9999999;
                }
                else
                {
                    B = Convert.ToDouble(sTemp) * Math.Pow(10, Convert.ToInt32(sTemp2));
                }
            }
            else
            {
                if (Convert.ToInt32(sTemp2) > 3)
                {
                    B = 0;
                }
                else
                {
                    B = Convert.ToDouble(sTemp) / Math.Pow(10, Convert.ToInt32(sTemp2));
                }

            }

            return B;
        }



        /// <summary>
        /// 获取绝缘电阻测试的结果：测试的绝缘电阻
        /// </summary>
        /// <param name="serialPortNYY"></param>
        /// <returns></returns>
        public static string GetJYDZRstOLD()
        {
            string rst = "0";
            try
            {
                if (!serialPortTH9110.IsOpen)
                {
                    serialPortTH9110.Open();
                }

                /*
                 --返回信息
                     查询命令: fetch?\r\n
                返回格式：“IR,0.499,140173.094,PASS”

                 * 
                 * 绝缘电阻测试，测试结果值，不小于电阻下限则为通过。返回格式：“IR,0.499,140173.094,PASS”
                 * 
                 */
                Thread.Sleep(500);
                if (serialPortTH9110.IsOpen)
                {
                    Thread.Sleep(50);
                    serialPortTH9110.DiscardInBuffer();
                    serialPortTH9110.DiscardOutBuffer();
                    Thread.Sleep(50);

                    //《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.0.doc》第63页
                    serialPortTH9110.WriteLine("FETCh?\n");

                    Thread.Sleep(100);

                    //绝缘电阻返回格式“IR,0.499,140173.094,PASS” 
                    //绝缘耐压返回格式“AC,0.053,0.000,PASS”
                    string outStr = serialPortTH9110.ReadLine();

                    rst = AnalysisDZRst(outStr);
                }
            }
            catch(Exception e) 
            {
                
            }
            return rst;


        }

        /// <summary>
        ///测试类型， 0 ：绝缘电阻测试。 1：绝缘耐压测试。
        /// </summary>
        public static int TestType { get; set; }

        /// <summary>
        /// 当前测试值，绝缘电阻的电阻值。
        /// </summary>
        public static string CurRValue { get; set; }

        /// <summary>
        /// 当前测试值，绝缘耐压的漏电流值。
        /// </summary>
        public static string CurIValue { get; set; }
        private static string AnalysisDZRst(string dzPara)
        {
            //绝缘电阻返回格式“IR,0.499,12.000e+3,PASS” 
            //               STEP 1:AC,1.000,1.000e-3,PASS
            //绝缘耐压返回格式“AC,0.053,0.000,PASS”

            //            例如：STEP 1:AC,1.000,1.000e-3,PASS；STEP 2:DC,1.500,0.100e-3,PASS；
            //测试步骤：1,测试模式：AC，实际测试电压 1kV，实际测试电流 1mA，测试结
            //论:PASS；
            //测试步骤：2,测试模式：DC，实际测试电压 1.5kV，实际测试电流 0.1mA，测
            //试结论:PASS；

            string rst = "FAIL";
            try
            {
                if (dzPara.IndexOf(',') < 0)
                    return rst;
                string[] tmp = dzPara.Split(',');
                if (tmp == null || tmp.Length < 4)
                    return rst;
                rst = tmp[3];

                if (TestType == 0)
                {
                    double IRValue = 0;
                    double.TryParse(tmp[2], out IRValue);
                    CurRValue = (IRValue/1000000).ToString();
                }
                else
                {
                    double LDLValue = 0;
                    double.TryParse(tmp[2], out LDLValue);

                    CurIValue = LDLValue.ToString();
                }

            }
            catch
            { }
            return rst;
        }
    }
}
