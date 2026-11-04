using BenchmarkDotNet.Attributes;
using ModernParser = UFP.NET.UfpParser;

namespace UFP.NET.Benchmarks
{
    /// <summary>
    /// Benchmarks of the modern implementation (UFP.NET).
    /// </summary>
    [MemoryDiagnoser]
    public class ModernUfpParserBenchmarks
    {
        #region Methods

        /// <summary>
        /// Parses a value by a single pattern without greedy variables.
        /// </summary>
        [Benchmark(Baseline = true, OperationsPerInvoke = UfpParserBenchmarksData.OperationsPerInvoke)]
        public int Single()
        {
            var matched = 0;
            for (var i = 0; i < UfpParserBenchmarksData.OperationsPerInvoke; i++)
            {
                matched += ModernParser.Parse(UfpParserBenchmarksData.SingleValue, UfpParserBenchmarksData.SinglePattern).Count;
            }

            return matched;
        }

        /// <summary>
        /// Parses a value by a mega pattern: several patterns separated by <c>|</c>.
        /// </summary>
        [Benchmark(OperationsPerInvoke = UfpParserBenchmarksData.OperationsPerInvoke)]
        public int MegaPattern()
        {
            var matched = 0;
            for (var i = 0; i < UfpParserBenchmarksData.OperationsPerInvoke; i++)
            {
                matched += ModernParser.Parse(UfpParserBenchmarksData.MegaValue, UfpParserBenchmarksData.MegaPattern).Count;
            }

            return matched;
        }

        /// <summary>
        /// Parses a value by a pattern with a greedy variable (<c>&lt;Path*&gt;</c>).
        /// </summary>
        [Benchmark(OperationsPerInvoke = UfpParserBenchmarksData.OperationsPerInvoke)]
        public int GreedyVariables()
        {
            var matched = 0;
            for (var i = 0; i < UfpParserBenchmarksData.OperationsPerInvoke; i++)
            {
                matched += ModernParser.Parse(UfpParserBenchmarksData.GreedyValue, UfpParserBenchmarksData.GreedyPattern).Count;
            }

            return matched;
        }

        #endregion
    }
}
