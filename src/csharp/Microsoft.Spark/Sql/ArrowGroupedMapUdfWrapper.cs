// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Apache.Arrow;

namespace Microsoft.Spark.Sql
{
    /// <summary>
    /// Wraps the given Func object, which represents a Grouped Map UDF.
    /// </summary>
    /// <remarks>
    /// UDF serialization requires a "wrapper" object in order to serialize/deserialize.
    /// </remarks>
    [UdfWrapper]
    internal sealed class ArrowGroupedMapUdfWrapper
    {
        private readonly Func<IEnumerable<RecordBatch>, RecordBatch> _func;

        internal ArrowGroupedMapUdfWrapper(Func<IEnumerable<RecordBatch>, RecordBatch> func)
        {
            _func = func;
        }

        internal RecordBatch Execute(IEnumerable<RecordBatch> input)
        {
            return _func(input);
        }
    }
}
