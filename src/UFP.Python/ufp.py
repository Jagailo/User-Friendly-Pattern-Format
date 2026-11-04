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


def tokenize(pattern):
    """§4 Tokenize: one alternative -> [(text, is_separator, is_greedy), ...]."""
    parts = []
    in_variable = False
    buffer = []

    for character in pattern:
        if character == START_CHAR:
            if not in_variable:
                in_variable = True
                if buffer:
                    parts.append(("".join(buffer), True, False))
                    buffer = []
        elif character == END_CHAR:
            if in_variable:
                in_variable = False
                name = "".join(buffer).strip()
                is_greedy = False
                if name.endswith(GREEDY_CHAR):
                    name = name[:-1]
                    is_greedy = True
                parts.append((name, False, is_greedy))
                buffer = []
        else:
            buffer.append(character)

    if buffer:
        parts.append(("".join(buffer), True, False))

    return parts


def parse_single(value, pattern):
    """§5 ParseSingle: match one alternative."""
    results = {}
    parts = tokenize(pattern)
    separators = [part[0] for part in parts if part[1]]

    # §5.2 Step 1: locate the separators from right to left.
    positions = [-1] * len(separators)
    bound = len(value) - 1
    for index in range(len(separators) - 1, -1, -1):
        positions[index] = rfind_within(value, separators[index], bound)
        if positions[index] >= 0:
            bound = positions[index] - 1

    # §5.3 Step 2: cut the value from left to right.
    segment_start = 0
    if parts and parts[0][1]:
        # A pattern that starts with a static part only matches a value that starts with it,
        # and the part is consumed.
        leading_part = parts[0][0]
        if not value.startswith(leading_part):
            return results

        segment_start = len(leading_part)

    separators_seen = 0
    for index, (text, is_separator, is_greedy) in enumerate(parts):
        if is_separator:
            separators_seen += 1
            continue

        if index + 1 == len(parts):
            results[text] = value[segment_start:]
            continue

        if not parts[index + 1][1]:
            continue

        terminator = parts[index + 1][0]
        if positions[separators_seen] < 0:
            return results

        if is_greedy:
            cut = positions[separators_seen]
        else:
            cut = find(value, terminator, segment_start)
            if cut < 0:
                return results

        if cut < segment_start:
            return results

        results[text] = value[segment_start:cut]
        segment_start = cut + len(terminator)

    return results


def parse(value, patterns):
    """§6 Parse: split the pattern set, match every alternative, keep the best one."""
    if value is None or patterns is None:
        return {}

    alternatives = [chunk for chunk in patterns.split(ALTERNATIVE_CHAR) if chunk]
    if not alternatives:
        return {}

    if len(alternatives) == 1:
        return parse_single(value, alternatives[0])

    best = {}
    for alternative in alternatives:
        current = parse_single(value, alternative)
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
