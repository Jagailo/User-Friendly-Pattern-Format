using BenchmarkDotNet.Attributes;
using StandardParser = UFP.NET.Standard.UfpParser;

namespace UFP.NET.Benchmarks
{
    /// <summary>
    /// Benchmarks of the legacy-compatible implementation (UFP.NET.Standard).
    /// </summary>
    [MemoryDiagnoser]
    public class StandardUfpParserBenchmarks
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
                matched += StandardParser.Parse(UfpParserBenchmarksData.SingleValue, UfpParserBenchmarksData.SinglePattern).Count;
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
                matched += StandardParser.Parse(UfpParserBenchmarksData.MegaValue, UfpParserBenchmarksData.MegaPattern).Count;
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
                matched += StandardParser.Parse(UfpParserBenchmarksData.GreedyValue, UfpParserBenchmarksData.GreedyPattern).Count;
            }

            return matched;
        }

        #endregion
    }
}
