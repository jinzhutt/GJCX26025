using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PressureTestApp.Core.Configs
{
    /// <summary>
    /// 提供对配置文件的读写统一接口
    /// </summary>
    public interface IConfig
    {
        /// <summary>
        /// 根据泛型T及关键信息获取配置的内容，返回转换后的类型
        /// </summary>
        T Get<T>(string key) where T : struct;

        /// <summary>
        /// 根据关键信息获取配置的内容，返回转换后的类型
        /// </summary>
        object Get(string key);

        
        /// <summary>
        /// 根据关键信息设置值
        /// </summary>
        void Set(string key, object value);

        /// <summary>
        /// 获取所有的Key
        /// </summary>
        string[] Keys { get; }

        /// <summary>
        /// 获取所有的Value
        /// </summary>
        object[] Values { get; }

        /// <summary>
        /// 移除指定项
        /// </summary>
        void Remove(string key);

        /// <summary>
        /// 清空所以项
        /// </summary>
        void Clear();
        /// <summary>
        /// 手动加载配置，并赋值给属性，通过读取文件/数据库等
        /// </summary>
        void Load();
        /// <summary>
        /// 手动保存配置信息
        /// </summary>
        void Save();
    }

    /// <summary>
    /// 使用集合的方式修改配置
    /// </summary>
    public interface IConfigCollection : IConfig, IEnumerable
    {
        /// <summary>
        /// 是否包含指定的Key
        /// </summary>
        bool Contains(string key);


        /// <summary>
        /// 获取指定的类型
        /// </summary>
        T[] GetValues<T>();

    }
}
