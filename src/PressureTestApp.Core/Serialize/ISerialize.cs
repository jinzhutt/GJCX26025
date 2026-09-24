using System;
using System.Collections.Generic;
using System.Text;

namespace PressureTestApp.Core.Serialize
{
    public interface ISerialize
    {
        string SerializeObject(object value);
        object DeserializeObject(string value, Type type);
        T DeserializeObject<T>(string value);
    }
}
