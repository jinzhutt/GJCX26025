using System;
using System.Collections.Generic;
using System.Text;

namespace PressureTestApp.Core.Serialize
{
    public class JsonSerialize : ISerialize
    {

        #region ISerialize 成员

        public string SerializeObject(object value)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(value);
        }

        public object DeserializeObject(string value, Type type)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject(value, type);
        }

        public T DeserializeObject<T>(string value)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value);
        }

        #endregion
    }
}
