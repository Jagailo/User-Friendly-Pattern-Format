# Changelog

All notable changes to the **UFP parser** (`UfpParser`) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Every entry describes the parser itself - the UFP pattern syntax and the parsing behavior - i.e. only what matters when copying `UfpParser.cs` into another project.

## [1.1.1] - 2026-11-04

### Fixed

- Both implementations did not compile in 1.1.0: `Length` was used on a `List<PatternPart>` instead of `Count` in `UFP.NET` and `UFP.NET.Standard`.

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

- The source in this tag does not compile: `SplitPattern` returns `List<PatternPart>` while the callers still access `.Length`; use [1.1.1] instead.

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

[1.1.1]: https://github.com/Jagailo/User-Friendly-Pattern-Format/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/Jagailo/User-Friendly-Pattern-Format/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/Jagailo/User-Friendly-Pattern-Format/releases/tag/v1.0.0
