using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PressureTestApp.Core.Configs
{
    public class TH2883ParaConfig : IniConfig
    {
        public TH2883ParaConfig()
          : base(GetIniPath())
        {

        }
        public TH2883ParaConfig(string sectionName)
            : base(GetIniPath())
        {
            this.SetSectionName(sectionName);
            Load();
        }

        public static string GetIniPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string filename = "\\config\\TH2883_Para.ini";
                if (baseDir.LastIndexOf("\\") != -1)
                {

                    filename = "config\\TH2883_Para.ini";
                }
                else
                {
                    filename = "\\config\\TH2883_Para.ini";
                }
                return baseDir + filename;
            }
            // 统信UOS / Linux
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return baseDir + "/config/TH2883_Para.ini";
            }
            // 默认
            return baseDir + "/config/TH2883_Para.ini";
        }
        /// <summary>
        /// 工频电压
        /// </summary>
        [IniKeyName("工频电压")]
        public double set1 { get; set; }

        /// <summary>
        /// 测试时间
        /// </summary>
        [IniKeyName("测试时间")]
        public double set2 { get; set; }

        /// <summary>
        /// 漏电流上限
        /// </summary>
        [IniKeyName("漏电流上限")]
        public double set3 { get; set; }

        /// <summary>
        /// 匝间电压
        /// </summary>
        [IniKeyName("匝间电压")]
        public double set4 { get; set; }

        /// <summary>
        /// 频率
        /// </summary>
        [IniKeyName("频率")]
        public double set5 { get; set; }

        /// <summary>
        /// U-V标波
        /// </summary>
        [IniKeyName("U-V标波")]
        public string set6 { get; set; }

        /// <summary>
        /// V-W标波
        /// </summary>
        [IniKeyName("V-W标波")]
        public string set7 { get; set; }

        /// <summary>
        /// U-W标波
        /// </summary>
        [IniKeyName("U-W标波")]
        public string set8 { get; set; }

        /// <summary>
        /// 绕组电压
        /// </summary>
        [IniKeyName("绕组电压")]
        public string set10 { get; set; }
        /// <summary>
        /// 绕组时间
        /// </summary>
        [IniKeyName("绕组时间")]
        public string set11 { get; set; }
        /// <summary>
        /// 绕组冷态电阻
        /// </summary>
        [IniKeyName("绕组冷态电阻")]
        public string set12 { get; set; }
        /// <summary>
        /// 绕组热态电阻
        /// </summary>
        [IniKeyName("绕组热态电阻")]
        public string set13 { get; set; }
        /// <summary>
        /// 
        /// </summary>
        [IniKeyName("")]
        public string set14 { get; set; }
        /// <summary>
        /// 
        /// </summary>
        [IniKeyName("")]
        public string set15 { get; set; }
        /// <summary>
        /// 
        /// </summary>
        [IniKeyName("")]
        public string set16 { get; set; }
        /// <summary>
        /// 
        /// </summary>
        [IniKeyName("")]
        public string set17 { get; set; }
        /// <summary>
        /// 
        /// </summary>
        [IniKeyName("")]
        public string set18 { get; set; }
        /// <summary>
        /// 
        /// </summary>
        [IniKeyName("")]
        public string set19 { get; set; }
        /// <summary>
        /// 
        /// </summary>
        [IniKeyName("")]
        public string set20 { get; set; }

        /// <summary>
        /// 语言
        /// </summary>
        [IniKeyName("语言")]
        public string languages { get; set; }
        /// <summary>
        /// 报表名称
        /// </summary>
        [IniKeyName("报表名称")]
        public string RptFile { get; set; }

        private string _SaveReportFormat = "xls";
        /// <summary>
        /// 保存报表格式："xls" or "pdf"
        /// </summary>
        [IniKeyName("保存报表格式")]
        [DefaultValue("xls")]
        public string SaveReportFormat { get { return _SaveReportFormat; } set { _SaveReportFormat = value; } }
    }
}
