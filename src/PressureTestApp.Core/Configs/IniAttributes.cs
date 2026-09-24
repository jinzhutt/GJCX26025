using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PressureTestApp.Core.Configs
{
    /// <summary>
    /// 描述Ini文件使用序列化时，是否自动忽略
    /// </summary>
    public class IniIgnoreAttribute : Attribute
    {
        public bool Ignore { get; set; }

        public IniIgnoreAttribute()
        {
            Ignore = true;
        }

        public IniIgnoreAttribute(bool ignore)
        {
            this.Ignore = ignore;
        }
    }

    /// <summary>
    /// 描述Ini文件使用序列化时，是否自动忽略
    /// </summary>
    public class IniEnabeldAttribute : Attribute
    {
        public IniEnabeldAttribute(bool enabled)
        {
            this.Enabled = enabled;
        }

        private bool enabled;
        public bool Enabled { get { return enabled; } set { enabled = value; } }
    }

    public class IniKeyNameAttribute : Attribute
    {
        public IniKeyNameAttribute(string name)
        {
            this.Name = name;
        }

        private string name;
        public string Name { get { return name; } set { name = value; } }
    }

    public class IniSectionNameAttribute : Attribute
    {
        public IniSectionNameAttribute(string name)
        {
            this.Name = name;
        }

        private string name;
        public string Name { get { return name; } set { name = value; } }
    }
}
