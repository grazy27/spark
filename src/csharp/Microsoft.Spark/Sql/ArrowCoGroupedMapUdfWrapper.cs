// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Apache.Arrow;

namespace Microsoft.Spark.Sql
{
    /// <summary>
    /// Wraps the given Func object, which represents a Co-grouped Map UDF.
    /// </summary>
    /// <remarks>
    /// UDF serialization requires a "wrapper" object in order to serialize/deserialize.
    /// </remarks>
    [UdfWrapper]
    internal sealed class ArrowCoGroupedMapUdfWrapper
    {
        private readonly Func<RecordBatch, RecordBatch, RecordBatch> _func;

        internal ArrowCoGroupedMapUdfWrapper(Func<RecordBatch, RecordBatch, RecordBatch> func)
        {
            _func = func;
        }

        internal RecordBatch Execute(RecordBatch inputDf1, RecordBatch inputDf2)
        {
            return _func(inputDf1, inputDf2);
        }
    }
}
