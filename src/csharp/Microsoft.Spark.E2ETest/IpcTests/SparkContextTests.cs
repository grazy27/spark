// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Spark.E2ETest.Utils;
using Microsoft.Spark.Hadoop.Conf;
using Microsoft.Spark.UnitTest.TestUtils;
using Xunit;

namespace Microsoft.Spark.E2ETest.IpcTests
{
    [Collection("Spark E2E Tests")]
    public class SparkContextTests
    {
        /// <summary>
        /// Test signatures for APIs up to Spark 2.4.*.
        /// </summary>
        /// <remarks>
        /// For the RDD related tests, refer to <see cref="RDDTests"/>.
        /// </remarks>
        [Fact]
        public void TestSignaturesV2_4_X()
        {
            SparkContext sc = SparkContext.GetOrCreate(new SparkConf());

            Assert.IsType<SparkConf>(sc.GetConf());
            Assert.IsType<int>(sc.DefaultParallelism);

            sc.SetJobDescription("job description");

            sc.SetLogLevel("ALL");
            sc.SetLogLevel("debug");
            Assert.Throws<Exception>(() => sc.SetLogLevel("INVALID"));

            sc.SetJobGroup("group id", "description");
            sc.SetJobGroup("group id", "description", true);

            sc.ClearJobGroup();

            string filePath = $"{TestEnvironment.ResourceDirectory}people.txt";
            sc.AddFile(filePath);
            sc.AddFile(filePath, true);

            Assert.IsType<string[]>(sc.ListFiles().ToArray());

            using var tempDir = new TemporaryDirectory();
            sc.SetCheckpointDir(TestEnvironment.ResourceDirectory);

            Assert.IsType<string>(sc.GetCheckpointDir());

            Assert.IsType<Configuration>(sc.HadoopConfiguration());
            
            Assert.NotNull(sc.Version());
        }

        /// <summary>
        /// Test signatures for APIs introduced in Spark 3.1.*.
        /// </summary>
        [SkipIfSparkVersionIsLessThan(Versions.V3_1_0)]
        public void TestSignaturesV3_1_X()
        {
            SparkContext sc = SparkContext.GetOrCreate(new SparkConf());

            string archivePath = $"{TestEnvironment.ResourceDirectory}archive.zip";

            try
            {
                sc.AddArchive(archivePath);
            }
            catch (Exception)
            {
                // It tries to delete non-existent file, but other from that its ok
            }

            var archives = sc.ListArchives().ToArray();

            Assert.IsType<string[]>(archives);
            Assert.True(archives.Length >= 1);
            Assert.NotEmpty(archives.Where(a => a.EndsWith("archive.zip")));
        }
    }
}
