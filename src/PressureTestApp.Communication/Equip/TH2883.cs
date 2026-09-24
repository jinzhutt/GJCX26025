using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace PressureTestApp.Communication.Equip
{
    public class TH2883
    {
        static object locker = new object();

        public static SerialPort serialPortTH2883;

        //public static SerialPort serialPortTH2883
        //{
        //    get { return serialportTH2883; }
        //    set { serialportTH2883 = value; }
        //}
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="nVolTage">脉冲电压</param>
        /// <param name="nStartTime">开始时间</param>
        /// <param name="nEndTime">截止时间</param>
        /// <param name="nCount">脉冲次数</param>
        public void Init(int nVolTage, int nStartTime, int nEndTime, int nCount)
        {
            try
            {
                if (!serialPortTH2883.IsOpen)
                {
                    serialPortTH2883.Open();
                }
                System.Threading.Thread.Sleep(500);
                if (serialPortTH2883.IsOpen)
                {
                    System.Threading.Thread.Sleep(50);
                    serialPortTH2883.WriteLine("TRIG:SOUR BUS\n");    //设定为总线触发
                    System.Threading.Thread.Sleep(50);
                    serialPortTH2883.WriteLine("COMP:AREA:RANG 0,960\n");     //设定面积比较的区域是(0-960)
                    System.Threading.Thread.Sleep(50);
                    serialPortTH2883.WriteLine("MAES:TIME " + nStartTime + "," + nEndTime + "\n");     //设定时间测量的范围
                    System.Threading.Thread.Sleep(50);
                    serialPortTH2883.WriteLine("IVOLT:TIMP " + nCount + "\n");    //设定测试脉冲次数
                    System.Threading.Thread.Sleep(50);
                    serialPortTH2883.WriteLine("IVOLT:VOLT " + nVolTage + "\n");    //设定测试脉冲电压
                    System.Threading.Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                string err = "初始化有误；" + ex.Message;
                //MessageBox.Show(err);
            }
        }

        /// <summary>
        ///  发送*idn?，有返回则说明通讯正常。 无返回则通讯异常。
        /// </summary>
        /// <returns></returns>
        public static string IsComConnect()
        {
            string str = "";
            try
            {

                if (!serialPortTH2883.IsOpen)
                {
                    serialPortTH2883.Open();
                }
                Thread.Sleep(500);
                if (serialPortTH2883.IsOpen)
                {
                    Thread.Sleep(50);
                    serialPortTH2883.DiscardInBuffer();
                    serialPortTH2883.DiscardOutBuffer();
                    Thread.Sleep(50);
                    serialPortTH2883.WriteLine("*idn?\n"); //查询产品信息，判断是否有返回
                    Thread.Sleep(1000);
                    str = serialPortTH2883.ReadExisting();
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

        /// <summary>
        /// 开始测试
        /// </summary>
        [DisplayName("开始测试")]
        public void TestStart()
        {
            try
            {
                if (!serialPortTH2883.IsOpen)
                {
                    serialPortTH2883.Open();
                }
                System.Threading.Thread.Sleep(500);
                if (serialPortTH2883.IsOpen)
                {
                    System.Threading.Thread.Sleep(50);
                    serialPortTH2883.DiscardInBuffer();
                    serialPortTH2883.DiscardOutBuffer();
                    System.Threading.Thread.Sleep(50);
                    serialPortTH2883.WriteLine("TRIGger:IMMediate\n");    //触发一次测量 

                }
            }
            catch (Exception ex)
            {

            }
        }

        /// <summary>
        /// 设置标准波形
        /// </summary>
        [DisplayName("")]
        public void SetWave()
        {
            if (!serialPortTH2883.IsOpen)
            {
                serialPortTH2883.Open();
            }
            if (serialPortTH2883.IsOpen)
            {
                System.Threading.Thread.Sleep(50);
                serialPortTH2883.WriteLine("SWAVE:TRIG\n");
                System.Threading.Thread.Sleep(1800);
                serialPortTH2883.WriteLine("SWAVE:CHO\n");


            }

        }

        /// <summary>
        /// 获取波形
        /// </summary>
        [DisplayName("获取波形")]
        public string GetWave()
        {
            if (!serialPortTH2883.IsOpen)
            {
                serialPortTH2883.Open();
            }
            if (serialPortTH2883.IsOpen)
            {
                System.Threading.Thread.Sleep(50);
                serialPortTH2883.DiscardInBuffer();
                serialPortTH2883.DiscardOutBuffer();
                System.Threading.Thread.Sleep(50);
                serialPortTH2883.WriteLine("FETC:SWAVE?\n");    //获取波形
                System.Threading.Thread.Sleep(2000);
                string outStr = serialPortTH2883.ReadLine();
                System.Threading.Thread.Sleep(50);
                return outStr;
            }
            else
            {
                return "-1";
            }
        }

        /// <summary>
        /// 获取结果
        /// </summary>
        [DisplayName("获取结果")]
        public string GetResult()
        {
            if (!serialPortTH2883.IsOpen)
            {
                serialPortTH2883.Open();
            }
            if (serialPortTH2883.IsOpen)
            {
                System.Threading.Thread.Sleep(50);
                serialPortTH2883.DiscardInBuffer();
                serialPortTH2883.DiscardOutBuffer();
                System.Threading.Thread.Sleep(50);
                serialPortTH2883.WriteLine("FETCh:CRESult?\n");    //获取结果
                System.Threading.Thread.Sleep(50);
                string outStr = serialPortTH2883.ReadLine();
                System.Threading.Thread.Sleep(50);
                return outStr;
            }
            else
            {
                return "-1";
            }
        }

        /// <summary>
        /// 断开串口
        /// </summary>
        public void Close()
        {
            if (serialPortTH2883.IsOpen)
            {
                serialPortTH2883.Close();
            }
        }

        /// <summary>
        /// 导入波形
        /// </summary>
        /// <param name="BB"></param>
        public void SetWave2(string BB)
        {
            string temp = "SWAVE:LOAD " + BB + "\n";
            serialPortTH2883.WriteLine(temp);
            System.Threading.Thread.Sleep(5000);
        }


    }

    public class ResultAll
    {
        public string GP_U { get; set; }
        public string GP_V { get; set; }
        public string GP_W { get; set; }
        public string GP_Time { get; set; }
        public string ZJ_UV { get; set; }
        public string ZJ_VW { get; set; }
        public string ZJ_UW { get; set; }
        public string ZJ_Time { get; set; }
    }
}
