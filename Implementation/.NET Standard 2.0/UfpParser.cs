using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UFP
{
    /// <summary>
    /// User Friendly Pattern parser.
    /// </summary>
    public static class UfpParser
    {
        #region Constants

        private const char StartChar = '<', EndChar = '>';
        private const char SeparatorChar = '|';

        #endregion

        #region Methods

        /// <summary>
        /// Parses the specified string.
        /// </summary>
        /// <param name="value">Value to parse.</param>
        /// <param name="patterns">Patterns (can be multiple or single).</param>
        /// <returns>A dictionary, where key is variable name and value is the value found.</returns>
        public static Dictionary<string, string> Parse(string value, string patterns)
        {
            if (value == null || patterns == null)
            {
                return new Dictionary<string, string>();
            }

            var patternArray = patterns.Split(SeparatorChar);
            if (patternArray.Length == 0)
            {
                return new Dictionary<string, string>();
            }

            return patternArray.Length == 1
                ? ParseBySinglePattern(value, patternArray[0])
                : patternArray.Select(x => ParseBySinglePattern(value, x)).OrderByDescending(x => x.Count).First();
        }

        #endregion

        #region Helpers

        private class PatternPart
        {
            internal string Value { get; }
            internal bool IsSeparatorPart { get; }

            internal PatternPart(string value, bool isSeparatorPart)
            {
                Value = value;
                IsSeparatorPart = isSeparatorPart;
            }
        }

        private static List<PatternPart> SplitPattern(string pattern)
        {
            var patternParts = new List<PatternPart>();

            var inBracket = false;
            var temp = new StringBuilder(25);
            foreach (var character in pattern)
            {
                switch (character)
                {
                    case StartChar:
                    {
                        if (!inBracket)
                        {
                            inBracket = true;
                            if (temp.Length != 0)
                            {
                                patternParts.Add(new PatternPart(temp.ToString(), isSeparatorPart: true));
                                temp.Clear();
                            }
                        }

                        break;
                    }
                    case EndChar:
                    {
                        if (inBracket)
                        {
                            inBracket = false;
                            patternParts.Add(new PatternPart(temp.ToString(), isSeparatorPart: false));
                            temp.Clear();
                        }

                        break;
                    }
                    default:
                    {
                        temp.Append(character);

                        break;
                    }
                }
            }

            if (temp.Length != 0)
            {
                patternParts.Add(new PatternPart(temp.ToString(), isSeparatorPart: !inBracket));
            }

            return patternParts;
        }

        private static Dictionary<string, string> ParseBySinglePattern(string value, string pattern)
        {
            var results = new Dictionary<string, string>();

            var patternParts = SplitPattern(pattern);

            var lastIndex = 0;
            string lastVariable = null;
            foreach (var patternPart in patternParts)
            {
                if (patternPart.IsSeparatorPart)
                {
                    var index = value.IndexOf(patternPart.Value, lastIndex, StringComparison.Ordinal);
                    if (index == -1)
                    {
                        lastVariable = null;
                        break;
                    }

                    if (lastVariable != null)
                    {
                        results.Add(lastVariable, value.Substring(lastIndex, index - lastIndex));
                        lastVariable = null;
                    }

                    lastIndex = index + patternPart.Value.Length;
                }
                else
                {
                    lastVariable = patternPart.Value;
                }
            }

            if (lastVariable != null)
            {
                results.Add(lastVariable, value.Substring(lastIndex));
            }

            return results;
        }

        #endregion
    }
}
