# Changelog

All notable changes to the **UFP parser** (`UfpParser`) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Every entry describes the parser itself - the UFP pattern syntax and the parsing behavior - i.e. only what matters when copying `UfpParser.cs` into another project.

## [1.2.0] - 2026-11-04

### Changed

- Both implementations implement the format `UFP-1.2` - the first published version of the format, specified in [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md).

### Fixed

- Both implementations did not compile since 1.1.0: `Length` was used on a `List<PatternPart>` instead of `Count` in `UFP.NET` and `UFP.NET.Standard`. This fix was prepared as 1.1.1, which was never released - 1.2.0 supersedes it.
- A static part at the beginning of a pattern is now verified and consumed: a value that does not start with it does not match, and the part is no longer part of the first captured value. `IMG_<Number>.jpg` returns `Number = "1234"` for `IMG_1234.jpg` (previously `Number = "IMG_1234"`) and returns an empty result for `scan_99.jpg` (previously `Number = "scan_99"`), so alternatives of a pattern set such as `IMG_<Number>.jpg|scan_<Number>.jpg` are now distinguishable.
- `Parse` no longer throws `ArgumentOutOfRangeException` when a separator occupies the beginning of the value and the pattern still contains separators to its left - e.g. `<A>_<B>_<C>` with `_x` or `<A>_<B>_<C>_<D>` with `_x_y`. Such a separator is treated as not found, so the pattern returns the variables captured before the failure (in these examples - an empty result).
- A greedy variable was terminated by the wrong static part when a static part precedes the first variable: the separator cursor is now advanced by every static part instead of by every extracted variable. `<A*>_<B>` with `a_b_c` was and still is `A = "a_b"`, `B = "c"`, but `_<A*>_<B>` with `_a_b_c` returned `A = ""`, `B = "a_b_c"` and now returns `A = "a_b"`, `B = "c"`, like the equivalent pattern without the leading static part.
- `Parse` no longer throws when the located separator occurrence lies before the current segment start: such a pattern stops and returns the variables captured so far.

### Compatibility

- Results of every pattern that starts with a variable are unchanged. Only patterns that start with a static part behave differently: that part is now required and consumed, and the separator cursor follows the pattern parts. The behaviour is fully specified in [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md).

## [1.1.0] - 2025-05-16

### Added

- Greedy variables: a variable name may end with `*` - e.g. `<StudyUid*>`, `<Path*>/<Filename>.<Extension>`. A greedy variable captures text up to the **last** occurrence of the following separator, while a regular variable stops at the **first** one.
- Two implementations with identical API and behavior: `UFP.NET` (`net8.0`; nullable reference types, `record`, ranges) and `UFP.NET.Standard` (`netstandard2.0`; C# 7.3 compatible) - pick the file that matches the target framework of your project.

### Changed

- Variable names are trimmed: `<Date >` and `< Name>` are now the same as `<Date>` and `<Name>`.
- Parsing algorithm rewritten: positions of all static separators are located first, scanning the value from its end (`LastIndexOf`), and only then the segments are cut. Previously the value was scanned left to right with a "pending variable", which made greedy matching impossible.
- Extracted values are assigned through the indexer (`results[key] = value`), so a repeated variable name overwrites the previous value instead of throwing.

### Fixed

- Empty parts of a mega pattern (e.g. `<Date>_<Name>||<Extension>`, trailing `|`) are skipped instead of being parsed as an empty pattern - `StringSplitOptions.RemoveEmptyEntries` is used when splitting patterns by `|`.
- An unclosed `<` at the end of a pattern is treated as a static separator; previously such a tail was treated as a variable.

### Known issues

- The source in this tag does not compile: `SplitPattern` returns `List<PatternPart>` while the callers still access `.Length`; use [1.2.0] instead.

## [1.0.0] - 2024-05-04

### Added

- First version of the parser: `Implementation/.NET Standard 2.0/UfpParser.cs` (namespace `UFP`, single file, `netstandard2.0`) - this is the file to copy for this release.
- Variables in angle brackets (`<Date>`, `<Patient.FirstName>`) and static separators of any length - any character except `<`, `>` and `|`.
- Mega patterns: several patterns separated by `|`; the value is parsed by the pattern that found the most variables.
- Format specification in the README: rules for variables and separators, a pattern/string/result example and a mega pattern example.

### Limitations

- No greedy variables: a variable always ends at the first occurrence of the following separator, so values that contain the separator themselves (e.g. paths or DICOM UIDs) are split incorrectly.
- Empty parts of a mega pattern (`a||b`, leading or trailing `|`) are parsed as empty patterns.
- An unclosed `<` at the end of a pattern is treated as a variable.
- The value is scanned strictly left to right, so repeated separators behave differently than in 1.1.0.

[1.2.0]: https://github.com/Jagailo/User-Friendly-Pattern-Format/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/Jagailo/User-Friendly-Pattern-Format/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/Jagailo/User-Friendly-Pattern-Format/releases/tag/v1.0.0
