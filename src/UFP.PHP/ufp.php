<?php

declare(strict_types=1);

// UFP Format (User Friendly Pattern)
// Format:         UFP-1.2
// Implementation: PHP 8.0+ (no dependencies)
// Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
// Author:         Alexey Yagelo
// Version:        1.2.0 (2026-11-04)
// License:        MIT (see LICENSE)

namespace UFP;

/**
 * UFP-1.2 reference port (PHP 8) - User Friendly Pattern Format.
 *
 * This file is a fourth, deliberately small implementation of the algorithm of
 * docs/SPECIFICATION.md (§4 Tokenization, §5 Matching, §6 Pattern sets). It exists so
 * that the conformance suite can be run without .NET, Python or Node.js and so that
 * porters have a working example in a language whose strings are byte sequences.
 *
 * String indexing follows §10.1: this port works on UTF-8 bytes. Every index produced by
 * the algorithm is a separator boundary, so a cut can never split a multi-byte character
 * and the result is the same as in the ports that index code points or UTF-16 code units.
 * The only requirement of §10.1 is that one unit is used consistently, which is what the
 * byte-oriented PHP string functions do.
 *
 * Usage:
 *     require_once "src/UFP.PHP/ufp.php";
 *
 *     UFP\parse("04-05-2024_scan.jpg", "<Date>_<Name>.<Extension>");
 *     // ["Date" => "04-05-2024", "Name" => "scan", "Extension" => "jpg"]
 *
 * Run the conformance suite:
 *     php src/UFP.PHP/conformance.php
 */

const START_CHAR = '<';
const END_CHAR = '>';
const ALTERNATIVE_CHAR = '|';
const GREEDY_CHAR = '*';

/**
 * Upper bound of the compiled-pattern cache (§10.2). A typical application parses many
 * values with the same pattern, so the plan is compiled once - see compileAlternative.
 */
const COMPILED_CACHE_LIMIT = 256;

/**
 * Upper bound of the compiled pattern-set cache (§6.1).
 */
const ALTERNATIVES_CACHE_LIMIT = 64;

/**
 * Unicode White_Space as a character class for PCRE (§2.2 TRIM). `\p{Z}` covers
 * U+0020, U+00A0, U+1680, U+2000-U+200A, U+2028, U+2029, U+202F, U+205F and U+3000;
 * U+0085 and the ASCII whitespace characters are not in `\p{Z}` and are listed explicitly.
 */
const WHITESPACE_CLASS = '\p{Z}\x{85}\t\n\x0B\f\r';

/**
 * §6 Parse: split the pattern set, match every alternative, keep the best one.
 *
 * The result is an array with one entry per captured variable. A variable name that looks
 * like a canonical integer (for example `<0>`) becomes an integer array key, because PHP
 * casts such keys; the set of pairs is exactly the specified one. The order of the entries
 * is not part of the specification (§7).
 *
 * @param string|null $value Text to parse.
 * @param string|null $patterns Pattern or pattern set (alternatives separated by `|`).
 *
 * @return array<array-key, string> Captured variables; empty when nothing matched.
 */
function parse(?string $value, ?string $patterns): array
{
    if ($value === null || $patterns === null) {
        return [];
    }

    if (strpos($patterns, ALTERNATIVE_CHAR) === false) {
        // The common case: a single alternative, so there is no pattern set to split.
        if ($patterns === '') {
            return [];
        }

        return parseCompiled($value, compileAlternative($patterns));
    }

    $best = [];
    $bestCount = 0;

    foreach (compileAlternatives($patterns) as $compiled) {
        $current = parseCompiled($value, $compiled);
        $currentCount = count($current);

        // Strictly greater: ties keep the first alternative (UFP-1.2 §6.2).
        if ($currentCount > $bestCount) {
            $best = $current;
            $bestCount = $currentCount;
        }
    }

    return $best;
}

