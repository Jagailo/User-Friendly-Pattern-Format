// UFP Format (User Friendly Pattern)
// Format:         UFP-1.2
// Implementation: JavaScript (Node.js 20+, ES modules, no dependencies)
// Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
// Author:         Alexey Yagelo
// Version:        1.2.0 (2026-11-04)
// License:        MIT (see LICENSE)

/**
 * UFP-1.2 reference port (JavaScript) - User Friendly Pattern Format.
 *
 * This file is a third, deliberately small implementation of the algorithm of
 * docs/SPECIFICATION.md (§4 Tokenization, §5 Matching, §6 Pattern sets). It exists so
 * that the conformance suite can be run without .NET or Python and so that porters have
 * a working example in a language with UTF-16 string indexing.
 *
 * Usage:
 *     import { parse } from "./ufp.js";
 *
 *     parse("04-05-2024_scan.jpg", "<Date>_<Name>.<Extension>");
 *     // { Date: "04-05-2024", Name: "scan", Extension: "jpg" }
 *
 * Run the conformance suite:
 *     node src/UFP.JavaScript/conformance.js
 */

const START_CHAR = "<";
const END_CHAR = ">";
const ALTERNATIVE_CHAR = "|";
const GREEDY_CHAR = "*";

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
 * One token of a pattern (§4): a static separator or a variable.
 * @typedef {Object} Part
 * @property {string} text For a separator - the literal; for a variable - its name.
 * @property {boolean} isSeparator `true` for a static part.
 * @property {boolean} isGreedy `true` only for a variable marked with a trailing `*`.
 */

/**
 * A variable of a compiled alternative that takes part in matching (§5.3).
 * @typedef {Object} Operation
 * @property {string} name Variable name (trimmed, without the greedy mark).
 * @property {boolean} isGreedy
 * @property {string | null} terminator Separator that ends the variable; `null` for the final variable.
 * @property {number} separatorIndex Index of the terminator in the separators array; `-1` for the final variable.
 * @property {number} terminatorLength Length of the terminator in UTF-16 code units.
 */

/**
 * The matching plan of one alternative - a pure function of the pattern (§4, §5).
 * @typedef {Object} CompiledAlternative
 * @property {ReadonlyArray<Part>} parts
 * @property {ReadonlyArray<string>} separators Static parts in pattern order.
 * @property {string | null} leadingPart Static part the value must start with, or `null`.
 * @property {ReadonlyArray<Operation>} operations Variables to capture, in pattern order.
 */

/** @type {Map<string, CompiledAlternative>} */
const compiledCache = new Map();

/** @type {Map<string, ReadonlyArray<CompiledAlternative>>} */
const alternativesCache = new Map();

/**
 * §6 Parse: split the pattern set, match every alternative, keep the best one.
 *
 * The result is a plain object with one own enumerable string property per captured
 * variable. A variable named `__proto__` is stored as an own data property, so no
 * variable name can change the prototype of the result. The order of the properties is
 * not part of the specification (§7).
 *
 * @param {string} value Text to parse.
 * @param {string} patterns Pattern or pattern set (alternatives separated by `|`).
 * @returns {Record<string, string>} Captured variables; empty when nothing matched.
 */
export function parse(value, patterns) {
    if (value === null || value === undefined || patterns === null || patterns === undefined) {
        return {};
    }

    if (patterns.indexOf(ALTERNATIVE_CHAR) === -1) {
        // The common case: a single alternative, so there is no pattern set to split.
        if (patterns.length === 0) {
            return {};
        }

        return parseCompiled(value, compileAlternative(patterns));
    }

    let best = null;
    let bestCount = 0;

    for (const compiled of compileAlternatives(patterns)) {
        const current = parseCompiled(value, compiled);
        const currentCount = Object.keys(current).length;

        // Strictly greater: ties keep the first alternative (UFP-1.2 §6.2).
        if (currentCount > bestCount) {
            best = current;
            bestCount = currentCount;
        }
    }

    return best === null ? {} : best;
}

/**
 * §5 ParseSingle: match exactly one alternative. A `|` inside the argument is an
 * ordinary static character here - use `parse` for a pattern set.
 *
 * @param {string} value Text to parse.
 * @param {string} pattern One alternative.
 * @returns {Record<string, string>} Captured variables; empty when nothing matched.
 */
export function parseSingle(value, pattern) {
    return parseCompiled(value, compileAlternative(pattern));
}

