using BenchmarkDotNet.Attributes;
using ModernParser = UFP.NET.UfpParser;
using StandardParser = UFP.NET.Standard.UfpParser;

namespace UFP.Benchmarks
{
    /// <summary>
    /// Patterns and values that represent the typical UFP use cases.
    /// </summary>
    internal static class BenchmarkData
    {
        #region Constants

        /// <summary>
        /// Number of parse calls per benchmark iteration.
        /// </summary>
        /// <remarks>
        /// A single parse takes a few microseconds, which is too short for a reliable measurement,
        /// so every benchmark performs a batch of calls.
        /// </remarks>
        public const int OperationsPerInvoke = 100000;

        /// <summary>
        /// Single pattern without greedy variables.
        /// </summary>
        public const string SinglePattern = "<Date>_<Name>.<Extension>";

        /// <summary>
        /// Value that is parsed by <see cref="SinglePattern"/>.
        /// </summary>
        public const string SingleValue = "04-05-2024_fh5_image.png";

        /// <summary>
        /// Mega pattern: several patterns separated by <c>|</c>.
        /// </summary>
        public const string MegaPattern = "<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>|<Date>_<Name>.<Extension>|<StudyUid*>.<Type>.<Extension>";

        /// <summary>
        /// Value that is parsed by <see cref="MegaPattern"/> (matches the greedy part of the pattern).
        /// </summary>
        public const string MegaValue = "1.2.840.52394.3.152.235.2.12.187636473.patient.csv";

        /// <summary>
        /// Pattern with a greedy variable that contains the following separator.
        /// </summary>
        public const string GreedyPattern = "<Path*>/<Filename>.<Extension>";

        /// <summary>
        /// Value that is parsed by <see cref="GreedyPattern"/>.
        /// </summary>
        public const string GreedyValue = "docs/reports/2024/annual_report.pdf";

        #endregion
    }

    /// <summary>
    /// Benchmarks of the modern implementation (UFP.NET, net10.0).
    /// </summary>
    [MemoryDiagnoser]
    public class ModernParserBenchmarks
    {
        #region Methods

        /// <summary>
        /// Parses a value by a single pattern without greedy variables.
        /// </summary>
        [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkData.OperationsPerInvoke)]
        public int Single()
        {
            var matched = 0;
            for (var i = 0; i < BenchmarkData.OperationsPerInvoke; i++)
            {
                matched += ModernParser.Parse(BenchmarkData.SingleValue, BenchmarkData.SinglePattern).Count;
            }

            return matched;
        }

        /// <summary>
        /// Parses a value by a mega pattern: several patterns separated by <c>|</c>.
        /// </summary>
        [Benchmark(OperationsPerInvoke = BenchmarkData.OperationsPerInvoke)]
        public int MegaPattern()
        {
            var matched = 0;
            for (var i = 0; i < BenchmarkData.OperationsPerInvoke; i++)
            {
                matched += ModernParser.Parse(BenchmarkData.MegaValue, BenchmarkData.MegaPattern).Count;
            }

            return matched;
        }

        /// <summary>
        /// Parses a value by a pattern with a greedy variable (<c>&lt;Path*&gt;</c>).
        /// </summary>
        [Benchmark(OperationsPerInvoke = BenchmarkData.OperationsPerInvoke)]
        public int GreedyVariables()
        {
            var matched = 0;
            for (var i = 0; i < BenchmarkData.OperationsPerInvoke; i++)
            {
                matched += ModernParser.Parse(BenchmarkData.GreedyValue, BenchmarkData.GreedyPattern).Count;
            }

            return matched;
        }

        #endregion
    }

    /// <summary>
    /// Benchmarks of the legacy-compatible implementation (UFP.NET.Standard, netstandard2.0).
    /// </summary>
    [MemoryDiagnoser]
    public class StandardParserBenchmarks
    {
        #region Methods

        /// <summary>
        /// Parses a value by a single pattern without greedy variables.
        /// </summary>
        [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkData.OperationsPerInvoke)]
        public int Single()
        {
            var matched = 0;
            for (var i = 0; i < BenchmarkData.OperationsPerInvoke; i++)
            {
                matched += StandardParser.Parse(BenchmarkData.SingleValue, BenchmarkData.SinglePattern).Count;
            }

            return matched;
        }

        /// <summary>
        /// Parses a value by a mega pattern: several patterns separated by <c>|</c>.
        /// </summary>
        [Benchmark(OperationsPerInvoke = BenchmarkData.OperationsPerInvoke)]
        public int MegaPattern()
        {
            var matched = 0;
            for (var i = 0; i < BenchmarkData.OperationsPerInvoke; i++)
            {
                matched += StandardParser.Parse(BenchmarkData.MegaValue, BenchmarkData.MegaPattern).Count;
            }

            return matched;
        }

        /// <summary>
        /// Parses a value by a pattern with a greedy variable (<c>&lt;Path*&gt;</c>).
        /// </summary>
        [Benchmark(OperationsPerInvoke = BenchmarkData.OperationsPerInvoke)]
        public int GreedyVariables()
        {
            var matched = 0;
            for (var i = 0; i < BenchmarkData.OperationsPerInvoke; i++)
            {
                matched += StandardParser.Parse(BenchmarkData.GreedyValue, BenchmarkData.GreedyPattern).Count;
            }

            return matched;
        }

        #endregion
    }
}
