using System;
using System.Collections.Generic;
using System.Text;

namespace PressureTestApp.Core.Serialize
{
    public static class SerializeHelper
    {
        public static ISerialize Instance = new JsonSerialize();
    }
}
