// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Spark.Interop.Ipc;
using Microsoft.Spark.Sql;
using Microsoft.Spark.UnitTest.TestUtils;
using Xunit;

namespace Microsoft.Spark.E2ETest
{
    /// <summary>
    /// SparkFixture acts as a global fixture to start Spark application in a debug
    /// mode through the spark-submit. It also provides a default SparkSession
    /// object that any tests can use.
    /// </summary>
    public sealed class SparkFixture : IDisposable
    {
        /// <summary>
        /// The names of environment variables used by the SparkFixture.
        /// </summary>
        public class EnvironmentVariableNames
        {
            /// <summary>
            /// This environment variable specifies extra args passed to spark-submit.
            /// </summary>
            public const string ExtraSparkSubmitArgs =
                "DOTNET_SPARKFIXTURE_EXTRA_SPARK_SUBMIT_ARGS";

            /// <summary>
            /// This environment variable specifies the path where the DotNet worker is installed.
            /// </summary>
            public const string WorkerDir = Services.ConfigurationService.DefaultWorkerDirEnvVarName;
        }

        private readonly Process _process = new Process();
        private readonly TemporaryDirectory _tempDirectory = new TemporaryDirectory();

        public const string DefaultLogLevel = "ERROR";

        internal SparkSession Spark { get; }

        internal IJvmBridge Jvm { get; }

        public SparkFixture()
        {
            // The worker directory must be set for the Microsoft.Spark.Worker executable.
            if (string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable(EnvironmentVariableNames.WorkerDir)))
            {
                throw new Exception(
                    $"Environment variable '{EnvironmentVariableNames.WorkerDir}' must be set.");
            }

            BuildSparkCmd(out var filename, out var args);

            // Configure the process using the StartInfo properties.
            _process.StartInfo.FileName = filename;
            _process.StartInfo.Arguments = args;
            // UseShellExecute defaults to true in .NET Framework,
            // but defaults to false in .NET Core. To support both, set it
            // to false which is required for stream redirection.
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError = true;

            bool isSparkReady = false;
            _process.OutputDataReceived += (sender, arguments) =>
            {
                // Scala-side driver for .NET emits the following message after it is
                // launched and ready to accept connections.
                if (!isSparkReady && arguments.Data != null &&
                    arguments.Data.Contains("Backend running debug mode"))
                {
                    isSparkReady = true;
                }
            };

            _process.Start();
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();

            bool processExited = false;
            while (!isSparkReady && !processExited)
            {
                processExited = _process.WaitForExit(500);
            }

            if (processExited)
            {
                _process.Dispose();

                // The process should not have been exited.
                throw new Exception(
                    $"Process exited prematurely with '{filename} {args}'.");
            }

            Spark = SparkSession
                .Builder()
                // Lower the shuffle partitions to speed up groupBy() operations.
                .Config("spark.sql.shuffle.partitions", "3")
                .Config("spark.ui.enabled", false)
                .Config("spark.ui.showConsoleProgress", false)
                .AppName("Microsoft.Spark.E2ETest")
                .GetOrCreate();

            Spark.SparkContext.SetLogLevel(DefaultLogLevel);

            Jvm = Spark.Reference.Jvm;
        }

        public string AddPackages(string args)
        {
            string packagesOption = "--packages ";
            string[] splits = args.Split(packagesOption, 2);

            StringBuilder newArgs = new StringBuilder(splits[0])
                .Append(packagesOption)
                .Append(GetAvroPackage());
            if (splits.Length > 1)
            {
                newArgs.Append(",").Append(splits[1]);
            }

            return newArgs.ToString();
        }

        public string GetAvroPackage()
        {
            Version sparkVersion = SparkSettings.Version;
            string avroVersion = sparkVersion.Major switch
            {
                2 => $"spark-avro_2.11:{sparkVersion}",
                3 => $"spark-avro_2.12:{sparkVersion}",
                _ => throw new NotSupportedException($"Spark {sparkVersion} not supported.")
            };

            return $"org.apache.spark:{avroVersion}";
        }

        public void Dispose()
        {
            Spark.Dispose();

            // CSparkRunner will exit upon receiving newline from
            // the standard input stream.
            _process.StandardInput.WriteLine("done");
            _process.StandardInput.Flush();
            _process.WaitForExit();

            _tempDirectory.Dispose();
        }

        private void BuildSparkCmd(out string filename, out string args)
        {
            string sparkHome = SparkSettings.SparkHome;

            // Build the executable name.
            filename = Path.Combine(sparkHome, "bin", "spark-submit");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filename += ".cmd";
            }

            if (!File.Exists(filename))
            {
                throw new FileNotFoundException($"{filename} does not exist.");
            }

            // Build the arguments for the spark-submit.
            string classArg = "--class org.apache.spark.deploy.dotnet.DotnetRunner";
            string curDir = AppDomain.CurrentDomain.BaseDirectory;
            string jarPrefix = GetJarPrefix();
            string scalaDir = Path.Combine(curDir, "..", "..", "..", "..", "..", "src", "scala");
            string jarDir = Path.Combine(scalaDir, jarPrefix, "target");
            string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            string scalaVersion = (SparkSettings.Version.Major == 3) ? "2.12" : "2.11";
            string jar = Path.Combine(jarDir, $"{jarPrefix}_{scalaVersion}-{assemblyVersion}.jar");

            if (!File.Exists(jar))
            {
                throw new FileNotFoundException($"{jar} does not exist.");
            }

            string warehouseUri = new Uri(
                Path.Combine(_tempDirectory.Path, "spark-warehouse")).AbsoluteUri;
            string warehouseDir = $"--conf spark.sql.warehouse.dir={warehouseUri}";

            // Spark24 < 2.4.8, Spark30 < 3.0.3 and Spark31 < 3.1.2 use bintray as the repository
            // service for spark-packages. As of  May 1st, 2021 bintray has been sunset and is no
            // longer available. Specify additional remote repositories to search for the maven
            // coordinates given with --packages.
            string repositories = "--repositories https://repos.spark-packages.org/";

            string extraArgs = Environment.GetEnvironmentVariable(
                EnvironmentVariableNames.ExtraSparkSubmitArgs) ?? "";

            // If there exists log4j.properties in SPARK_HOME/conf directory, Spark from 2.3.*
            // to 2.4.0 hang in E2E test. The reverse behavior is true for Spark 2.4.1; if
            // there does not exist log4j.properties, the tests hang.
            // Note that the hang happens in JVM when it tries to append a console logger (log4j).
            // The solution is to use custom log configuration that appends NullLogger, which
            // works across all Spark versions.
            string resourceUri = new Uri(TestEnvironment.ResourceDirectory).AbsoluteUri;
            var log4jConfiguration = "-Dlog4j.configuration={resourceUri}/log4j.properties";
            var allowRemoteDebugging = ""; // "-agentlib:jdwp=transport=dt_socket,server=y,suspend=n,address=5005"

            string javaDriverOptions = $"--conf \"spark.driver.extraJavaOptions={log4jConfiguration} {allowRemoteDebugging}\"";

            // use "--driver-memory 8g " to set RAM, 1g is default 

            args = $"{javaDriverOptions} {warehouseDir} {AddPackages(extraArgs)} {repositories} {classArg} " +
                @$"--master local ""{jar}"" debug";
        }

        private string GetJarPrefix()
        {
            Version sparkVersion = SparkSettings.Version;
            return $"microsoft-spark-{sparkVersion.Major}-{sparkVersion.Minor}";
        }
    }

    [CollectionDefinition("Spark E2E Tests")]
    public class SparkCollection : ICollectionFixture<SparkFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
