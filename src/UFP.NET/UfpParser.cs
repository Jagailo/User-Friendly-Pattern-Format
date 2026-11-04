// UFP Format (User Friendly Pattern)
// Format:         UFP-1.2
// Implementation: .NET 10 (net10.0)
// Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
// Author:         Alexey Yagelo
// Version:        1.2.0 (2026-11-04)
// License:        MIT (see LICENSE)

using System;
using System.Collections.Generic;

namespace UFP.NET
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

        private const int StackSeparatorLimit = 32;
        private const int StackBufferLimit = 256;

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
                return [ ];
            }

            var patternsSpan = patterns.AsSpan();
            Dictionary<string, string>? best = null;

            foreach (var alternative in patternsSpan.Split(SeparatorChar))
            {
                if (alternative.Start.Value == alternative.End.Value)
                {
                    continue;
                }

                var current = ParseBySinglePattern(value, patternsSpan[alternative]);

                // Strictly greater: ties keep the first alternative (UFP-1.2 §6.2), so no sorting is needed.
                if (best == null || current.Count > best.Count)
                {
                    best = current;
                }
            }

            return best ?? [ ];
        }

        #endregion

        #region Helpers

        private readonly struct PatternPart(int start, int length, bool isSeparator, bool isGreedy)
        {
            public readonly int Start = start;
            public readonly int Length = length;
            public readonly bool IsSeparator = isSeparator;
            public readonly bool IsGreedy = isGreedy;
        }

        /// <summary>
        /// Splits one alternative into parts (UFP-1.2 §4).
        /// </summary>
        /// <param name="pattern">Alternative to tokenize.</param>
        /// <param name="text">Buffer for the text of the parts, at least <c>pattern.Length</c> characters long.</param>
        /// <param name="parts">Buffer for the parts, at least <c>pattern.Length</c> items long.</param>
        /// <param name="separatorCount">Number of static parts.</param>
        /// <param name="variableCount">Number of variables.</param>
        /// <returns>Number of parts written to <paramref name="parts"/>.</returns>
        /// <remarks>
        /// A part keeps a range of <paramref name="text"/> instead of a copied string, so tokenization does not
        /// allocate. Reserved characters that the tokenizer drops (<c>&lt;</c> inside a variable, <c>&gt;</c>
        /// outside a variable) are not copied either, and the retained characters of a part are written next to
        /// each other, so every part is a contiguous range even when characters were dropped.
        /// </remarks>
        private static int Tokenize(ReadOnlySpan<char> pattern, Span<char> text, Span<PatternPart> parts, out int separatorCount, out int variableCount)
        {
            var partCount = 0;
            var textLength = 0;
            var partStart = 0;
            var partLength = 0;
            var inBracket = false;
            separatorCount = 0;
            variableCount = 0;

            for (var i = 0; i < pattern.Length; i++)
            {
                switch (pattern[i])
                {
                    case StartChar:
                    {
                        if (!inBracket)
                        {
                            inBracket = true;
                            if (partLength != 0)
                            {
                                parts[partCount++] = new PatternPart(partStart, partLength, isSeparator: true, isGreedy: false);
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

                            parts[partCount++] = new PatternPart(nameStart, nameEnd - nameStart, isSeparator: false, isGreedy);
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

                        text[textLength++] = pattern[i];
                        partLength++;

                        break;
                    }
                }
            }

            if (partLength != 0)
            {
                parts[partCount++] = new PatternPart(partStart, partLength, isSeparator: true, isGreedy: false);
                separatorCount++;
            }

            return partCount;
        }

        private static Dictionary<string, string> ParseBySinglePattern(string value, ReadOnlySpan<char> pattern)
        {
            var text = pattern.Length <= StackBufferLimit ? stackalloc char[pattern.Length] : new char[pattern.Length];
            var parts = pattern.Length <= StackBufferLimit ? stackalloc PatternPart[pattern.Length] : new PatternPart[pattern.Length];

            var partCount = Tokenize(pattern, text, parts, out var separatorCount, out var variableCount);

            Dictionary<string, string>? results = null;
            var valueSpan = value.AsSpan();

            // Step 1 (UFP-1.2 §5.2): locate the separators from right to left.
            // Positions are indexed by the separator number, so a small array on the stack is enough.
            var separatorIndexes = separatorCount <= StackSeparatorLimit ? stackalloc int[separatorCount] : new int[separatorCount];

            var separatorIndex = separatorCount - 1;
            var searchEnd = value.Length;

            for (var i = partCount - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (!part.IsSeparator)
                {
                    continue;
                }

                // Only the prefix before the separator to the right is searched - RFIND_WITHIN of the specification.
                // No part of the value is copied and the scanned range shrinks with every located separator.
                separatorIndexes[separatorIndex] = LocateSeparator(valueSpan[..searchEnd], text.Slice(part.Start, part.Length));
                if (separatorIndexes[separatorIndex] >= 0)
                {
                    searchEnd = separatorIndexes[separatorIndex];
                }

                separatorIndex--;
            }

            // Step 2 (UFP-1.2 §5.3): cut the value from left to right.
            var lastIndex = 0;
            if (partCount > 0 && parts[0].IsSeparator)
            {
                // A pattern that starts with a static part only matches a value that starts with it, and the part is consumed.
                var leadingPart = text.Slice(parts[0].Start, parts[0].Length);
                if (!valueSpan.StartsWith(leadingPart))
                {
                    return [ ];
                }

                lastIndex = leadingPart.Length;
            }

            var separatorsSeen = 0;
            for (var i = 0; i < partCount; i++)
            {
                var part = parts[i];
                if (part.IsSeparator)
                {
                    separatorsSeen++;

                    continue;
                }

                if (i + 1 == partCount)
                {
                    results ??= new Dictionary<string, string>(variableCount);
                    results[GetVariableName(text, part)] = valueSpan[lastIndex..].ToString();

                    continue;
                }

                var terminatorPart = parts[i + 1];
                if (!terminatorPart.IsSeparator)
                {
                    continue; // A variable that is directly followed by another variable is ignored.
                }

                if (separatorIndexes[separatorsSeen] == -1)
                {
                    return results ?? [ ];
                }

                var terminator = text.Slice(terminatorPart.Start, terminatorPart.Length);

                int segmentEnd;
                if (part.IsGreedy)
                {
                    segmentEnd = separatorIndexes[separatorsSeen];
                }
                else
                {
                    var offset = valueSpan[lastIndex..].IndexOf(terminator, StringComparison.Ordinal);
                    if (offset == -1)
                    {
                        return results ?? [ ];
                    }

                    segmentEnd = lastIndex + offset;
                }

                if (segmentEnd < lastIndex)
                {
                    return results ?? [ ];
                }

                results ??= new Dictionary<string, string>(variableCount);
                results[GetVariableName(text, part)] = valueSpan.Slice(lastIndex, segmentEnd - lastIndex).ToString();
                lastIndex = segmentEnd + terminator.Length;
            }

            return results ?? [ ];
        }

        private static int LocateSeparator(ReadOnlySpan<char> prefix, ReadOnlySpan<char> separator)
        {
            if (prefix.Length == 0)
            {
                return -1; // A separator occupies the beginning of the value, so there is no room for the separators to its left.
            }

            return separator.Length == 1 ? prefix.LastIndexOf(separator[0]) : prefix.LastIndexOf(separator, StringComparison.Ordinal);
        }

        private static string GetVariableName(ReadOnlySpan<char> text, PatternPart part)
        {
            // Tokenize has already trimmed the range and removed the greedy mark,
            // so the name is allocated exactly once - when the variable is captured.
            return text.Slice(part.Start, part.Length).ToString();
        }

        #endregion
    }
}
