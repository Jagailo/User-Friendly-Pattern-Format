// UFP Format (User Friendly Pattern)
// Format:         UFP-1.2
// Implementation: .NET Standard 2.0 (netstandard2.0)
// Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
// Author:         Alexey Yagelo
// Version:        1.2.0 (2026-11-04)
// License:        MIT (see LICENSE)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UFP.NET.Standard
{
    /// <summary>
    /// User Friendly Pattern parser.
    /// </summary>
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
        public static Dictionary<string, string> Parse(string value, string patterns)
        {
            if (value == null || patterns == null)
            {
                return new Dictionary<string, string>();
            }

            var patternArray = patterns.Split(new[] { SeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
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
            public readonly string Value;
            public readonly bool IsSeparator;
            public readonly bool IsGreedy;

            public PatternPart(string value, bool isSeparator, bool isGreedy)
            {
                Value = value;
                IsSeparator = isSeparator;
                IsGreedy = isGreedy;
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
                                patternParts.Add(new PatternPart(temp.ToString(), true, false));
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
                            if (name.EndsWith(GreedyChar.ToString()))
                            {
                                name = name.Substring(0, name.Length - 1);
                                isGreedy = true;
                            }

                            patternParts.Add(new PatternPart(name, false, isGreedy));
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
                patternParts.Add(new PatternPart(temp.ToString(), true, false));
            }

            return patternParts;
        }

        private static Dictionary<string, string> ParseBySinglePattern(string value, string pattern)
        {
            var results = new Dictionary<string, string>();

            var patternParts = SplitPattern(pattern);

            var separatorParts = patternParts.Select((part, index) => new { part, index }).Where(x => x.part.IsSeparator).ToArray();

            var separatorIndexes = new int[separatorParts.Length];
            var searchEnd = value.Length;

            // Find all separator positions from the right
            for (var i = separatorParts.Length - 1; i >= 0; i--)
            {
                var searchStart = searchEnd - 1;
                if (searchStart < 0)
                {
                    // A separator occupies the beginning of the value, so there is no room for the separators to its left.
                    separatorIndexes[i] = -1;
                    continue;
                }

                var separatorPosition = value.LastIndexOf(separatorParts[i].part.Value, searchStart, StringComparison.Ordinal);
                separatorIndexes[i] = separatorPosition;
                if (separatorPosition != -1)
                {
                    searchEnd = separatorPosition;
                }
            }

            var lastIndex = 0;
            if (patternParts.Count > 0 && patternParts[0].IsSeparator)
            {
                // A pattern that starts with a static part only matches a value that starts with it, and the part is consumed.
                var leadingPart = patternParts[0].Value;
                if (!value.StartsWith(leadingPart, StringComparison.Ordinal))
                {
                    return results;
                }

                lastIndex = leadingPart.Length;
            }

            var separatorsSeen = 0;
            for (var i = 0; i < patternParts.Count; i++)
            {
                if (patternParts[i].IsSeparator)
                {
                    separatorsSeen++;
                    continue;
                }

                if (i + 1 == patternParts.Count)
                {
                    results[patternParts[i].Value] = value.Substring(lastIndex);
                    continue;
                }

                if (!patternParts[i + 1].IsSeparator)
                {
                    // A variable that is directly followed by another variable is ignored.
                    continue;
                }

                if (separatorIndexes[separatorsSeen] == -1)
                {
                    return results;
                }

                int segmentEnd;
                if (patternParts[i].IsGreedy)
                {
                    segmentEnd = separatorIndexes[separatorsSeen];
                }
                else
                {
                    segmentEnd = value.IndexOf(patternParts[i + 1].Value, lastIndex, StringComparison.Ordinal);
                    if (segmentEnd == -1)
                    {
                        return results;
                    }
                }

                if (segmentEnd < lastIndex)
                {
                    return results;
                }

                results[patternParts[i].Value] = value.Substring(lastIndex, segmentEnd - lastIndex);
                lastIndex = segmentEnd + patternParts[i + 1].Value.Length;
            }

            return results;
        }

        #endregion
    }
}
