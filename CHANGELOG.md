# Changelog

All notable changes to the **UFP format** and to the **parsing algorithm** are documented in this file.

The changelog is formatted according to [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). The format version (`UFP-1.x`) numbers the pattern syntax and the parsing rules; it is independent of the version of any implementation. Implementations may be added, rewritten or updated without changing the format version, so this file lists only what a user of the format or an author of a parser needs to know.

## [1.2.0] - 2026-11-04

The first published version of the UFP format and the baseline for the 1.x line.

### Added

- Normative specification `UFP-1.2`: the grammar, the primitive string operations, the complete parsing algorithm in language-neutral pseudocode, failure semantics, an edge-case catalogue and the requirements for a conforming implementation ([`docs/SPECIFICATION.md`](docs/SPECIFICATION.md)).
- Machine-readable conformance suite [`spec/ufp-1.2.test-vectors.json`](spec/ufp-1.2.test-vectors.json) - 77 vectors, each with the exact result that `Parse` must return and a stable `id`.
- Defined behaviour for every input, including ill-formed patterns: an unclosed `<`, a `>` outside a variable, a `<` inside a variable, adjacent variables and empty alternatives are not errors; tokenization is a total function and `Parse` never throws.
- Partial-match semantics: the variables captured before the first mismatch are returned, and an empty map means "the text does not have the shape described by the pattern".

### Fixed

- A static part at the beginning of a pattern is now a prefix requirement: the text must start with it and the part is consumed, so it never leaks into the first captured value. `IMG_<Number>.jpg` returns `Number = "1234"` for `IMG_1234.jpg` and an empty result for `scan_99.jpg`, which also makes alternatives such as `IMG_<Number>.jpg|scan_<Number>.jpg` distinguishable.
- A greedy variable is terminated by the correct separator when a static part precedes the first variable: the separator cursor now advances per separator part, not per extracted variable. `_<A*>_<B>` with `_a_b_c` returns `A = "a_b"`, `B = "c"`, like the equivalent pattern without the leading static part.
- A separator that cannot be found within its bound (for example a separator at index 0 with separators to its left) is treated as not found instead of raising an error, so such patterns return an empty or partial result.

### Compatibility

- Well-formed patterns that start with a variable produce the same results as in `1.1.0`.
- Patterns that start with a static part behave differently: that part is now required and consumed.

## [1.1.0] - 2025-05-16

### Added

- Greedy variables: a variable name may end with `*`, for example `<StudyUid*>` or `<Path*>/<Filename>.<Extension>`. A greedy variable captures text up to the **last** occurrence of the following separator, while a regular variable stops at the **first** one.

### Changed

- Variable names are trimmed: `<Date >` and `< Name>` are the same as `<Date>` and `<Name>`.
- Parsing algorithm rewritten: the positions of all separators are located first by scanning the text from its end, and only then is the text cut from left to right. This is what made greedy variables and deterministic partial results possible.
- A repeated variable name overwrites the previous value instead of raising an error.

### Fixed

- Empty alternatives of a pattern set (leading, trailing or doubled `|`) are ignored instead of being parsed as empty patterns.
- An unclosed `<` at the end of a pattern is treated as static text; previously such a tail was treated as a variable.

### Known issues

- The leading-static-part rule and the separator cursor were not handled correctly; both are fixed in `1.2.0`.

## [1.0.0] - 2024-05-04

### Added

- First version of the parser: variables in angle brackets (`<Date>`, `<Patient.FirstName>`), static separators of any length (any character except `<`, `>` and `|`) and pattern sets - several patterns separated by `|`, where the pattern that captured the most variables wins.

### Limitations

- No greedy variables: a variable always ends at the first occurrence of the following separator, so values that contain the separator themselves (paths, DICOM UIDs) are split incorrectly.
- Empty alternatives of a pattern set (`a||b`, leading or trailing `|`) are parsed as empty patterns.
- An unclosed `<` at the end of a pattern is treated as a variable.
- The text is scanned strictly left to right, so repeated separators behave differently than in `1.1.0`.

[1.2.0]: https://github.com/Jagailo/User-Friendly-Pattern-Format/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/Jagailo/User-Friendly-Pattern-Format/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/Jagailo/User-Friendly-Pattern-Format/releases/tag/v1.0.0
