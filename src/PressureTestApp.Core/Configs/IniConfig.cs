using PressureTestApp.Core.Reflect;
using PressureTestApp.Core.Serialize;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using static System.Collections.Generic.Dictionary<string, string>;

namespace PressureTestApp.Core.Configs
{


    /// <summary>
    /// INI 键名标记（保留原项目的模式）
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IniKeyNamesAttribute : Attribute
    {
        public string Section { get; }
        public string Key { get; }
        public string DefaultValue { get; }

        public IniKeyNamesAttribute(string section, string key, string defaultValue = "")
        {
            Section = section;
            Key = key;
            DefaultValue = defaultValue;
        }
    }

    /// <summary>
    /// 旧版一体式耐压机 INI 键名（无 [节] 名，如 AuthenticationConfig.ini）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IniFlatKeyAttribute : Attribute
    {
        public const string RootSection = "";

        public string Key { get; }
        public string DefaultValue { get; }

        public IniFlatKeyAttribute(string key, string defaultValue = "")
        {
            Key = key;
            DefaultValue = defaultValue;
        }
    }

    /// <summary>
    /// 关于读写Ini配置文件的基类
    /// </summary>
    [Description("关于读写Ini配置文件的基类")]
    public class IniConfig : IConfig
    {
        /// <summary>
        /// Ini文件工具
        /// </summary>
        public IniFile Config = new IniFile();

        /// <summary>
        /// 初始化构造
        /// </summary>
        public IniConfig()
        {
            PreInit();
        }

        /// <summary>
        /// 使用指定的文件名初始化构造
        /// </summary>
        public IniConfig(string filename)
        {
            Config.Filename = filename;
            PreInit();
        }

        public IniConfig(string filename, string sectionName)
        {
            Config.Filename = filename;
            PreInit();
            SetSectionName(sectionName);
        }

        /// <summary>
        /// 构造函数时调用此方法，预初始化
        /// </summary>
        protected virtual void PreInit()
        {
        }



        /// <summary>
        /// 文件路径
        /// </summary>
        [IniIgnore]
        public string Filename
        {
            get { return Config.Filename; }
            set { Config.Filename = value; }
        }

        /// <summary>
        /// 获取所有的Key
        /// </summary>
        [IniIgnore]
        public string[] Keys => Config.GetKeys(sectionName);

        /// <summary>
        /// 获取所有的Value
        /// </summary>
        [IniIgnore]
        public object[] Values
        {
            get
            {
                var keys = Keys;
                object[] values = new object[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                    values[i] = Config.GetString(sectionName, keys[i]);
                return values;
            }
        }

        //Dictionary<string, string> dicKeyMap = new Dictionary<string, string>();
        //Dictionary<string, string> dicKeyValue = new Dictionary<string, string>();

        /// <summary>
        /// 保存配置
        /// </summary>
        public virtual void Save()
        {
            Save(Config.Filename);
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        public virtual void Save(string filename)
        {
            Save(GetSectionName(), filename);
        }

        /// <summary>
        /// 将指定对象按Ini标准保存到指定文件中
        /// </summary>
        /// <param name="sectionName">自定义节点名称</param>
        /// <param name="filename">文件名称</param>
        private void Save(string sectionName, string filename)
        {
            if (filename.LastIndexOf("\\") != -1)
            {
                string dir = filename.Substring(0, filename.LastIndexOf('\\'));
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            IniConvert.SerializeObject(this, sectionName, filename);
        }


        /// <summary>
        /// 加载配置文件
        /// </summary>
        public virtual void Load()
        {
            Load(this, this.GetSectionName(), Config.Filename);
        }

        /// <summary>
        /// 将指定对象加载到本对象中
        /// </summary>
        /// <typeparam name="T">对象的类型</typeparam>
        /// <param name="obj">对象的实例</param>
        /// <param name="filename">文件名</param>
        public void Load<T>(T obj, string filename)
        {
            Load(obj, obj.GetType().Name, filename);
        }

        /// <summary>
        /// 将指定对象加载到本对象中
        /// </summary>
        /// <typeparam name="T">对象的类型</typeparam>
        /// <param name="obj">对象的实例</param>
        /// <param name="sectionName">自定义节点名称</param>
        /// <param name="filename">文件名</param>
        public void Load<T>(T obj, string sectionName, string filename)
        {
            IniConvert.Deserialize(obj, sectionName, filename);
            //IniConvert.SerializeObject(obj, sectionName, filename);
        }


        /// <summary>
        /// 加载配置文件
        /// </summary>
        public virtual void Load(string filename)
        {
            Load(this, GetSectionName(), filename);
        }

        /// <summary>
        /// 获取节点名称，
        /// </summary>
        /// <returns></returns>
        public virtual string GetSectionName()
        {
            if (!string.IsNullOrEmpty(sectionName))
            {
                return sectionName;
            }
            return this.GetType().Name;
        }

        private string sectionName;
        /// <summary>
        /// 设置自定义的节点名称
        /// </summary>
        /// <param name="name"></param>
        public void SetSectionName(string name)
        {
            this.sectionName = name;
        }

        /// <summary>
        /// 获取所有的属性
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetProperty()
        {
            return IniConvert.GetPropertyValue(this);
        }

        /// <summary>
        /// 获取sectionName下Key的string类型的Value。[section] key=value
        /// </summary>
        /// <param name="key">指定的节点</param>
        /// <returns>string类型的值</returns>
        public string GetString(string key)
        {
            return Config.GetString(this.GetSectionName(), key);
        }

        public static string GetConfigFilePath()
        {
            // 基础目录（程序运行目录）
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 拼接 config 文件夹 + 文件名
            // Path.Combine 会自动适配 / 和 \
            string configPath = System.IO.Path.Combine(baseDir, "config", "SerialPort.ini");

            return configPath;
        }
        public static string GetIniPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string filename = "\\config\\sys.ini";
                if (baseDir.LastIndexOf("\\") != -1)
                {

                    filename = "config\\sys.ini";
                }
                else
                {
                    filename = "\\config\\sys.ini";
                }
                    return baseDir + filename;
            }
            // 统信UOS / Linux
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return baseDir + "/config/sys.ini";
            }
            // 默认
            return baseDir + "/config/sys.ini";
        }

        #region IConfig 成员

        public T Get<T>(string key) where T : struct
        {
            return Config.Get<T>(this.GetSectionName(), key, default(T));
        }

        public object Get(string key)
        {
            return Config.GetString(this.GetSectionName(), key);
        }

        public void Set(string key, object value)
        {
            //1.写对象；2.写文件；由于sectionName可能被重写，导致section不一致问题；
            //1.写入对象
            IniConvert.SetProperty(this, key, value.ToString());
            //2.写入文件
            Config.Set(this.GetSectionName(), key, value);
        }

        public void Remove(string key)
        {
            Config.RemoveKey(this.GetSectionName(), key);
        }

        public void Clear()
        {
            Config.RemoveSection(this.GetSectionName());
        }

        #endregion
    }
}