/**
 * §4 Tokenize: one alternative -> `[{ text, isSeparator, isGreedy }, ...]`.
 *
 * Every call returns fresh objects, so a caller cannot corrupt the compiled cache.
 *
 * @param {string} pattern One alternative.
 * @returns {Part[]}
 */
export function tokenize(pattern) {
    return compileAlternative(pattern).parts.map(({ text, isSeparator, isGreedy }) => ({ text, isSeparator, isGreedy }));
}

/**
 * §2.2 FIND: the first index `p >= start` where `needle` occurs, or -1.
 *
 * @param {string} value Text to search.
 * @param {string} needle Non-empty literal to find.
 * @param {number} start Index to start the search at.
 * @returns {number}
 */
export function find(value, needle, start) {
    return value.indexOf(needle, start < 0 ? 0 : start);
}

/**
 * §2.2 RFIND_WITHIN: the last occurrence of `needle` that ends at or before `maxEnd`,
 * or -1. The bound is the index of the last *character* of the match, not the index
 * where the match may start.
 *
 * @param {string} value Text to search.
 * @param {string} needle Non-empty literal to find.
 * @param {number} maxEnd Largest index of the last character of the match.
 * @returns {number}
 */
export function rfindWithin(value, needle, maxEnd) {
    if (maxEnd < 0) {
        return -1;
    }

    const maxStart = maxEnd - needle.length + 1;
    if (maxStart < 0) {
        return -1; // The needle is longer than the allowed part of the value.
    }

    return value.lastIndexOf(needle, maxStart);
}

/**
 * §6.1 Split a pattern set into compiled alternatives, dropping empty pieces.
 *
 * The result is cached by the whole pattern set, so a typical "mega pattern" is split
 * and tokenized once (§10.2).
 *
 * @param {string} patterns Pattern set that contains at least one `|`.
 * @returns {ReadonlyArray<CompiledAlternative>}
 */
function compileAlternatives(patterns) {
    const cached = alternativesCache.get(patterns);
    if (cached !== undefined) {
        return cached;
    }

    const alternatives = [];
    for (const alternative of patterns.split(ALTERNATIVE_CHAR)) {
        if (alternative.length > 0) {
            alternatives.push(compileAlternative(alternative));
        }
    }

    return remember(alternativesCache, ALTERNATIVES_CACHE_LIMIT, patterns, Object.freeze(alternatives));
}

/**
 * §4-§5 Tokenize an alternative and compile its matching plan.
 *
 * The plan is a pure function of the pattern and is cached (see COMPILED_CACHE_LIMIT).
 * The returned containers are frozen, so a caller cannot corrupt the cache.
 *
 * @param {string} pattern One alternative.
 * @returns {CompiledAlternative}
 */
function compileAlternative(pattern) {
    const cached = compiledCache.get(pattern);
    if (cached !== undefined) {
        return cached;
    }

    const parts = tokenizeParts(pattern);
    /** @type {string[]} */
    const separators = [];
    /** @type {Operation[]} */
    const operations = [];
    let separatorsSeen = 0;

    for (let index = 0; index < parts.length; index++) {
        const part = parts[index];

        if (part.isSeparator) {
            separators.push(part.text);
            separatorsSeen++;
            continue;
        }

        if (index + 1 === parts.length) {
            operations.push(Object.freeze({
                name: part.text,
                isGreedy: false,
                terminator: null,
                separatorIndex: -1,
                terminatorLength: 0,
            }));
            continue;
        }

        const nextPart = parts[index + 1];
        if (!nextPart.isSeparator) {
            // A variable that is directly followed by another variable is ignored.
            continue;
        }

        operations.push(Object.freeze({
            name: part.text,
            isGreedy: part.isGreedy,
            terminator: nextPart.text,
            separatorIndex: separatorsSeen,
            terminatorLength: nextPart.text.length,
        }));
    }

    const leadingPart = parts.length > 0 && parts[0].isSeparator ? parts[0].text : null;
    const compiled = Object.freeze({
        parts: Object.freeze(parts),
        separators: Object.freeze(separators),
        leadingPart,
        operations: Object.freeze(operations),
    });

    return remember(compiledCache, COMPILED_CACHE_LIMIT, pattern, compiled);
}

/**
 * §4.1 Tokenize one alternative into parts.
 *
 * A `<` inside a variable and a `>` outside a variable are dropped; an unclosed variable
 * becomes a static part. The name is trimmed and a trailing `*` marks the variable as
 * greedy. The function never fails and never looks at the value.
 *
 * @param {string} pattern One alternative.
 * @returns {Part[]}
 */
