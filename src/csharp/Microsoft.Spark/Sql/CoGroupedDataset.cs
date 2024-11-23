using System;
using System.Collections.Generic;
using System.Text;
using Apache.Arrow;
using Microsoft.Spark.Interop.Ipc;
using Microsoft.Spark.Sql.Expressions;
using Microsoft.Spark.Sql.Types;
using Microsoft.Spark.Utils;

namespace Microsoft.Spark.Sql;

/// <summary>
/// Represent a pair of Co-grouped   <see cref=" Microsoft.Spark.Sql.RelationalGroupedDataset"/>
/// Allows calling UDF on it and passing both datasets as arguments
/// </summary> 
/// 
public sealed class CoGroupedDataset
{
    private RelationalGroupedDataset _first;
    private DataFrame _firstDf;

    private RelationalGroupedDataset _second;

    internal CoGroupedDataset(RelationalGroupedDataset first, DataFrame firstDf, RelationalGroupedDataset second)
    {
        _first = first;
        _firstDf = firstDf;
        _second = second;
    }

    /// <summary>
    /// Maps each group of the current DataFrame using a UDF and
    /// returns the result as a DataFrame.
    /// 
    /// The user-defined function should take two <see cref=" Apache.Arrow.RecordBatch"/>es
    /// and return another Apache Arrow RecordBatch. For each group, all
    /// rows with same key are passed together as 2 RecordBatches to the user-function and
    /// the returned RecordBatch are combined as a DataFrame.
    ///
    /// The returned <see cref="RecordBatch"/> can be of arbitrary length and its
    /// schema must match <paramref name="returnType"/>.
    /// </summary>
    /// <param name="returnType">
    /// The <see cref="StructType"/> that represents the shape of the return data set.
    /// </param>
    /// <param name="func">A Co-Grouped map user-defined function.</param>
    /// <returns>New DataFrame object with the UDF applied.</returns>
    [Since(Versions.V3_0_0)]
    public DataFrame Apply(StructType returnType, Func<RecordBatch, RecordBatch, RecordBatch> func)
    {
        ArrowCoGroupedMapWorkerFunction.ExecuteDelegate wrapper =
                      new ArrowCoGroupedMapUdfWrapper(func).Execute;

        UserDefinedFunction udf = UserDefinedFunction.Create(
           _first.Reference.Jvm,
           func.Method.ToString(),
           CommandSerDe.Serialize(
               wrapper,
               CommandSerDe.SerializedMode.Row,
               CommandSerDe.SerializedMode.Row),
           UdfUtils.PythonEvalType.SQL_COGROUPED_MAP_PANDAS_UDF,
           returnType.Json);

        IReadOnlyList<string> columnNames = _firstDf.Columns();
        var columns = new Column[columnNames.Count];
        for (int i = 0; i < columnNames.Count; ++i)
        {
            columns[i] = _firstDf[columnNames[i]];
        }

        Column udfColumn = udf.Apply(columns);

        var res = new DataFrame((JvmObjectReference)_first.Reference.Invoke(
            "flatMapCoGroupsInPandas",
            _second,
            udfColumn.Expr()));

        return res;
    }
}