/**
 * §5 ParseSingle: match exactly one alternative. A `|` inside the argument is an ordinary
 * static character here - use parse for a pattern set.
 *
 * @param string $value Text to parse.
 * @param string $pattern One alternative.
 *
 * @return array<array-key, string> Captured variables; empty when nothing matched.
 */
function parseSingle(string $value, string $pattern): array
{
    return parseCompiled($value, compileAlternative($pattern));
}

/**
 * §4 Tokenize: one alternative -> a list of `['text', 'isSeparator', 'isGreedy']` entries.
 *
 * Arrays are values in PHP, so the caller gets a copy of the cached parts and cannot
 * corrupt the compiled plan by modifying the result.
 *
 * @param string $pattern One alternative.
 *
 * @return list<array{text: string, isSeparator: bool, isGreedy: bool}>
 */
function tokenize(string $pattern): array
{
    return compileAlternative($pattern)['parts'];
}

/**
 * §2.2 FIND: the first index `p >= start` where `needle` occurs, or -1.
 *
 * @param string $value Text to search.
 * @param string $needle Non-empty literal to find.
 * @param int $start Index to start the search at.
 */
function find(string $value, string $needle, int $start): int
{
    if ($start < 0) {
        $start = 0;
    } elseif ($start > strlen($value)) {
        return -1; // strpos() would reject an offset past the end of the string.
    }

    $position = strpos($value, $needle, $start);

    return $position === false ? -1 : $position;
}

/**
 * §2.2 RFIND_WITHIN: the last occurrence of `needle` that ends at or before `maxEnd`,
 * or -1. The bound is the index of the last *character* of the match, not the index where
 * the match may start.
 *
 * PHP has no end-bounded reverse search, but the end bound can be converted into a start
 * bound: a match ends at or before `maxEnd` exactly when it starts at or before
 * `maxEnd - strlen(needle) + 1`, and a negative offset of strrpos() is such a start bound.
 *
 * @param string $value Text to search.
 * @param string $needle Non-empty literal to find.
 * @param int $maxEnd Largest index of the last character of the match.
 */
function rfindWithin(string $value, string $needle, int $maxEnd): int
{
    if ($maxEnd < 0) {
        return -1;
    }

    $length = strlen($value);
    if ($maxEnd >= $length) {
        $maxEnd = $length - 1;
    }

    $maxStart = $maxEnd - strlen($needle) + 1;
    if ($maxStart < 0) {
        return -1; // The needle is longer than the allowed part of the value.
    }

    $position = strrpos($value, $needle, $maxStart - $length);

    return $position === false ? -1 : $position;
}

/**
 * §6.1 Split a pattern set into compiled alternatives, dropping empty pieces.
 *
 * The result is cached by the whole pattern set, so a typical "mega pattern" is split and
 * tokenized once (§10.2).
 *
 * @param string $patterns Pattern set that contains at least one `|`.
 *
 * @return list<array{parts: list<array{text: string, isSeparator: bool, isGreedy: bool}>,
 *     separators: list<string>, leadingPart: string|null,
 *     operations: list<array{name: string, isGreedy: bool, terminator: string|null,
 *     separatorIndex: int, terminatorLength: int}>}>
 */
function compileAlternatives(string $patterns): array
{
    static $cache = [];

    if (isset($cache[$patterns])) {
        return $cache[$patterns];
    }

    $alternatives = [];
    foreach (explode(ALTERNATIVE_CHAR, $patterns) as $piece) {
        if ($piece !== '') {
            $alternatives[] = compileAlternative($piece);
        }
    }

    remember($cache, ALTERNATIVES_CACHE_LIMIT, $patterns, $alternatives);

    return $alternatives;
}