function tokenizeParts(pattern) {
    const parts = [];
    let inVariable = false;
    let buffer = "";

    for (let index = 0; index < pattern.length; index++) {
        const character = pattern[index];

        switch (character) {
            case START_CHAR:
                if (!inVariable) {
                    inVariable = true;
                    if (buffer.length > 0) {
                        parts.push(createSeparator(buffer));
                        buffer = "";
                    }
                }

                break;

            case END_CHAR:
                if (inVariable) {
                    inVariable = false;
                    let name = buffer.trim();

                    // Only a trailing '*' after the trim marks the variable as greedy (§3.2).
                    const isGreedy = name.length > 0 && name.endsWith(GREEDY_CHAR);
                    if (isGreedy) {
                        name = name.slice(0, -1);
                    }

                    parts.push(Object.freeze({ text: name, isSeparator: false, isGreedy }));
                    buffer = "";
                }

                break;

            default:
                buffer += character;
                break;
        }
    }

    if (buffer.length > 0) {
        parts.push(createSeparator(buffer));
    }

    return parts;
}

/**
 * @param {string} text Non-empty literal.
 * @returns {Part}
 */
function createSeparator(text) {
    return Object.freeze({ text, isSeparator: true, isGreedy: false });
}

/**
 * §5.2-§5.3: match a compiled alternative against a value.
 *
 * Every variable extracted before the first failure is kept, so the result of a partial
 * match is always a prefix of the variables of the pattern (§5.4).
 *
 * @param {string} value Text to parse.
 * @param {CompiledAlternative} compiled
 * @returns {Record<string, string>}
 */
function parseCompiled(value, compiled) {
    const operations = compiled.operations;
    if (operations.length === 0) {
        // Without captured variables the result is always the empty map.
        return {};
    }

    let segmentStart = 0;
    if (compiled.leadingPart !== null) {
        // A pattern that starts with a static part only matches a value that starts with it,
        // and the part is consumed.
        if (!value.startsWith(compiled.leadingPart)) {
            return {};
        }

        segmentStart = compiled.leadingPart.length;
    }

    // §5.2 Step 1: locate the separators from right to left.
    const separators = compiled.separators;
    const positions = new Array(separators.length).fill(-1);
    let bound = value.length - 1;

    for (let index = separators.length - 1; index >= 0; index--) {
        if (bound < 0) {
            // No room is left for the separators to the left: their positions stay -1.
            break;
        }

        const position = rfindWithin(value, separators[index], bound);
        positions[index] = position;
        if (position >= 0) {
            bound = position - 1;
        }
    }

    // §5.3 Step 2: cut the value from left to right.
    let results = null;

    for (const operation of operations) {
        if (operation.terminator === null) {
            results = results === null ? {} : results;
            setResult(results, operation.name, value.slice(segmentStart));
            continue;
        }

        if (positions[operation.separatorIndex] < 0) {
            return results === null ? {} : results;
        }

        let cut;
        if (operation.isGreedy) {
            cut = positions[operation.separatorIndex];
        } else {
            cut = value.indexOf(operation.terminator, segmentStart);
            if (cut < 0) {
                return results === null ? {} : results;
            }
        }

        if (cut < segmentStart) {
            return results === null ? {} : results;
        }

        results = results === null ? {} : results;
        setResult(results, operation.name, value.slice(segmentStart, cut));
        segmentStart = cut + operation.terminatorLength;
    }

    return results === null ? {} : results;
}

/**
 * §7 Assign a captured value; a repeated variable name overwrites the previous value.
 *
 * @param {Record<string, string>} results
 * @param {string} name
 * @param {string} value
 */
function setResult(results, name, value) {
    if (name === "__proto__") {
        // `results[name] = value` would invoke the inherited `__proto__` setter, so the
        // entry is created as an own data property instead.
        Object.defineProperty(results, name, { value, enumerable: true, configurable: true, writable: true });
    } else {
        results[name] = value;
    }
}

/**
 * Stores a compiled plan in a bounded cache, evicting the oldest entry when full.
 *
 * @template T
 * @param {Map<string, T>} cache
 * @param {number} limit
 * @param {string} key
 * @param {T} value
 * @returns {T}
 */
function remember(cache, limit, key, value) {
    if (cache.size >= limit) {
        cache.delete(cache.keys().next().value);
    }

    cache.set(key, value);
    return value;
}
