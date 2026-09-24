using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;




namespace PressureTestApp.Core.Configs
{
    /// <summary>
    /// 用于读写ini文件的帮助类
    /// <para>定义：</para>
    /// <para>filename：文件名称</para>
    /// <para>Section：结点名称</para>
    /// <para>key：键名称</para>
    /// <para>Value：值</para>
    /// <para>[section]</para>
    /// <para>key1=value1</para>
    /// <para>key2=value2</para>
    /// <para>key3=value3</para>
    /// <para>key4=value4</para>
    /// </summary>
    public class IniFile
    {
        //函数作用：向INI文件中写信息，若文件不存在则新建一个
        //[DllImport("kernel32")]
        //static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        //[DllImport("kernel32")]
        //static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        //[DllImport("kernel32", EntryPoint = "GetPrivateProfileString")]
        //private static extern uint GetPrivateProfileStringA(string section, string key, string def, Byte[] retVal, int size, string filePath);

        public IniFile() { }

        public IniFile(string filename) { this.Filename = filename; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// 获取所有的Section节点，返回字符串数组
        /// </summary>
        /// <returns></returns>
        public string[] GetSections()
        {
            return GetSections(Filename);
        }

        /// <summary>
        /// 获取所有的节点名称，也就是[]里面的内容，返回一个字符串数组
        /// </summary>
        public string[] GetSections(string filename)
        {
            return GetKeys(filename, null);
        }

        /// <summary>
        /// 获取指定节点下的所有keys
        /// </summary>
        public string[] GetKeys(string section)
        {
            return GetKeys(this.Filename, section);
        }

        /// <summary>
        /// 获取文件名下的指定节点的所有keys
        /// </summary>
        public string[] GetKeys(string filename, string section)
        {
            List<string> result = new List<string>();

            // 初始化 INI 读写器
            var parser = new FileIniDataParser();

            // ======================================
            // 2. 读取 INI 文件
            // ======================================
            IniData readData = parser.ReadFile(filename, Encoding.UTF8);

            // 读取值
            // 遍历所有键值
            foreach (KeyData key in readData[section])
            {
                string keyName = key.KeyName;   // 键名
                string value = key.Value;       // 值
                Console.WriteLine($"{keyName} = {value}");
                result.Add(keyName);
            }

            //Byte[] buf = new Byte[65536];
            //uint len = GetPrivateProfileStringA(section, null, null, buf, buf.Length, filename);
            //int j = 0;
            //for (int i = 0; i < len; i++)
            //    if (buf[i] == 0)
            //    {
            //        result.Add(Encoding.Default.GetString(buf, j, i - j));
            //        j = i + 1;
            //    }
            return result.ToArray();
        }

        public T Get<T>(string section, string key, T def) where T : struct
        {
            //StringBuilder temp = new StringBuilder(102400);
            //GetPrivateProfileString(section, key, def.ToString(), temp, temp.Capacity, Filename);
            //return (T)Convert.ChangeType(temp.ToString(), typeof(T));

            // 初始化 INI 读写器
            var parser = new FileIniDataParser();

            // ======================================
            // 2. 读取 INI 文件
            // ======================================
            IniData readData = parser.ReadFile(Filename, Encoding.UTF8);

            return (T)Convert.ChangeType(readData[section][key].ToString(), typeof(T));
        }

        public string GetString(string section, string key, string def)
        {
            if (!File.Exists(Filename))
            {
                return def;
            }
            
            var parser = new FileIniDataParser();
            IniData readData = parser.ReadFile(Filename, Encoding.UTF8);
            
            if (!readData.Sections.Any(s => s.SectionName == section))
            {
                return def;
            }

            if (!readData[section].ContainsKey(key))
            {
                return def;
            }
            return readData[section][key].ToString();
        }

        public string GetString(string section, string key)
        {
            return GetString(section, key, default(string));
        }

        public string[] GetArray(string section, string key)
        {
            return GetArray(section, key, default(string[]));
        }

        public string[] GetArray(string section, string key, string[] def)
        {
            string source = GetString(section, key, string.Empty);
            return source == null ? def : source.Split(',');
        }

        public int[] GetIntArray(string section, string key)
        {
            return GetIntArray(section, key, null);
        }

        public int[] GetIntArray(string section, string key, int[] def)
        {
            string[] arr = GetArray(section, key, null);
            if (arr == null)
                return def;
            int[] cArr = Array.ConvertAll<string, int>(arr, delegate (string input) { return Convert.ToInt32(input); });
            return cArr;
        }

        public float[] GetFloatArray(string section, string key)
        {
            return GetFloatArray(section, key, null);
        }

        public float[] GetFloatArray(string section, string key, float[] def)
        {
            string[] arr = GetArray(section, key, null);
            if (arr == null)
                return def;
            float[] cArr = Array.ConvertAll<string, float>(arr, delegate (string input) { return Convert.ToSingle(input); });
            return cArr;
        }

        public double[] GetDoubleArray(string section, string key)
        {
            return GetDoubleArray(section, key, null);
        }

        public double[] GetDoubleArray(string section, string key, double[] def)
        {
            string[] arr = GetArray(section, key);
            if (arr == null)
                return def;
            double[] cArr = Array.ConvertAll<string, double>(arr, delegate (string input) { return Convert.ToDouble(input); });
            return cArr;
        }

        public decimal[] GetDecimalArray(string section, string key)
        {
            return GetDecimalArray(section, key, null);
        }

        public decimal[] GetDecimalArray(string section, string key, decimal[] def)
        {
            string[] arr = GetArray(section, key);
            if (arr == null)
                return def;
            decimal[] cArr = Array.ConvertAll<string, decimal>(arr, delegate (string input) { return Convert.ToDecimal(input); });
            return cArr;
        }

        public bool[] GetBoolArray(string section, string key)
        {
            return GetBoolArray(section, key, null);
        }

        public bool[] GetBoolArray(string section, string key, bool[] def)
        {
            string[] arr = GetArray(section, key);
            if (arr == null)
                return def;
            bool[] cArr = Array.ConvertAll<string, bool>(arr, delegate (string input) { return Convert.ToBoolean(input); });
            return cArr;
        }

        public int GetInt(string section, string key)
        {
            return GetInt(section, key, 0);
        }

        public int GetInt(string section, string key, int def)
        {
            int _value;
            if (int.TryParse(GetString(section, key, def.ToString()), out _value))
                return _value;
            else
                return def;
        }

        public decimal GetDecimal(string section, string key)
        {
            return GetDecimal(section, key, 0M);
        }

        public decimal GetDecimal(string section, string key, decimal def)
        {
            decimal _value;
            if (decimal.TryParse(GetString(section, key, def.ToString()), out _value))
                return _value;
            else
                return def;
        }

        public double GetDouble(string section, string key)
        {
            return GetDouble(section, key, 0d);
        }

        public double GetDouble(string section, string key, double def)
        {
            double _value;
            if (double.TryParse(GetString(section, key, def.ToString()), out _value))
                return _value;
            else
                return def;
        }

        public float GetFloat(string section, string key)
        {
            return GetFloat(section, key, 0f);
        }

        public float GetFloat(string section, string key, float def)
        {
            float _value;
            if (float.TryParse(GetString(section, key, def.ToString()), out _value))
                return _value;
            else
                return def;
        }

        public bool GetBool(string section, string key)
        {
            return GetBool(section, key, false);
        }

        public bool GetBool(string section, string key, bool def)
        {
            bool _value;
            if (bool.TryParse(GetString(section, key, def.ToString()), out _value))
                return _value;
            else
                return def;
        }

        public void Set(string section, string key, object value)
        {
            var parser = new FileIniDataParser();
            IniData data;
            
            if (File.Exists(Filename))
            {
                data = parser.ReadFile(Filename, Encoding.UTF8);
            }
            else
            {
                data = new IniData();
            }
            
            data[section][key] = value.ToString();
            parser.WriteFile(Filename, data, Encoding.UTF8);
        }

        public void SetString(string section, string key, string value)
        {
            Set(section, key, value);
        }

        public void SetArray<T>(string section, string key, T[] array)
        {
            string value = "";
            for (int i = 0; i < array.Length; i++)
            {
                value += array[i] + ",";
            }
            if (value.Length > 0) value = value.Substring(0, value.Length - 1);
            Set(section, key, value);
        }

        public void SetArray(string section, string key, string[] array)
        {
            SetArray<string>(section, key, array);
        }

        public void SetInt(string section, string key, int value)
        {
            Set(section, key, value);
        }

        public void SetIntArray(string section, string key, int[] array)
        {
            SetArray<int>(section, key, array);
        }

        public void SetFloat(string section, string key, float value)
        {
            Set(section, key, value);
        }

        public void SetFloatArray(string section, string key, float[] array)
        {
            SetArray<float>(section, key, array);
        }

        public void SetDouble(string section, string key, double value)
        {
            Set(section, key, value);
        }

        public void SetDoubleArray(string section, string key, double[] array)
        {
            SetArray<double>(section, key, array);
        }

        public void SetBool(string section, string key, bool value)
        {
            Set(section, key, value);
        }

        public void SetBoolArray(string section, string key, bool[] array)
        {
            SetArray<bool>(section, key, array);
        }

        public void SetDecimal(string section, string key, decimal value)
        {
            Set(section, key, value);
        }

        public void SetDecimalArray(string section, string key, decimal[] array)
        {
            SetArray<decimal>(section, key, array);
        }

        public void RemoveKey(string section, string key)
        {
            //WritePrivateProfileString(section, key, null, Filename);
            // 1. 创建解析器
            var parser = new FileIniDataParser();
            // 2. 读取INI文件
            IniData iniData = parser.ReadFile(Filename, Encoding.UTF8);

            // 3. 清空该KEY的值（赋值为空字符串）
            if (iniData.Sections.ContainsSection(section) && iniData[section].ContainsKey(key))
            {
                iniData[section][key] = ""; // 清空值
            }
            // 4. 保存回文件
            parser.WriteFile(Filename, iniData);
        }

        public void Clear()
        {
            //WritePrivateProfileString(null, null, null, Filename);
            // 1. 创建解析器
            var parser = new FileIniDataParser();
            // 2. 读取INI文件
            IniData iniData = parser.ReadFile(Filename, Encoding.UTF8);
            // 3. 移除所有 Section
            iniData.Sections.Clear();
            // 4. 保存回文件
            parser.WriteFile(Filename, iniData);

        }

        public void RemoveSection(string section)
        {
            //WritePrivateProfileString(section, null, null, Filename);
            // 1. 创建解析器
            var parser = new FileIniDataParser();
            // 2. 读取INI文件
            IniData iniData = parser.ReadFile(Filename, Encoding.UTF8);

            // 3. 移除一个 Section（例如移除 [User]）
            if (iniData.Sections.ContainsSection(section))
            {
                iniData.Sections.RemoveSection(section);
            }
            // 4. 保存回文件
            parser.WriteFile(Filename, iniData);
        }
    }
}