/**
 * §4-§5 Tokenize an alternative and compile its matching plan.
 *
 * Tokenization and the plan are pure functions of the pattern, and a typical application
 * parses many values with the same pattern, so the result is cached (§10.2). The cache is
 * bounded and its entries are never exposed directly, so a caller cannot corrupt it.
 *
 * A compiled plan has four fields: `parts` is the tokenizer result, `separators` is the
 * static parts in pattern order, `leadingPart` is the static part a value must start with
 * (or null), and `operations` lists the variables to capture as
 * `['name', 'isGreedy', 'terminator', 'separatorIndex', 'terminatorLength']`. A variable
 * whose terminator is null is the final one and captures the rest of the value.
 *
 * @param string $pattern One alternative.
 *
 * @return array{parts: list<array{text: string, isSeparator: bool, isGreedy: bool}>,
 *     separators: list<string>, leadingPart: string|null,
 *     operations: list<array{name: string, isGreedy: bool, terminator: string|null,
 *     separatorIndex: int, terminatorLength: int}>}
 */
function compileAlternative(string $pattern): array
{
    static $cache = [];

    if (isset($cache[$pattern])) {
        return $cache[$pattern];
    }

    $parts = tokenizeParts($pattern);
    $separators = [];
    $operations = [];
    $separatorsSeen = 0;
    $lastIndex = count($parts) - 1;

    foreach ($parts as $index => $part) {
        if ($part['isSeparator']) {
            $separators[] = $part['text'];
            $separatorsSeen++;
            continue;
        }

        if ($index === $lastIndex) {
            $operations[] = [
                'name' => $part['text'],
                'isGreedy' => false,
                'terminator' => null,
                'separatorIndex' => -1,
                'terminatorLength' => 0,
            ];
            continue;
        }

        $nextPart = $parts[$index + 1];
        if (!$nextPart['isSeparator']) {
            // A variable that is directly followed by another variable is ignored.
            continue;
        }

        $operations[] = [
            'name' => $part['text'],
            'isGreedy' => $part['isGreedy'],
            'terminator' => $nextPart['text'],
            'separatorIndex' => $separatorsSeen,
            'terminatorLength' => strlen($nextPart['text']),
        ];
    }

    $compiled = [
        'parts' => $parts,
        'separators' => $separators,
        'leadingPart' => ($parts !== [] && $parts[0]['isSeparator']) ? $parts[0]['text'] : null,
        'operations' => $operations,
    ];

    remember($cache, COMPILED_CACHE_LIMIT, $pattern, $compiled);

    return $compiled;
}

/**
 * §4.1 Tokenize one alternative into parts.
 *
 * A `<` inside a variable and a `>` outside a variable are dropped; an unclosed variable
 * becomes a static part. The name is trimmed and a trailing `*` marks the variable as
 * greedy. The function never fails and never looks at the value.
 *
 * @param string $pattern One alternative.
 *
 * @return list<array{text: string, isSeparator: bool, isGreedy: bool}>
 */
function tokenizeParts(string $pattern): array
{
    $parts = [];
    $inVariable = false;
    $buffer = '';
    $length = strlen($pattern);

    for ($index = 0; $index < $length; $index++) {
        $character = $pattern[$index];

        if ($character === START_CHAR) {
            if (!$inVariable) {
                $inVariable = true;
                if ($buffer !== '') {
                    $parts[] = createSeparator($buffer);
                    $buffer = '';
                }
            }

            continue;
        }

        if ($character === END_CHAR) {
            if ($inVariable) {
                $inVariable = false;
                $name = trimWhitespace($buffer);

                // Only a trailing '*' after the trim marks the variable as greedy (§3.2).
                $isGreedy = $name !== '' && $name[strlen($name) - 1] === GREEDY_CHAR;
                if ($isGreedy) {
                    $name = substr($name, 0, -1);
                }

                $parts[] = ['text' => $name, 'isSeparator' => false, 'isGreedy' => $isGreedy];
                $buffer = '';
            }

            continue;
        }

        $buffer .= $character;
    }

    if ($buffer !== '') {
        $parts[] = createSeparator($buffer);
    }

    return $parts;
}

