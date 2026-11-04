namespace UFP.NET.Tests
{
    /// <summary>
    /// Tests of the UFP parser.
    /// </summary>
    /// <remarks>
    /// The same set of tests is executed for both implementations:
    /// <see cref="ModernUfpParserTests"/> (UFP.NET, net10.0) and
    /// <see cref="StandardUfpParserTests"/> (UFP.NET.Standard, netstandard2.0).
    /// </remarks>
    public abstract class UfpParserTests
    {
        #region Constants

        private const string MegaPattern = "<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>|<Date>_<Name>.<Extension>|<StudyUid*>.<Type>.<Extension>";

        #endregion

        #region Methods

        /// <summary>
        /// Parses a value with the implementation under test.
        /// </summary>
        /// <param name="value">Value to parse.</param>
        /// <param name="pattern">Pattern to parse the value with.</param>
        /// <returns>A dictionary, where key is variable name and value is the value found.</returns>
        protected abstract Dictionary<string, string> Parse(string? value, string? pattern);

        [Fact]
        public void Parse_UrlPartOfMegaPattern_ReturnsAllVariables()
        {
            AssertParseMatches(
                MegaPattern,
                "https://youtu.be/KoFSQeOAYz4?t=10",
                ("Protocol", "https"),
                ("Domain", "youtu.be"),
                ("Page", "KoFSQeOAYz4"),
                ("FirstParameter.Key", "t"),
                ("FirstParameter.Value", "10"));
        }

        [Fact]
        public void Parse_FileNamePartOfMegaPattern_ReturnsAllVariables()
        {
            AssertParseMatches(
                MegaPattern,
                "04-05-2024_fh5_image.png",
                ("Date", "04-05-2024"),
                ("Name", "fh5_image"),
                ("Extension", "png"));
        }

        [Fact]
        public void Parse_GreedyPartOfMegaPattern_ReturnsAllVariables()
        {
            AssertParseMatches(
                MegaPattern,
                "1.2.840.52394.3.152.235.2.12.187636473.patient.csv",
                ("StudyUid", "1.2.840.52394.3.152.235.2.12.187636473"),
                ("Type", "patient"),
                ("Extension", "csv"));
        }

        [Fact]
        public void Parse_GreedyVariable_CapturesAllIntermediateSeparators()
        {
            AssertParseMatches(
                "<Path*>/<Filename>.<Extension>",
                "docs/reports/2024/annual_report.pdf",
                ("Path", "docs/reports/2024"),
                ("Filename", "annual_report"),
                ("Extension", "pdf"));
        }

        [Fact]
        public void Parse_NonGreedyVariable_StopsOnFirstSeparator()
        {
            AssertParseMatches(
                "<Folder>/<Filename>.<Extension>",
                "docs/reports/annual_report.pdf",
                ("Folder", "docs"),
                ("Filename", "reports/annual_report"),
                ("Extension", "pdf"));
        }

        [Fact]
        public void Parse_SingleVariable_ReturnsWholeValue()
        {
            AssertParseMatches("<Value>", "any text here", ("Value", "any text here"));
        }

        [Fact]
        public void Parse_SinglePattern_ReturnsAllVariables()
        {
            AssertParseMatches(
                "<Date> - <Patient.FirstName> <Patient.LastName>, <Patient.Gender>",
                "22-04-2024 - Alexey Yagelo, male",
                ("Date", "22-04-2024"),
                ("Patient.FirstName", "Alexey"),
                ("Patient.LastName", "Yagelo"),
                ("Patient.Gender", "male"));
        }

        [Fact]
        public void Parse_VariableNamesWithSpaces_AreTrimmed()
        {
            AssertParseMatches("<Date >_< Name>", "04-05-2024_scan", ("Date", "04-05-2024"), ("Name", "scan"));
        }

        [Fact]
        public void Parse_ValueDoesNotMatchPattern_ReturnsEmptyResult()
        {
            AssertParseMatches("<Date>_<Name>", "no separators at all");
        }

        [Fact]
        public void Parse_NullValue_ReturnsEmptyResult()
        {
            Assert.Empty(Parse(null, "<Date>_<Name>"));
        }

        [Fact]
        public void Parse_NullPattern_ReturnsEmptyResult()
        {
            Assert.Empty(Parse("04-05-2024_scan", null));
        }

        [Fact]
        public void Parse_EmptyPattern_ReturnsEmptyResult()
        {
            Assert.Empty(Parse("04-05-2024_scan", string.Empty));
        }

        [Fact]
        public void Parse_SeparatorAtStartOfValue_ReturnsEmptyResult()
        {
            AssertParseMatches("<A>_<B>_<C>", "_x");
        }

        [Fact]
        public void Parse_MoreSeparatorsThanValueCanHold_ReturnsEmptyResult()
        {
            AssertParseMatches("<A>_<B>_<C>_<D>", "_x_y");
        }

        [Fact]
        public void Parse_GreedyVariableAfterLeadingStaticPart_CapturesUpToLastSeparator()
        {
            // The leading static part is verified and consumed, so it does not leak into the first value.
            AssertParseMatches("pre/<A*>_<B>", "pre/a_b_c", ("A", "a_b"), ("B", "c"));
        }

        [Fact]
        public void Parse_LeadingStaticPart_IsConsumed()
        {
            AssertParseMatches("IMG_<Number>.jpg", "IMG_1234.jpg", ("Number", "1234"));
        }

        [Fact]
        public void Parse_ValueDoesNotStartWithLeadingStaticPart_ReturnsEmptyResult()
        {
            AssertParseMatches("IMG_<Number>.jpg", "scan_99.jpg");
        }

        [Fact]
        public void Parse_LeadingStaticPartOfMegaPattern_SelectsTheMatchingAlternative()
        {
            AssertParseMatches("IMG_<Number>.jpg|scan_<Number>.jpg", "scan_99.jpg", ("Number", "99"));
        }

        #endregion

        #region Helpers

        private void AssertParseMatches(string pattern, string value, params (string Key, string Value)[] expected)
        {
            var expectedResult = new Dictionary<string, string>(expected.Length);
            foreach (var (key, expectedValue) in expected)
            {
                expectedResult[key] = expectedValue;
            }

            AssertSameResult(expectedResult, Parse(value, pattern));
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

        #endregion
    }
}
