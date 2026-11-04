using StandardUfpParser = UFP.NET.Standard.UfpParser;

namespace UFP.NET.Tests
{
    /// <summary>
    /// Runs the whole <see cref="UfpParserTests"/> set against the legacy-compatible implementation (UFP.NET.Standard).
    /// </summary>
    public class StandardUfpParserTests : UfpParserTests
    {
        #region Methods

        /// <inheritdoc/>
        protected override Dictionary<string, string> Parse(string? value, string? pattern)
        {
            return StandardUfpParser.Parse(value!, pattern!);
        }

        #endregion
    }
}
