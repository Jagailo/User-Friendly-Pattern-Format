# UFP Format (User Friendly Pattern)
# Format:         UFP-1.2
# Implementation: Python 3 (3.6+; standard library only)
# Repository:     https://github.com/Jagailo/User-Friendly-Pattern-Format
# Author:         Alexey Yagelo
# Version:        1.2.0 (2026-11-04)
# License:        MIT (see LICENSE)

"""Tests of the Python reference port (src/UFP.Python/ufp.py).

Run from the repository root:

    py -m unittest discover -s tests/UFP.Python.Tests -v
"""

import json
import os
import sys
import unittest

REPOSITORY_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(REPOSITORY_ROOT, "src", "UFP.Python"))

import ufp  # noqa: E402 - the port is a single module, not a package

VECTORS_PATH = os.path.join(REPOSITORY_ROOT, "spec", "ufp-1.2.test-vectors.json")
SPECIFICATION_VERSION = "UFP-1.2"


def load_vectors():
    with open(VECTORS_PATH, encoding="utf-8") as stream:
        return json.load(stream)


class ConformanceTests(unittest.TestCase):
    """The machine-readable conformance suite of the specification."""

    @classmethod
    def setUpClass(cls):
        cls.document = load_vectors()

    def test_suite_belongs_to_the_implemented_format_version(self):
        self.assertEqual(SPECIFICATION_VERSION, self.document["specification"])

    def test_every_vector_is_reproduced(self):
        for vector in self.document["vectors"]:
            with self.subTest(vector=vector["id"]):
                self.assertEqual(vector["expected"], ufp.parse(vector["value"], vector["pattern"]))

    def test_no_vector_raises(self):
        for vector in self.document["vectors"]:
            with self.subTest(vector=vector["id"]):
                ufp.parse(vector["value"], vector["pattern"])


class PrimitiveTests(unittest.TestCase):
    """§2.2 of the specification - the primitives the algorithm is built on."""

    def test_find_returns_the_first_occurrence_at_or_after_the_start(self):
        self.assertEqual(1, ufp.find("a_b_c", "_", 0))
        self.assertEqual(3, ufp.find("a_b_c", "_", 2))
        self.assertEqual(-1, ufp.find("a_b_c", ".", 0))

    def test_rfind_within_bounds_the_end_of_the_match(self):
        # "XX" occurs at 1 and at 2; the match must end at or before max_end.
        self.assertEqual(2, ufp.rfind_within("aXXXb", "XX", 3))
        self.assertEqual(1, ufp.rfind_within("aXXXb", "XX", 2))
        self.assertEqual(-1, ufp.rfind_within("aXXXb", "XX", 1))
        self.assertEqual(-1, ufp.rfind_within("aXXXb", "XX", -1))

    def test_rfind_within_is_not_a_start_index_search(self):
        # A match that starts exactly at max_end but extends past it is not allowed.
        self.assertEqual(0, ufp.rfind_within("abab", "ab", 2))
        self.assertEqual(2, ufp.rfind_within("abab", "ab", 3))


class TokenizerTests(unittest.TestCase):
    """§4 of the specification."""

    def test_variables_and_separators(self):
        self.assertEqual(
            [
                ("Date", False, False),
                ("_", True, False),
                ("Name", False, False),
                (".", True, False),
                ("Extension", False, False),
            ],
            ufp.tokenize("<Date>_<Name>.<Extension>"))

    def test_greedy_mark_is_removed_from_the_name(self):
        self.assertEqual(
            [("Path", False, True), ("/", True, False), ("Filename", False, False)],
            ufp.tokenize("<Path*>/<Filename>"))

    def test_empty_name_is_a_variable(self):
        self.assertEqual([("", False, False), ("_", True, False), ("B", False, False)],
                         ufp.tokenize("<>_<B>"))

    def test_adjacent_variables_are_two_parts(self):
        self.assertEqual([("A", False, False), ("B", False, False)], ufp.tokenize("<A><B>"))

    def test_name_is_trimmed_before_the_mark_is_removed(self):
        self.assertEqual([("A ", False, True)], ufp.tokenize("<A *>"))

    def test_mark_inside_the_name_is_not_greedy(self):
        self.assertEqual([("A*B", False, False)], ufp.tokenize("<A*B>"))

    def test_unclosed_variable_becomes_a_separator(self):
        self.assertEqual([("Date", False, False), ("_", True, False), ("Name", True, False)],
                         ufp.tokenize("<Date>_<Name"))

    def test_end_char_outside_a_variable_is_dropped(self):
        self.assertEqual([("ab", True, False), ("C", False, False)], ufp.tokenize("a>b<C>"))

    def test_start_char_inside_a_variable_is_dropped(self):
        self.assertEqual([("AB", False, False)], ufp.tokenize("<A<B>>"))


