using System;
using System.Collections.Generic;
using System.Text;

namespace PressureTestApp.Communication.Equip
{
   public class TH9320
    {
        //=============绝缘

        public static System.IO.Ports.SerialPort serialPortTH9320;

        /// <summary>
        /// 绝缘测试参数
        /// </summary>
        public struct JYTEST
        {
            /// <summary>
            ///  数据范围: 50~1000
            /// </summary>
            public static string vol = "1000";
            public static string time = "60";
            /// <summary>
            /// 电阻下限  
            /// </summary>
            public static string IRlower = "100";

        }


        /// <summary>
        /// 耐压测试参数
        /// </summary>
        public struct NYTEST
        {
            public static string vol = "1000";
            public static string time = "60";

            /// <summary>
            /// 漏电流上限
            /// </summary>
            public static string DLhigh = "20";
        }



        /// <summary>
        /// 开始测试
        /// </summary>
        public static void TH9320_TestStart()
        {
            try
            {
                if (!serialPortTH9320.IsOpen)
                {
                    serialPortTH9320.Open();
                }

                /*
                见TH9320v1.0说明书，第58页。《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.0.doc》
                 * 
                 * 绝缘电阻测试，仪表设定电阻下限，例如5MΩ，测试完成后，绝缘电阻下限不小于设定值，认为测试通过。
                 */
                System.Threading.Thread.Sleep(500);
                if (serialPortTH9320.IsOpen)
                {
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.DiscardInBuffer();
                    serialPortTH9320.DiscardOutBuffer();
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.WriteLine("FUNC:STAR");


                }
            }
            catch
            { }
        }

