using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace PressureTestApp.Core.Reflect
{
    /// <summary>
    /// 反射工具
    /// </summary>
    public static class Reflector
    {
        /// <summary>
        /// 反射一个类到另外一个类的对象中，通常是两个相同的进行复制
        /// </summary>
        /// <param name="sourceObj"></param>
        /// <param name="destObj"></param>
        public static void Reflect(object sourceObj, object destObj)
        {
            if (sourceObj == null || destObj == null)
                return;

            var props = sourceObj.GetType().GetProperties();
            var props2 = destObj.GetType().GetProperties();

            foreach (var item in props)
            {
                object value = item.GetValue(sourceObj, null);
                var item2 = props2.FirstOrDefault(x => x.Name == item.Name);
                if (item2 == null)
                    continue;
                item2.SetValue(destObj, value, null);
            }
        }

        public static void Reflect(object source, Dictionary<string, object> props, bool removeKey = true)
        {
            var sourceProps = source.GetType().GetProperties();

            foreach (var prop in sourceProps)
            {
                string name = prop.Name;
                if (props.ContainsKey(name))
                {
                    object value = null;
                    if (prop.PropertyType.IsEnum)
                        value = Enum.Parse(prop.PropertyType, props[name].ToString());
                    else
                        value = Convert.ChangeType(props[name], prop.PropertyType);
                    prop.SetValue(source, value, null);
                    if (removeKey)
                        props.Remove(name);
                }
            }
        }

        public static void Reflect(object source, Dictionary<string, string> props, bool removeKey = true)
        {
            var sourceProps = source.GetType().GetProperties();

            foreach (var prop in sourceProps)
            {
                string name = prop.Name;
                if (props.ContainsKey(name))
                {
                    object value = null;
                    if (prop.PropertyType.IsEnum)
                        value = Enum.Parse(prop.PropertyType, props[name].ToString());
                    else
                        value = Convert.ChangeType(props[name], prop.PropertyType);
                    prop.SetValue(source, value, null);
                    if (removeKey)
                        props.Remove(name);
                }
            }

        }

    }
}
