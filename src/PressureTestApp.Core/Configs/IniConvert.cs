//using Newtonsoft.Json;
using PressureTestApp.Core.Serialize;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PressureTestApp.Core.Configs
{
    public static class IniConvert
    {
        /// <summary>
        /// 获取节点名称
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string GetSectioName(object obj)
        {
            Type t = obj.GetType();
            string cname = null;
            var secs = t.GetCustomAttributes(typeof(IniSectionNameAttribute), true);
            if (secs != null && secs.Length > 0)
                cname = (secs[0] as IniSectionNameAttribute).Name;
            else
                cname = t.Name;
            return cname;
        }

        /// <summary>
        /// 将一个对象序列化保存到Ini文件中
        /// </summary>
        /// <param name="obj">将要序列化的对象</param>
        /// <param name="filename">将要序列化存储的路径</param>
        /// <returns>成功/失败</returns>
        public static bool SerializeObject(object obj, string filename)
        {
            return SerializeObject(obj, GetSectioName(obj), filename);
        }

        //描述了已序列化的对象中，值和value的对应关系
        //static Dictionary<object, Dictionary<string, PropertyInfo>> dicKeyValue = new Dictionary<object, Dictionary<string, PropertyInfo>>();

        /// <summary>
        /// 将一个对象序列化保存到Ini文件中
        /// </summary>
        /// <param name="obj">将要序列化的对象</param>
        /// <param name="sectionName">自定义节点名称</param>
        /// <param name="filename">将要序列化存储的路径</param>
        /// <returns>成功/失败</returns>
        public static bool SerializeObject(object obj, string sectionName, string filename)
        {
            if (filename.LastIndexOf("\\") != -1)
            {
                string dir = filename.Substring(0, filename.LastIndexOf('\\'));
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            IniFile Config = new IniFile(filename);
            string cname = sectionName;

            var objs = GetPropertyNames(obj);

            foreach (var item in objs)
            {
                var p = item.Value;
                string pname = item.Key;

                //object[] ignores = p.GetCustomAttributes(typeof(IniIgnoreAttribute), true);
                //if (ignores != null && ignores.Length > 0)
                //    continue;

                //object[] datas = p.GetCustomAttributes(typeof(IniKeyNameAttribute), false);
                //if (datas.Length > 0)
                //{
                //    IniKeyNameAttribute iname = datas[0] as IniKeyNameAttribute;
                //    pname = iname.Name;
                //}

                object pvalue = p.GetValue(obj, null);

                if (p.PropertyType.IsValueType)
                {
                    if (pvalue != null)
                        Config.Set(cname, pname, pvalue);
                }
                else if (p.PropertyType == typeof(string))
                {
                    if (pvalue != null)
                    {
                        string[] array = pvalue.ToString().Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        Config.SetArray(cname, pname, array);
                    }
                }
                else//其余情况，采用json序列化
                {
                    string text = SerializeHelper.Instance.SerializeObject(pvalue);
                    Config.Set(cname, pname, text);
                }
                //}
                //else
                //{
                //    Config.Set(cname, pname, pvalue);
                //}
            }
            return true;
        }

        /// <summary>
        /// 反序列化，读取Ini文件并返回为指定对象，对象类型必须包含无参构造函数
        /// </summary>
        /// <typeparam name="T">将要反序列化的类型</typeparam>
        /// <param name="filename">持久化的文件名</param>
        /// <returns>泛型T的对象</returns>
        public static T Deserialize<T>(string filename)
        {
            Type t = typeof(T);
            T obj = (T)Activator.CreateInstance(t);
            Deserialize(obj, filename);
            return obj;
        }

        /// <summary>
        /// 反序列化，将Ini文件读取到obj对象中
        /// </summary>
        /// <param name="obj">将要反序列化的对象</param>
        /// <param name="filename">持久化的文件名</param>
        /// <returns>成功/失败</returns>
        public static bool Deserialize(object obj, string filename)
        {
            return Deserialize(obj, GetSectioName(obj), filename);
        }

        /// <summary>
        /// 反序列化，将Ini文件读取到obj对象中
        /// </summary>
        /// <param name="obj">将要反序列化的对象</param>
        /// <param name="sectionName">自定义节点名称</param>
        /// <param name="filename">持久化的文件名</param>
        /// <returns>成功/失败</returns>
        public static bool Deserialize(object obj, string sectionName, string filename)
        {
            Type t = obj.GetType();
            IniFile Config = new IniFile(filename);
            //string cname = GetSectioName(obj);
            string cname = sectionName;
            //object obj = new T();
            //PropertyInfo[] objs = null;
            var ps = GetPropertyNames(obj);

            foreach (var item in ps)
            {
                var p = item.Value;
                string pname = item.Key;

                string value = Config.GetString(cname, pname, "");
                //TODO: 如果是数组，读取方式将不同
                //dicKeyMap[pname] = p.Name;
                //dicKeyValue[pname] = value;

                if (!string.IsNullOrEmpty(value))
                {
                    if (p.PropertyType == typeof(string))
                    {
                        string[] arr = Config.GetArray(cname, pname);
                        string txt = "";
                        foreach (string s in arr)
                        {
                            txt += s + "\r\n";
                        }
                        if (txt.Length > 0) txt = txt.Substring(0, txt.Length - 2);
                        SetProperty(obj, p, txt);
                    }
                    else
                        SetProperty(obj, p, value);
                }
            }

            return true;
        }

        public static Dictionary<string, PropertyInfo> GetPropertyNames(object obj)
        {
            var ps = obj.GetType().GetProperties();
            Dictionary<string, PropertyInfo> dic = new Dictionary<string, PropertyInfo>();

            foreach (PropertyInfo p in ps)
            {
                string pname = p.Name;

                var ignoreAttr = p.GetCustomAttributes(typeof(IniIgnoreAttribute), true);
                if (ignoreAttr.Length > 0)
                    continue;

                object[] datas = p.GetCustomAttributes(typeof(IniKeyNameAttribute), false);
                if (datas.Length > 0)
                {
                    IniKeyNameAttribute iname = datas[0] as IniKeyNameAttribute;
                    pname = iname.Name;
                }
                else
                {
                    object[] datas2 = p.GetCustomAttributes(typeof(IniKeyNamesAttribute), false);
                    if (datas2.Length > 0)
                    {
                        IniKeyNamesAttribute iname = datas2[0] as IniKeyNamesAttribute;
                        pname = iname.Key;
                    }
                }

                dic[pname] = p;


                //TODO: 如果是数组，读取方式将不同
                //dicKeyMap[pname] = p.Name;
                //dicKeyValue[pname] = value;
            }
            return dic;
        }

        /// <summary>
        /// 获取Ini文件下指定对象属性的所有值
        /// </summary>
        /// <param name="obj">指定对象</param>
        /// <returns></returns>
        public static Dictionary<string, string> GetPropertyValue(object obj)
        {
            var names = GetPropertyNames(obj);
            Dictionary<string, string> dicKeyValue = new Dictionary<string, string>();
            foreach (var item in names)
            {
                var p = item.Value.GetValue(obj, null);
                dicKeyValue[item.Key] = Convert.ToString(p);
            }
            return dicKeyValue;
        }

        static Dictionary<Type, PropertyInfo[]> dicTypeProperties = new Dictionary<Type, PropertyInfo[]>();

        /// <summary>
        /// 按照Ini的规则，设置某个对象的属性
        /// </summary>
        /// <param name="obj">需要设置的对象</param>
        /// <param name="pname">需要设置的属性名</param>
        /// <param name="value">需要写入的值</param>
        public static void SetProperty(object obj, string pname, string value)
        {
            if (obj == null) throw new NullReferenceException("序列化对象的对象不能为空！");

            var t = obj.GetType();
            if (!dicTypeProperties.ContainsKey(t))
                dicTypeProperties[t] = t.GetProperties();

            PropertyInfo[] props = dicTypeProperties[t];
            PropertyInfo info = null;
            foreach (var p in props)
            {
                string name = p.Name;

                var ignoreAttr = p.GetCustomAttributes(typeof(IniIgnoreAttribute), true);
                if (ignoreAttr.Length > 0)
                    continue;

                object[] datas = p.GetCustomAttributes(typeof(IniKeyNameAttribute), false);
                if (datas.Length > 0)
                {
                    IniKeyNameAttribute iname = datas[0] as IniKeyNameAttribute;
                    name = iname.Name;
                }
                else
                {
                    object[] datas2 = p.GetCustomAttributes(typeof(IniKeyNamesAttribute), false);
                    if (datas2.Length > 0)
                    {
                        IniKeyNamesAttribute iname = datas2[0] as IniKeyNamesAttribute;
                        name = iname.Key;
                    }
                }
                if (name == pname)
                {
                    info = p;
                    break;
                }
            }

            SetProperty(obj, info, value);
        }


        /// <summary>
        /// 按照Ini的规则，设置某个对象的属性
        /// </summary>
        /// <param name="obj">需要设置的对象</param>
        /// <param name="p">需要设置的属性</param>
        /// <param name="value">需要写入的值（自动转换类型）</param>
        public static void SetProperty(object obj, PropertyInfo p, string value)
        {
            if (obj == null) throw new NullReferenceException("设置的对象不能为空！");
            if (p == null) throw new NullReferenceException("设置的属性对象不能为空！");

            //只读属性，则不进行反序列化
            if (!p.CanWrite) return;
            if (p.PropertyType.IsValueType || (p.PropertyType == typeof(string)))
            {
                if (p.PropertyType.IsEnum)
                    p.SetValue(obj, Enum.Parse(p.PropertyType, value), null);
                else
                    p.SetValue(obj, Convert.ChangeType(value, p.PropertyType, null), null);
            }
            else
            {
                try
                {
                    object obj2 = SerializeHelper.Instance.DeserializeObject(value, p.PropertyType);
                    p.SetValue(obj, obj2, null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("反序列化ini文件的属性[" + p.Name + "=" + value + ",type=" + p.PropertyType + "]时，出现了异常。" + ex.Message.ToString());
                }
            }
        }
    }
}
