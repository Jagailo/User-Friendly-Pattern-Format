namespace UFP.NET.Tests
{
    /// <summary>
    /// Runs the whole <see cref="UfpParserTests"/> set against the modern implementation (UFP.NET, net10.0).
    /// </summary>
    public class ModernUfpParserTests : UfpParserTests
    {
        #region Methods

        /// <inheritdoc/>
        protected override Dictionary<string, string> Parse(string? value, string? pattern)
        {
            return UfpParser.Parse(value, pattern);
        }

        #endregion
    }
}
