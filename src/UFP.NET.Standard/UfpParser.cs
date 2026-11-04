// UFP Format (User Friendly Pattern)
// Format:         UFP-1.2
// Implementation: .NET Standard 2.0 (netstandard2.0)
// Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
// Author:         Alexey Yagelo
// Version:        1.2.0 (2026-11-04)
// License:        MIT (see LICENSE)

using System;
using System.Collections.Generic;

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

            Dictionary<string, string> best = null;
            var alternativeStart = 0;

            while (alternativeStart <= patterns.Length)
            {
                var separatorPosition = patterns.IndexOf(SeparatorChar, alternativeStart);
                var alternativeEnd = separatorPosition < 0 ? patterns.Length : separatorPosition;
                if (alternativeEnd > alternativeStart)
                {
                    var current = ParseBySinglePattern(value, patterns, alternativeStart, alternativeEnd - alternativeStart);

                    // Strictly greater: ties keep the first alternative (UFP-1.2 §6.2), so no sorting is needed.
                    if (best == null || current.Count > best.Count)
                    {
                        best = current;
                    }
                }

                if (separatorPosition < 0)
                {
                    break;
                }

                alternativeStart = separatorPosition + 1;
            }

            return best ?? new Dictionary<string, string>();
        }

        #endregion

        #region Helpers

        private readonly struct PatternPart
        {
            public readonly int Start;
            public readonly int Length;
            public readonly bool IsSeparator;
            public readonly bool IsGreedy;

            public PatternPart(int start, int length, bool isSeparator, bool isGreedy)
            {
                Start = start;
                Length = length;
                IsSeparator = isSeparator;
                IsGreedy = isGreedy;
            }
        }

        /// <summary>
        /// Splits one alternative into parts (UFP-1.2 §4).
        /// </summary>
        /// <param name="pattern">Pattern set that contains the alternative.</param>
        /// <param name="patternStart">Start of the alternative in <paramref name="pattern"/>.</param>
        /// <param name="patternLength">Length of the alternative.</param>
        /// <param name="text">Buffer for the text of the parts, at least <paramref name="patternLength"/> characters long.</param>
        /// <param name="separatorCount">Number of static parts.</param>
        /// <param name="variableCount">Number of variables.</param>
        /// <returns>The parts of the alternative.</returns>
        /// <remarks>
        /// A part keeps a range of <paramref name="text"/> instead of a copied string, so tokenization does not
        /// allocate. Reserved characters that the tokenizer drops (<c>&lt;</c> inside a variable, <c>&gt;</c>
        /// outside a variable) are not copied either, and the retained characters of a part are written next to
        /// each other, so every part is a contiguous range even when characters were dropped.
        /// </remarks>
        private static List<PatternPart> SplitPattern(string pattern, int patternStart, int patternLength, char[] text, out int separatorCount, out int variableCount)
        {
            var patternParts = new List<PatternPart>();

            var inBracket = false;
            var textLength = 0;
            var partStart = 0;
            var partLength = 0;
            separatorCount = 0;
            variableCount = 0;

            for (var i = 0; i < patternLength; i++)
            {
                switch (pattern[patternStart + i])
                {
                    case StartChar:
                    {
                        if (!inBracket)
                        {
                            inBracket = true;
                            if (partLength != 0)
                            {
                                patternParts.Add(new PatternPart(partStart, partLength, true, false));
                                separatorCount++;
                                partStart = textLength;
                                partLength = 0;
                            }
                        }

                        break;
                    }
                    case EndChar:
                    {
                        if (inBracket)
                        {
                            inBracket = false;

                            // The name is trimmed and a trailing '*' marks the variable as greedy (UFP-1.2 §4.1).
                            // Only the range is adjusted, no substring is created.
                            var nameStart = partStart;
                            var nameEnd = partStart + partLength;
                            while (nameStart < nameEnd && char.IsWhiteSpace(text[nameStart]))
                            {
                                nameStart++;
                            }

                            while (nameEnd > nameStart && char.IsWhiteSpace(text[nameEnd - 1]))
                            {
                                nameEnd--;
                            }

                            var isGreedy = nameEnd > nameStart && text[nameEnd - 1] == GreedyChar;
                            if (isGreedy)
                            {
                                nameEnd--;
                            }

                            patternParts.Add(new PatternPart(nameStart, nameEnd - nameStart, false, isGreedy));
                            variableCount++;
                            partStart = textLength;
                            partLength = 0;
                        }

                        break;
                    }
                    default:
                    {
                        if (partLength == 0)
                        {
                            partStart = textLength;
                        }

                        text[textLength] = pattern[patternStart + i];
                        textLength++;
                        partLength++;

                        break;
                    }
                }
            }

            if (partLength != 0)
            {
                patternParts.Add(new PatternPart(partStart, partLength, true, false));
                separatorCount++;
            }

            return patternParts;
        }

        private static Dictionary<string, string> ParseBySinglePattern(string value, string patterns, int patternStart, int patternLength)
        {
            // Retained characters are copied into this buffer, so a part is a contiguous range even when the tokenizer drops characters.
            // The buffer is never longer than the alternative itself.
            var text = new char[patternLength];
            var patternParts = SplitPattern(patterns, patternStart, patternLength, text, out var separatorCount, out var variableCount);

            var separatorTexts = new string[separatorCount];
            var separatorIndexes = new int[separatorCount];
            var separatorIndex = separatorCount - 1;
            var searchEnd = value.Length;

            // Find all separator positions from the right
            for (var i = patternParts.Count - 1; i >= 0; i--)
            {
                var part = patternParts[i];
                if (!part.IsSeparator)
                {
                    continue;
                }

                // The text of a separator is created once and reused by the reverse search and the cutting loop.
                var separator = new string(text, part.Start, part.Length);
                separatorTexts[separatorIndex] = separator;

                var searchStart = searchEnd - 1;
                if (searchStart < 0)
                {
                    // A separator occupies the beginning of the value, so there is no room for the separators to its left.
                    separatorIndexes[separatorIndex] = -1;
                }
                else
                {
                    var separatorPosition = value.LastIndexOf(separator, searchStart, StringComparison.Ordinal);
                    separatorIndexes[separatorIndex] = separatorPosition;
                    if (separatorPosition != -1)
                    {
                        searchEnd = separatorPosition;
                    }
                }

                separatorIndex--;
            }

            Dictionary<string, string> results = null;
            var lastIndex = 0;
            if (patternParts.Count > 0 && patternParts[0].IsSeparator)
            {
                // A pattern that starts with a static part only matches a value that starts with it, and the part is consumed.
                var leadingPart = separatorTexts[0];
                if (!value.StartsWith(leadingPart, StringComparison.Ordinal))
                {
                    return new Dictionary<string, string>();
                }

                lastIndex = leadingPart.Length;
            }

            var separatorsSeen = 0;
            for (var i = 0; i < patternParts.Count; i++)
            {
                var part = patternParts[i];
                if (part.IsSeparator)
                {
                    separatorsSeen++;

                    continue;
                }

                if (i + 1 == patternParts.Count)
                {
                    if (results == null)
                    {
                        results = new Dictionary<string, string>(variableCount);
                    }

                    results[new string(text, part.Start, part.Length)] = value.Substring(lastIndex);

                    continue;
                }

                if (!patternParts[i + 1].IsSeparator)
                {
                    // A variable that is directly followed by another variable is ignored.
                    continue;
                }

                if (separatorIndexes[separatorsSeen] == -1)
                {
                    return results ?? new Dictionary<string, string>();
                }

                var terminator = separatorTexts[separatorsSeen];

                int segmentEnd;
                if (part.IsGreedy)
                {
                    segmentEnd = separatorIndexes[separatorsSeen];
                }
                else
                {
                    segmentEnd = value.IndexOf(terminator, lastIndex, StringComparison.Ordinal);
                    if (segmentEnd == -1)
                    {
                        return results ?? new Dictionary<string, string>();
                    }
                }

                if (segmentEnd < lastIndex)
                {
                    return results ?? new Dictionary<string, string>();
                }

                if (results == null)
                {
                    results = new Dictionary<string, string>(variableCount);
                }

                results[new string(text, part.Start, part.Length)] = value.Substring(lastIndex, segmentEnd - lastIndex);
                lastIndex = segmentEnd + terminator.Length;
            }

            return results ?? new Dictionary<string, string>();
        }

        #endregion
    }
}
