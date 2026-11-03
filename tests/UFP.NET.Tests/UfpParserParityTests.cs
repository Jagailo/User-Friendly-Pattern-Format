using StandardUfpParser = UFP.NET.Standard.UfpParser;

namespace UFP.NET.Tests
{
    /// <summary>
    /// Verifies that both implementations (UFP.NET and UFP.NET.Standard) return exactly the same result for the same pattern and value.
    /// </summary>
    public class UfpParserParityTests
    {
        #region Methods

        [Theory]
        [InlineData("<Date>_<Name>.<Extension>", "04-05-2024_scan.jpg")]
        [InlineData("<StudyUid*>.<Type>.<Extension>", "1.2.840.52394.3.152.235.2.12.187636473.patient.csv")]
        [InlineData("<Protocol>://<Domain>/<Page>", "https://youtu.be/KoFSQeOAYz4")]
        [InlineData("<Path*>/<Filename>.<Extension>", "docs/reports/2024/annual_report.pdf")]
        public void Parse_SameInput_ReturnsSameResult(string pattern, string value)
        {
            var expected = UfpParser.Parse(value, pattern);
            var actual = StandardUfpParser.Parse(value, pattern);

            Assert.Equal(expected.Count, actual.Count);
            foreach (var (key, expectedValue) in expected)
            {
                Assert.True(actual.TryGetValue(key, out var actualValue), $"Variable '{key}' was not found.");
                Assert.Equal(expectedValue, actualValue);
            }
        }

        #endregion
    }
}
