﻿using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Microsoft.Spark.Utils
{
    internal static class BinarySerDe
    {
#pragma warning disable SYSLIB0011 // Type or member is deprecated as it's vulnerable to RCE.
        // TODO: Investigate how not to use typeless serialization
        [ThreadStatic]
        private static BinaryFormatter _binaryFormatter;

        private static BinaryFormatter Formatter => _binaryFormatter ??= new();
#pragma warning restore SYSLIB0011 // Type or member is deprecated

        internal static T Deserialize<T>(Stream stream)
        {
            return (T)Formatter.Deserialize(stream);
        }

        internal static void Serialize<T>(Stream stream, T graph)
        {
            Formatter.Serialize(stream, graph);
        }
    }
}
