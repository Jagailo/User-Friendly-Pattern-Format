using System.Text.Json;

namespace UFP.NET.Tests
{
    /// <summary>
    /// Runs the conformance suite of the specification (spec/ufp-1.2.test-vectors.json) against both implementations.
    /// </summary>
    public class UfpConformanceTests
    {
        #region Constants

        private const string VectorsPath = "spec/ufp-1.2.test-vectors.json";

        #endregion

        #region Methods

        public static TheoryData<string> VectorIds()
        {
            var ids = new TheoryData<string>();
            foreach (var vector in LoadVectors())
            {
                ids.Add(vector.Id);
            }

            return ids;
        }

        [Theory]
        [MemberData(nameof(VectorIds))]
        public void Parse_ConformanceVector_ReturnsExpectedResultInBothImplementations(string id)
        {
            var vector = LoadVectors().Single(x => x.Id == id);

            AssertSameResult(vector.Expected, UfpParser.Parse(vector.Value, vector.Pattern));
            AssertSameResult(vector.Expected, UFP.NET.Standard.UfpParser.Parse(vector.Value, vector.Pattern));
        }

        #endregion

        #region Helpers

        private static List<ConformanceVector> LoadVectors()
        {
            var path = Path.Combine(AppContext.BaseDirectory, VectorsPath);
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            var vectors = new List<ConformanceVector>();
            foreach (var element in document.RootElement.GetProperty("vectors").EnumerateArray())
            {
                var expected = new Dictionary<string, string>();
                foreach (var pair in element.GetProperty("expected").EnumerateObject())
                {
                    expected[pair.Name] = pair.Value.GetString() ?? string.Empty;
                }

                vectors.Add(new ConformanceVector(
                    element.GetProperty("id").GetString() ?? string.Empty,
                    element.GetProperty("pattern").GetString() ?? string.Empty,
                    element.GetProperty("value").GetString() ?? string.Empty,
                    expected));
            }

            return vectors;
        }

        private static void AssertSameResult(Dictionary<string, string> expected, Dictionary<string, string> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            foreach (var (key, expectedValue) in expected)
            {
                Assert.True(actual.TryGetValue(key, out var actualValue), $"Variable '{key}' was not found.");
                Assert.Equal(expectedValue, actualValue);
            }
        }

        private sealed record ConformanceVector(string Id, string Pattern, string Value, Dictionary<string, string> Expected);

        #endregion
    }
}