/**
 * @param string $text Non-empty literal.
 *
 * @return array{text: string, isSeparator: bool, isGreedy: bool}
 */
function createSeparator(string $text): array
{
    return ['text' => $text, 'isSeparator' => true, 'isGreedy' => false];
}

/**
 * §2.2 TRIM: removes Unicode White_Space from both ends of a variable name.
 *
 * PHP's trim() only knows ASCII whitespace, so the Unicode set is matched with PCRE. The
 * `/u` modifier requires valid UTF-8, so a name that is not valid UTF-8 falls back to the
 * ASCII trim(). Only the name is trimmed; captured text is never touched (§3.2).
 *
 * @param string $text Variable content between `<` and `>`.
 */
function trimWhitespace(string $text): string
{
    $trimmed = preg_replace(
        '/\A[' . WHITESPACE_CLASS . ']+|[' . WHITESPACE_CLASS . ']+\z/u',
        '',
        $text);

    return $trimmed ?? trim($text);
}

/**
 * §5.2-§5.3: match a compiled alternative against a value.
 *
 * Every variable extracted before the first failure is kept, so the result of a partial
 * match is always a prefix of the variables of the pattern (§5.4).
 *
 * @param string $value Text to parse.
 * @param array{parts: list<array{text: string, isSeparator: bool, isGreedy: bool}>,
 *     separators: list<string>, leadingPart: string|null,
 *     operations: list<array{name: string, isGreedy: bool, terminator: string|null,
 *     separatorIndex: int, terminatorLength: int}>} $compiled
 *
 * @return array<array-key, string>
 */
function parseCompiled(string $value, array $compiled): array
{
    $operations = $compiled['operations'];
    if ($operations === []) {
        // Without captured variables the result is always the empty map.
        return [];
    }

    // The leading static part is checked before the separators are located.
    $segmentStart = 0;
    $leadingPart = $compiled['leadingPart'];
    if ($leadingPart !== null) {
        // A pattern that starts with a static part only matches a value that starts with it,
        // and the part is consumed.
        if (!str_starts_with($value, $leadingPart)) {
            return [];
        }

        $segmentStart = strlen($leadingPart);
    }

    // §5.2 Step 1: locate the separators from right to left.
    $separators = $compiled['separators'];
    $positions = array_fill(0, count($separators), -1);
    $bound = strlen($value) - 1;

    for ($index = count($separators) - 1; $index >= 0; $index--) {
        if ($bound < 0) {
            // No room is left for the separators to the left: their positions stay -1.
            break;
        }

        $position = rfindWithin($value, $separators[$index], $bound);
        $positions[$index] = $position;
        if ($position >= 0) {
            $bound = $position - 1;
        }
    }

    // §5.3 Step 2: cut the value from left to right.
    $results = [];

    foreach ($operations as $operation) {
        if ($operation['terminator'] === null) {
            $results[$operation['name']] = substr($value, $segmentStart);
            continue;
        }

        $separatorPosition = $positions[$operation['separatorIndex']];
        if ($separatorPosition < 0) {
            return $results;
        }

        if ($operation['isGreedy']) {
            $cut = $separatorPosition;
        } else {
            $cut = find($value, $operation['terminator'], $segmentStart);
            if ($cut < 0) {
                return $results;
            }
        }

        if ($cut < $segmentStart) {
            return $results;
        }

        $results[$operation['name']] = substr($value, $segmentStart, $cut - $segmentStart);
        $segmentStart = $cut + $operation['terminatorLength'];
    }

    return $results;
}

/**
 * Stores a compiled plan in a bounded cache, evicting the oldest entry when full.
 *
 * @param array<string, mixed> $cache
 * @param int $limit
 * @param string $key
 * @param array<mixed> $value
 */
function remember(array &$cache, int $limit, string $key, array $value): void
{
    if (count($cache) >= $limit) {
        unset($cache[array_key_first($cache)]);
    }

    $cache[$key] = $value;
}
