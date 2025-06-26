using System;
using System.Collections.Generic;

namespace Mtf.Database
{
    public static class TypeMapping
    {
        public static readonly Dictionary<Type, string> Mappings = new Dictionary<Type, string>
        {
            { typeof(short), "SMALLINT" },
            { typeof(int), "INT" },
            { typeof(long), "BIGINT" },
            { typeof(byte), "TINYINT" },
            { typeof(decimal), "DECIMAL" },
            { typeof(double), "FLOAT" },
            { typeof(float), "REAL" },
            { typeof(bool), "BIT" }
        };
    }
}
