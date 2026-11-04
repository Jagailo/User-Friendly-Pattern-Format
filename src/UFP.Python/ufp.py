# UFP Format (User Friendly Pattern)
# Format:         UFP-1.2
# Implementation: Python 3 (3.6+; standard library only)
# Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
# Author:         Alexey Yagelo
# Version:        1.2.0 (2026-11-04)
# License:        MIT (see LICENSE)

"""UFP-1.2 reference port (Python 3) - User Friendly Pattern Format.

This file is a second, deliberately small implementation of the algorithm of
docs/SPECIFICATION.md (§4 Tokenization, §5 Matching, §6 Pattern sets). It exists so
that the conformance suite can be run without .NET and so that porters have a working
example in a language with code-point string indexing.

Usage:
    py src/UFP.Python/ufp.py                 # run the conformance suite
    py src/UFP.Python/ufp.py <vectors.json>  # run a specific suite file
"""

import json
import os
import sys
from functools import lru_cache

START_CHAR = "<"
END_CHAR = ">"
ALTERNATIVE_CHAR = "|"
GREEDY_CHAR = "*"

DEFAULT_VECTORS = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "..", "spec", "ufp-1.2.test-vectors.json")


def find(value, needle, start):
    """§2.2 FIND: first index p >= start where needle occurs, or -1."""
    return value.find(needle, start)


def rfind_within(value, needle, max_end):
    """§2.2 RFIND_WITHIN: last occurrence that ends at or before max_end, or -1."""
    if max_end < 0:
        return -1
    return value.rfind(needle, 0, max_end + 1)


@lru_cache(maxsize=256)
def _compile(pattern):
    """§4-§5 Tokenize an alternative and compile its matching plan.

    Returns an immutable ``(parts, separators, leading_part, operations)`` tuple:
    ``parts`` is the tokenizer result, ``separators`` is the static parts in pattern
    order, ``leading_part`` is the static part a value must start with (or None), and
    ``operations`` lists the variables to capture as
    ``(name, is_greedy, terminator, separator_index, terminator_length)``. A variable
    whose terminator is None is the final one and captures the rest of the value.

    Tokenization and the plan are pure functions of the pattern, and a typical
    application parses many values with the same pattern, so the result is cached (see
    §10.2 of the specification). The cache is bounded and the returned containers are
    immutable, so a caller cannot corrupt it.
    """
    parts = []
    separators = []
    in_variable = False
    buffer = []

    for character in pattern:
        if character == START_CHAR:
            if not in_variable:
                in_variable = True
                if buffer:
                    text = "".join(buffer)
                    parts.append((text, True, False))
                    separators.append(text)
                    buffer = []
        elif character == END_CHAR:
            if in_variable:
                in_variable = False
                name = "".join(buffer).strip()
                is_greedy = len(name) > 0 and name[-1] == GREEDY_CHAR
                if is_greedy:
                    name = name[:-1]
                parts.append((name, False, is_greedy))
                buffer = []
        else:
            buffer.append(character)

    if buffer:
        text = "".join(buffer)
        parts.append((text, True, False))
        separators.append(text)

    leading_part = parts[0][0] if parts and parts[0][1] else None

    operations = []
    separators_seen = 0
    parts_count = len(parts)
    for index, part in enumerate(parts):
        if part[1]:
            separators_seen += 1
            continue

        if index + 1 == parts_count:
            operations.append((part[0], False, None, -1, 0))
            continue

        next_part = parts[index + 1]
        if not next_part[1]:
            # A variable that is directly followed by another variable is ignored.
            continue

        terminator = next_part[0]
        operations.append((part[0], part[2], terminator, separators_seen, len(terminator)))

    return tuple(parts), tuple(separators), leading_part, tuple(operations)


def tokenize(pattern):
    """§4 Tokenize: one alternative -> [(text, is_separator, is_greedy), ...]."""
    return list(_compile(pattern)[0])


@lru_cache(maxsize=256)
def _compile_alternatives(patterns):
    """§6.1 Split a pattern set into compiled alternatives, dropping empty pieces."""
    return tuple(
        _compile(alternative)
        for alternative in patterns.split(ALTERNATIVE_CHAR)
        if alternative)


def parse_single(value, pattern):
    """§5 ParseSingle: match one alternative."""
    return _parse_compiled(value, _compile(pattern))


def _parse_compiled(value, compiled):
    """§5.2-§5.3: match a compiled alternative against a value."""
    separators = compiled[1]
    leading_part = compiled[2]
    operations = compiled[3]
    results = {}

    if not operations:
        # Without captured variables the result is always the empty map.
        return results

    # The leading static part is checked before the separators are located.
    segment_start = 0
    if leading_part is not None:
        # A pattern that starts with a static part only matches a value that starts with it,
        # and the part is consumed.
        if not value.startswith(leading_part):
            return results

        segment_start = len(leading_part)

    # §5.2 Step 1: locate the separators from right to left.
    separators_count = len(separators)
    positions = [-1] * separators_count
    bound = len(value) - 1
    for index in range(separators_count - 1, -1, -1):
        if bound < 0:
            # No room is left for the separators to the left: their positions stay -1.
            break
        position = value.rfind(separators[index], 0, bound + 1)
        positions[index] = position
        if position >= 0:
            bound = position - 1

    # §5.3 Step 2: cut the value from left to right.
    find = value.find
    for text, is_greedy, terminator, separator_index, terminator_length in operations:
        if terminator is None:
            results[text] = value[segment_start:]
            continue

        if positions[separator_index] < 0:
            return results

        if is_greedy:
            cut = positions[separator_index]
        else:
            cut = find(terminator, segment_start)
            if cut < 0:
                return results

        if cut < segment_start:
            return results

        results[text] = value[segment_start:cut]
        segment_start = cut + terminator_length

    return results


def parse(value, patterns):
    """§6 Parse: split the pattern set, match every alternative, keep the best one."""
    if value is None or patterns is None:
        return {}

    if ALTERNATIVE_CHAR not in patterns:
        # The common case: a single alternative, so there is no pattern set to split.
        if not patterns:
            return {}
        return _parse_compiled(value, _compile(patterns))

    best = {}
    for compiled in _compile_alternatives(patterns):
        current = _parse_compiled(value, compiled)
        if len(current) > len(best):
            best = current

    return best


def run_conformance_suite(path):
    with open(path, encoding="utf-8") as stream:
        document = json.load(stream)

    failures = 0
    for vector in document["vectors"]:
        expected = vector["expected"]
        actual = parse(vector["value"], vector["pattern"])
        if actual != expected:
            failures += 1
            print("FAIL %s: expected %r, got %r" % (vector["id"], expected, actual))

    print("vectors: %d, failures: %d" % (len(document["vectors"]), failures))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(run_conformance_suite(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_VECTORS))