class MatchingTests(unittest.TestCase):
    """§5 of the specification."""

    def test_single_variable_takes_the_rest_of_the_value(self):
        self.assertEqual({"Value": "any text here"}, ufp.parse("any text here", "<Value>"))
        self.assertEqual({"Value": ""}, ufp.parse("", "<Value>"))

    def test_non_greedy_variable_stops_at_the_first_occurrence(self):
        self.assertEqual({"A": "1", "B": "2", "C": "3.4"}, ufp.parse("1.2.3.4", "<A>.<B>.<C>"))

    def test_greedy_variable_stops_at_the_last_occurrence(self):
        self.assertEqual({"A": "1.2", "B": "3", "C": "4"}, ufp.parse("1.2.3.4", "<A*>.<B>.<C>"))

    def test_greedy_variable_keeps_intermediate_separators(self):
        self.assertEqual({"A": "a_b", "B": "c"}, ufp.parse("a_b_c", "<A*>_<B>"))
        self.assertEqual({"A": "a", "B": "b_c"}, ufp.parse("a_b_c", "<A>_<B*>"))

    def test_leading_static_part_is_consumed(self):
        self.assertEqual({"Number": "1234"}, ufp.parse("IMG_1234.jpg", "IMG_<Number>.jpg"))

    def test_leading_static_part_is_required(self):
        self.assertEqual({}, ufp.parse("scan_99.jpg", "IMG_<Number>.jpg"))

    def test_leading_static_part_must_be_a_prefix(self):
        self.assertEqual({}, ufp.parse("x_IMG_1.jpg", "IMG_<Number>.jpg"))

    def test_leading_static_part_does_not_shift_a_greedy_variable(self):
        self.assertEqual({"A": "a_b", "B": "c"}, ufp.parse("_a_b_c", "_<A*>_<B>"))

    def test_trailing_static_part_is_consumed_by_the_variable_before_it(self):
        self.assertEqual({"A": "x", "B": "png"}, ufp.parse("x.png.jpg", "<A>.<B>.jpg"))

    def test_missing_trailing_static_part_returns_the_partial_result(self):
        self.assertEqual({"A": "x"}, ufp.parse("x.png", "<A>.<B>.jpg"))

    def test_empty_segments_are_allowed(self):
        self.assertEqual({"A": "", "B": ""}, ufp.parse("_", "<A>_<B>"))
        self.assertEqual({"A": "", "B": "x"}, ufp.parse("_x", "<A>_<B>"))
        self.assertEqual({"A": "x", "B": ""}, ufp.parse("x_", "<A>_<B>"))

    def test_separator_at_the_start_of_the_value_leaves_no_room_for_the_chain(self):
        self.assertEqual({}, ufp.parse("_x", "<A>_<B>_<C>"))
        self.assertEqual({}, ufp.parse("_x_y", "<A>_<B>_<C>_<D>"))

    def test_repeated_name_keeps_the_last_value(self):
        self.assertEqual({"A": "2"}, ufp.parse("1_2", "<A>_<A>"))

    def test_value_and_pattern_are_case_sensitive(self):
        self.assertEqual({"a": "Abc", "B": "def"}, ufp.parse("Abc_def", "<a>_<B>"))

    def test_unicode_is_not_split(self):
        self.assertEqual({"A": "\U0001f642", "B": "\U0001f643"},
                         ufp.parse("\U0001f642_\U0001f643", "<A>_<B>"))
        self.assertEqual({"A": "\u043f\u0440\u0438\u0432\u0435\u0442", "B": "\u043c\u0438\u0440"},
                         ufp.parse("\u043f\u0440\u0438\u0432\u0435\u0442_\u043c\u0438\u0440", "<A>_<B>"))

    def test_none_arguments_return_the_empty_map(self):
        self.assertEqual({}, ufp.parse(None, "<A>"))
        self.assertEqual({}, ufp.parse("x", None))

    def test_empty_pattern_returns_the_empty_map(self):
        self.assertEqual({}, ufp.parse("x", ""))

    def test_pattern_without_variables_returns_the_empty_map(self):
        self.assertEqual({}, ufp.parse("abc", "abc"))


class PatternSetTests(unittest.TestCase):
    """§6 of the specification."""

    def test_more_captured_variables_win(self):
        self.assertEqual({"A": "1", "B": "2"}, ufp.parse("1_2", "<A>_<B>|<A>"))
        self.assertEqual({"A": "1", "B": "2"}, ufp.parse("1-2", "<A>_<B>|<A>-<B>"))

    def test_tie_keeps_the_first_alternative(self):
        self.assertEqual({"A": "x"}, ufp.parse("x", "<A>|<B>"))
        self.assertEqual({"A": "2"}, ufp.parse("1_2", "<A>_<A>|<A>"))

    def test_empty_alternatives_are_ignored(self):
        self.assertEqual({"A": "x"}, ufp.parse("x", "|<A>|"))
        self.assertEqual({}, ufp.parse("x", "||"))

    def test_leading_static_part_selects_the_alternative(self):
        self.assertEqual({"Number": "99"}, ufp.parse("scan_99.jpg", "IMG_<Number>.jpg|scan_<Number>.jpg"))
        self.assertEqual({}, ufp.parse("other_5.jpg", "IMG_<Number>.jpg|scan_<Number>.jpg"))


if __name__ == "__main__":
    unittest.main()

