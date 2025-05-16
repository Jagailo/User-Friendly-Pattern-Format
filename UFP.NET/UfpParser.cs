using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UFP.NET
{
    /// <summary>
    /// User Friendly Pattern parser.
    /// </summary>
    /// <remarks>https://github.com/Jagailo/User-Friendly-Pattern-Format</remarks>
    public static class UfpParser
    {
        #region Constants

        private const char StartChar = '<', EndChar = '>';
        private const char SeparatorChar = '|';
        private const char GreedyChar = '*';

        #endregion

        #region Methods

        /// <summary>
        /// Parses the specified string.
        /// </summary>
        /// <param name="value">Value to parse.</param>
        /// <param name="patterns">Patterns (can be multiple or single).</param>
        /// <returns>A dictionary, where key is variable name and value is the value found.</returns>
        public static Dictionary<string, string> Parse(string? value, string? patterns)
        {
            if (value == null || patterns == null)
            {
                return [];
            }

            var patternArray = patterns.Split(SeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (patternArray.Length == 0)
            {
                return [];
            }

            return patternArray.Length == 1
                ? ParseBySinglePattern(value, patternArray[0])
                : patternArray.Select(x => ParseBySinglePattern(value, x)).OrderByDescending(x => x.Count).First();
        }

        #endregion

        #region Helpers

        private record PatternPart(string Value, bool IsSeparator, bool IsGreedy);

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
                                patternParts.Add(new PatternPart(temp.ToString(), IsSeparator: true, IsGreedy: false));
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
                            var name = temp.ToString().Trim();
                            var isGreedy = false;
                            if (name.EndsWith(GreedyChar))
                            {
                                name = name[..^1];
                                isGreedy = true;
                            }

                            patternParts.Add(new PatternPart(name, IsSeparator: false, isGreedy));
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
                patternParts.Add(new PatternPart(temp.ToString(), IsSeparator: true, IsGreedy: false));
            }

            return patternParts;
        }

        private static Dictionary<string, string> ParseBySinglePattern(string value, string pattern)
        {
            var results = new Dictionary<string, string>();

            var patternParts = SplitPattern(pattern);

            var separatorParts = patternParts.Select((part, index) => (part, index)).Where(x => x.part.IsSeparator)
                .ToArray();

            var separatorIndexes = new int[separatorParts.Length];
            var searchEnd = value.Length;

            // Find all separator positions from the right
            for (var i = separatorParts.Length - 1; i >= 0; i--)
            {
                var separatorPosition = value.LastIndexOf(separatorParts[i].part.Value, searchEnd - 1, StringComparison.Ordinal);
                separatorIndexes[i] = separatorPosition;
                if (separatorPosition != -1)
                {
                    searchEnd = separatorPosition;
                }
            }

            var lastIndex = 0;
            var separatorIdx = 0;
            for (var i = 0; i < patternParts.Length; i++)
            {
                if (!patternParts[i].IsSeparator)
                {
                    if (i + 1 < patternParts.Length)
                    {
                        if (patternParts[i + 1].IsSeparator)
                        {
                            if (separatorIndexes[separatorIdx] == -1)
                            {
                                return results;
                            }

                            int segmentEnd;
                            if (patternParts[i].IsGreedy)
                            {
                                segmentEnd = separatorIndexes[separatorIdx];
                            }
                            else
                            {
                                segmentEnd = value.IndexOf(patternParts[i + 1].Value, lastIndex, StringComparison.Ordinal);
                                if (segmentEnd == -1)
                                {
                                    return results;
                                }
                            }

                            results[patternParts[i].Value] = value.Substring(lastIndex, segmentEnd - lastIndex);
                            lastIndex = segmentEnd + patternParts[i + 1].Value.Length;
                            separatorIdx++;
                        }
                    }
                    else
                    {
                        results[patternParts[i].Value] = value[lastIndex..];
                    }
                }
            }

            return results;
        }

        #endregion
    }
}