        /// <summary>
        /// 停止测试
        /// </summary>
        public static void TH9320_TestStop()
        {
            try
            {
                if (!serialPortTH9320.IsOpen)
                {
                    serialPortTH9320.Open();
                }

                /*
                见TH9320v1.0说明书，第58页。《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.0.doc》
                 * 
                 * 绝缘电阻测试，仪表设定电阻下限，例如5MΩ，测试完成后，绝缘电阻下限不小于设定值，认为测试通过。
                 */
                System.Threading.Thread.Sleep(500);
                if (serialPortTH9320.IsOpen)
                {
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.DiscardInBuffer();
                    serialPortTH9320.DiscardOutBuffer();
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.WriteLine("FUNC:STOP");


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
        public static void TH9320_JueYuanTest(string dzDC, object dzTime, object dzIRLow)
        {
            //try
            //{
            if (!serialPortTH9320.IsOpen)
            {
                serialPortTH9320.Open();
            }

            /*
            见TH9320v1.0说明书，第58页。《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.0.doc》
             * 数据单位: Ω
               --范例:
                     把STEP 1中IR的电阻下限设置为:1 MΩ
                     命令为:  FUNC:SOUR:STEP 1:IR: LOWR 1000000

             * 绝缘电阻测试，仪表设定电阻下限，例如5MΩ，测试完成后，绝缘电阻下限不小于设定值，认为测试通过。
             * 
             * 《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.2.doc》
             *  * 数据单位: MΩ
               --范例:
                     把STEP 1中IR的电阻下限设置为:1 MΩ
                     命令为:  FUNC:SOUR:STEP 1:IR: LOWC 1
             */

            TestType = 0;

            System.Threading.Thread.Sleep(500);
            if (serialPortTH9320.IsOpen)
            {
                System.Threading.Thread.Sleep(50);
                serialPortTH9320.DiscardInBuffer();
                serialPortTH9320.DiscardOutBuffer();

                System.Threading.Thread.Sleep(50);
                //serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:DC:VOLT " + dzDC);
                serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:IR:VOLT " + dzDC);


                System.Threading.Thread.Sleep(50);
                serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:IR:TTIM " + dzTime);

                System.Threading.Thread.Sleep(50);

                //V1.0版本LOWR，单位为Ω
                //serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:IR:LOWR " + dzIRLow);

                //V1.2版本LOWC，单位为MΩ
                serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:IR:LOWC " + dzIRLow);

                //V1.2版本UPPC，单位为MΩ
                serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:IR:UPPC 0" ); // 0 为关闭上限


                TH9320_TestStart();
            }
            //}
            //catch
            //{ }
        }






        /// <summary>
        /// TH9320绝缘耐压测试，串口指令。NY_Voltage:耐压电压V ,NY_Time 耐压时间：秒。
        /// </summary>
        /// <param name="serialPortTH9320">串口控件</param>
        /// <param name="NY_Voltage">耐压AC电压,单位：V</param>
        /// <param name="NY_Time">耐压AC时间，单位：S 秒</param>
        /// <param name="dlUper">漏电流上限，mA，0-20mA</param> 
        public static void TH9320_NaiYaTest(string NY_Voltage, string NY_Time, string dlUper)
        {
            try
            {
                if (!serialPortTH9320.IsOpen)
                {
                    serialPortTH9320.Open();
                }


                TestType = 1;
                /*
                 *  把STEP 1中ACW的电压(V)设置为:1000V
                         命令为:  FUNC:SOUR:STEP 1:AC:VOLT 1000
                 * 
                 *   把STEP 1中ACW的测试时间(秒)设置为:1s
                         命令为： FUNC:SOUR:STEP 1:AC:TTIM 1

                  最小50V
                 * 数据<电流值>:
数据类型: 浮点数
数据范围: 0~20.0 mA (其中 0 为 OFF) TH9320
数据精度: 0.1 mA
数据单位: mA
--范例:
把 STEP 1 中 ACW 的 ARC 电流上限设置为:1mA
命令为: FUNC:SOUR:STEP 1:AC:ARC 1
                 * 仪表中设置最大漏电流例如20mA。 测试完成后，如果漏电流小于20mA,则认为测试通过。
                 * 
                 */
                System.Threading.Thread.Sleep(500);
                if (serialPortTH9320.IsOpen)
                {
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.DiscardInBuffer();
                    serialPortTH9320.DiscardOutBuffer();
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:AC:VOLT " + NY_Voltage);
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:AC:TTIM " + NY_Time);
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:AC:UPPC " + dlUper);

                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.WriteLine("FUNC:SOUR:STEP 1:AC:LOWC 0");

                    //下位机给DO点控制开始，不用命令
                   TH9320_TestStart();
                }
            }
            catch
            { }
        }


        /// <summary>
        /// 获取绝缘电阻测试的结果：测试的绝缘电阻
        /// </summary>
        /// <param name="serialPortTH9320"></param>
        /// <returns></returns>
        public static string GetJYDZRst(System.IO.Ports.SerialPort serialPortTH9320)
        {
            string rst = "0";
            try
            {
                if (!serialPortTH9320.IsOpen)
                {
                    serialPortTH9320.Open();
                }

                /*
                 --返回信息
                     查询命令: fetch?\r\n
                返回格式：“IR,0.499,140173.094,PASS”

                 * 
                 * 绝缘电阻测试，测试结果值，不小于电阻下限则为通过。返回格式：“IR,0.499,140173.094,PASS”
                 * 
                 */
                System.Threading.Thread.Sleep(500);
                if (serialPortTH9320.IsOpen)
                {
                    System.Threading.Thread.Sleep(50);
                    serialPortTH9320.DiscardInBuffer();
                    serialPortTH9320.DiscardOutBuffer();
                    System.Threading.Thread.Sleep(50);

                    //《TH9310_TH9320交直流耐压绝缘测试仪系列说明书VER1.0.doc》第63页
                    serialPortTH9320.WriteLine("FETCh?\r\n");

                    System.Threading.Thread.Sleep(100);

                    //绝缘电阻返回格式“IR,0.499,140173.094,PASS” 
                    //绝缘耐压返回格式“AC,0.053,0.000,PASS”
                    string outStr = serialPortTH9320.ReadLine();


                    rst = AnalysisDZRst(outStr);
                }
            }
            catch
            { }
            return rst;


        }

       /// <summary>
       ///测试类型， 0 ：绝缘电阻测试。 1：绝缘耐压测试。
       /// </summary>
        public static int TestType { get; set; }

       /// <summary>
        /// 当前测试值，绝缘电阻的电阻值。
       /// </summary>
        public static string  CurRValue { get; set; }

       /// <summary>
        /// 当前测试值，绝缘耐压的漏电流值。
       /// </summary>
        public static string CurIValue { get; set; }



        private static string AnalysisDZRst(string dzPara)
        {
            //绝缘电阻返回格式“IR,0.499,140173.094,PASS” 
            //绝缘耐压返回格式“AC,0.053,0.000,PASS”

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
                    CurRValue = tmp[2];
                }
                else
                {
                    CurIValue = tmp[2];
                }

            }
            catch
            { }
            return rst;
        }




    }
}
