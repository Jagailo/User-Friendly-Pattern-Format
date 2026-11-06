# UFP — User Friendly Pattern Format

[![Build](https://github.com/Jagailo/User-Friendly-Pattern-Format/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/Jagailo/User-Friendly-Pattern-Format/actions/workflows/build.yml)
[![Version](https://img.shields.io/badge/format-UFP--1.2-blue.svg)](docs/SPECIFICATION.md)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

UFP (User Friendly Pattern Format) is a small open pattern language for extracting named values from text, together with its formal specification and reference implementations in several languages. A pattern is simply an example of the text with the changing parts replaced by names in angle brackets:

```text
value:   04-05-2024_scan.jpg
pattern: <Date>_<Name>.<Extension>
result:  Date = "04-05-2024", Name = "scan", Extension = "jpg"
```

There is no escaping, no character classes and no quantifiers - the language is deliberately much smaller than a regular expression. The format is defined by the open specification [UFP-1.2](docs/SPECIFICATION.md) and is implemented in C# (.NET 10 and .NET Standard 2.0), JavaScript, PHP and Python.

UFP is meant for the case when the pattern is written by the *end user*: a doctor naming scans, an accountant splitting invoice numbers, a mechanic reading part codes. It is a focused alternative to regular expressions and to [URI Templates (RFC 6570)](https://www.rfc-editor.org/rfc/rfc6570) for the common task "split this text into named fields".

**Writing patterns yourself?** Start with the [user guide](docs/USER-GUIDE.md) - it takes about five minutes. Integrating UFP into an application or porting it to another language? Read the [specification](docs/SPECIFICATION.md), take the parser file for your language from the table below, and verify it with the [conformance suite](spec/README.md).

**Want to try it first?** The [live demo](https://jagailo.github.io/User-Friendly-Pattern-Format/playground/UFP.HTML/) parses the text under a pattern as you type and shows the captured variables, with nothing to install.

## Why UFP

| | Regular expressions | URI Templates (RFC 6570) | UFP |
| --- | --- | --- | --- |
| Written by a non-programmer | Hard: escaping, groups, quantifiers | Works only for URL-shaped text | The pattern looks like an example of the text |
| Named extraction | Extra `(?<Name>...)` syntax | Expression names | The name *is* the slot: `<Name>` |
| Arbitrary static text around values | Needs quoting and escaping | Limited | Any character, no escaping |
| Several shapes in one pattern | Alternation plus anchors | No | Alternatives separated by `\|` |
| Greedy captures | `.*` vs `.*?` and backtracking | Prefixes and operators | One `*` before `>` |
| Deterministic result | Depends on the engine | Yes | Fully specified |
| Worst case | Can be catastrophic (ReDoS) | - | Linear, no backtracking |

Key properties:

- **Readable by end users.** A pattern is written as one real example with holes: `<Date>_<Name>.<Extension>`. No manual, no escaping rules to remember.
- **The pattern is the schema.** Capturing happens by name, so the result is immediately usable structured data.
- **No escaping.** Every character except `<`, `>`, `|` is a literal - spaces, dots, slashes, Cyrillic, Arabic, Hebrew and emoji are all fine.
- **Several shapes in one pattern.** Alternatives are part of the syntax: `IMG_<Number>.jpg|scan_<Number>.jpg` replaces several separate checks, and the alternative that captured the most names wins.
- **No regex under the hood.** The pattern is not translated into a regular expression; it is tokenized into a small matching plan and matched with ordinary substring searches.
- **Linear and predictable.** Separators are located with ordinary substring searches; there is no recursion and no backtracking, so a hostile pattern cannot hang the parser.
- **Never fails.** `Parse` always returns a map: a mismatch gives an empty map or the variables captured before it. Every conforming implementation returns the same result for the same input.
- **Tiny and dependency-free.** Each implementation is a single source file with no third-party dependencies, and every one of them is verified against the same conformance suite.

## Documentation

| Document | For whom |
| --- | --- |
| [User guide](docs/USER-GUIDE.md) | End users - doctors, accountants, mechanics: how to write a pattern in five minutes, with simple examples |
| [Specification](docs/SPECIFICATION.md) ([Russian translation](docs/SPECIFICATION.ru.md)) | Implementers: grammar, primitive operations, the complete algorithm in language-neutral pseudocode, failure semantics, edge cases, conformance requirements |
| [Conformance suite](spec/README.md) | Anyone writing or checking a port: 77 machine-readable vectors in [`spec/ufp-1.2.test-vectors.json`](spec/ufp-1.2.test-vectors.json) |
| [Changelog](CHANGELOG.md) | History of the format and of the parsing algorithm |
| [Contributing](CONTRIBUTING.md) | How to build, test and propose changes |

## The format at a glance

| Element | Meaning | Example |
| --- | --- | --- |
| `<Name>` | A variable: captures the text at this place and stores it under `Name` | `<Date>`, `<Patient.FirstName>` |
| `text` | A static part (separator): literal text that must be present | `_`, `.jpg`, `IMG_` |
| `<Name*>` | A greedier variable: captures as much as possible up to the **last** following separator | `<Path*>/<Filename>.<Extension>` |
| `A\|B` | A pattern set ("mega pattern"): the alternative that captures the most variables wins | `IMG_<Number>.jpg\|scan_<Number>.jpg` |

Rules in one screen:

- A name is anything between `<` and `>`, trimmed at the edges and case-sensitive. The same name may be reused; the last captured value wins.
- Every variable that captures text is returned: there is no anonymous or discard syntax, and `<>` is the empty name, not a way to hide a value. If a value is not interesting, give it a name and ignore it in the code.
- A static part may be any text without `<`, `>`, `|`. It is matched exactly, including spaces.
- A static part at the beginning of a pattern must match the beginning of the value and is not captured: `IMG_<Number>.jpg` gives `Number = "1234"` for `IMG_1234.jpg` and an empty result for `scan_99.jpg`.
- Two variables cannot stand next to each other: in `<A><B>` the first one is ignored.
- The last variable captures the rest of the value, including text that looks like a separator.
- Text direction does not matter: right-to-left scripts such as Arabic and Hebrew are matched in logical order.
- The result is always a map of strings. Values are never trimmed, converted or validated - `"10"` stays `"10"`.
- A pattern that fits only partially returns the variables captured before the first mismatch; an empty map means that the value does not have the shape described by the pattern.

The full rules, including edge cases, are in the [specification](docs/SPECIFICATION.md) and in the [user guide](docs/USER-GUIDE.md).

```text
pattern: <Path*>/<Filename>.<Extension>
value:   docs/reports/2024/annual_report.pdf
result:  Path = "docs/reports/2024", Filename = "annual_report", Extension = "pdf"
```

## Patterns as a schema

Variable names are stable keys, not just throwaway labels for one file. An application that knows its patterns can use them as a small schema:

- **Explain the fields.** Show the user which values a pattern captures and what each one means: `<Patient.FirstName>` can be displayed as "First name" or "Имя", while the pattern keeps the technical key.
- **Build patterns in a UI.** Offer a palette of known variables and let the user assemble a pattern by drag and drop instead of typing angle brackets by hand.
- **Share one vocabulary.** The same names are used by the parser, by the UI labels and by the code that consumes the result, so a pattern doubles as configuration and as documentation of the data.

Names may also be completely free-form when the end user writes patterns for their own files - the format does not care.

## Implementations

Every implementation is a single dependency-free source file that passes the [conformance suite](spec/README.md). Copy the file into your project (or reference the project) - there is nothing to install.

| Language | Platform | Parser file | Entry point |
| --- | --- | --- | --- |
| C# | .NET 10 (`net10.0`) | [`src/UFP.NET/UfpParser.cs`](src/UFP.NET/UfpParser.cs) | `UFP.NET.UfpParser.Parse` |
| C# | .NET Standard 2.0 (`netstandard2.0`, C# 7.3) - .NET Framework 4.6.1+, UWP, Unity, Xamarin and other legacy targets | [`src/UFP.NET.Standard/UfpParser.cs`](src/UFP.NET.Standard/UfpParser.cs) | `UFP.NET.Standard.UfpParser.Parse` |
| JavaScript | Node.js 20+, ES module | [`src/UFP.JavaScript/ufp.js`](src/UFP.JavaScript/ufp.js) | `parse` |
| PHP | PHP 8.0+ | [`src/UFP.PHP/ufp.php`](src/UFP.PHP/ufp.php) | `UFP\parse` |
| Python | Python 3.6+ | [`src/UFP.Python/ufp.py`](src/UFP.Python/ufp.py) | `parse` |

`Parse` / `parse` always returns a map (dictionary, object, array or `dict`) of the captured variables and never throws: a value that does not fit gives an empty map (or the variables captured before the mismatch), and `null`/`None` arguments give an empty map as well. An empty map means that the value does not have the shape described by the pattern.

## Usage

### .NET

Use `UFP.NET` on .NET 10, and `UFP.NET.Standard` (identical API) on any older target:

```csharp
using UFP.NET; // or UFP.NET.Standard
using System.Collections.Generic;

const string pattern = "<Date>_<Name>.<Extension>";

Dictionary<string, string> pairs = UfpParser.Parse("04-05-2024_scan.jpg", pattern);

// pairs["Date"]      = "04-05-2024"
// pairs["Name"]      = "scan"
// pairs["Extension"] = "jpg"
```

### JavaScript

```js
import { parse } from "./src/UFP.JavaScript/ufp.js";

const pattern = "<Date>_<Name>.<Extension>";

parse("04-05-2024_scan.jpg", pattern);
// { Date: "04-05-2024", Name: "scan", Extension: "jpg" }
```

### PHP

```php
require_once "src/UFP.PHP/ufp.php";

$pattern = "<Date>_<Name>.<Extension>";

UFP\parse("04-05-2024_scan.jpg", $pattern);
// ["Date" => "04-05-2024", "Name" => "scan", "Extension" => "jpg"]
```

A variable name that is a canonical integer (for example `<0>`) becomes an integer array key, because PHP casts such keys; the captured pairs are the specified ones.

### Python

```python
from ufp import parse  # ufp.py copied into your project or on sys.path

pattern = "<Date>_<Name>.<Extension>"

parse("04-05-2024_scan.jpg", pattern)
# {'Date': '04-05-2024', 'Name': 'scan', 'Extension': 'jpg'}
```

The ports also export their tokenizer and the primitive string operations (`tokenize`, `find`, `rfindWithin` / `rfind_within`) for tests and for other ports.

## Specification and conformance

The format is defined by [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) ([Russian translation](docs/SPECIFICATION.ru.md)): the grammar, the primitive string operations, the complete algorithm in language-neutral pseudocode, failure semantics, an edge-case catalogue and the requirements for a conforming implementation. The current version is **UFP-1.2** (published 2026-11-04). The specification is a dated public disclosure under the MIT license, so the format stays free for every implementation, including proprietary ones.

Every implementation is verified against [`spec/ufp-1.2.test-vectors.json`](spec/ufp-1.2.test-vectors.json) - 77 vectors, each with the exact map that `Parse` must return. The format of the file and the way to run it against your own implementation are described in [`spec/README.md`](spec/README.md):

```bash
dotnet test UFP.slnx --configuration Release --filter "FullyQualifiedName~UfpConformanceTests"   # .NET
py src/UFP.Python/ufp.py                                                                         # Python
node src/UFP.JavaScript/conformance.js                                                           # JavaScript
php src/UFP.PHP/conformance.php                                                                  # PHP
# .NET reports 77 passed conformance tests; each port runner prints: vectors: 77, failures: 0
```

The format version is independent of the version of any implementation: ports can be added, rewritten or updated without changing `UFP-1.2`, as long as they keep passing the suite. The format itself is intentionally closed and small: it is not going to grow into a regular-expression engine, and its syntax is not expected to change.

## Benchmarks

Mean time and memory per single `Parse` call. Scenarios: `Single` = `<Date>_<Name>.<Extension>`, `MegaPattern` = three alternatives in one pattern, `GreedyVariables` = `<Path*>/<Filename>.<Extension>`. Same machine (AMD Ryzen 7 7800X3D 4.20 GHz, Windows 10 22H2, 2026-11-04); BenchmarkDotNet 0.15.8 for .NET, the standard-library timers of each language for the ports (see [`benchmarks/`](benchmarks)).

| Implementation | `Single` | `MegaPattern` | `GreedyVariables` |
| --- | ---: | ---: | ---: |
| .NET 10 (`net10.0`) | 146.3 ns / 440 B | 342.6 ns / 664 B | 146.3 ns / 464 B |
| .NET Standard 2.0 | 164.2 ns / 864 B | 428.7 ns / 2.33 KB | 162.5 ns / 896 B |
| JavaScript (Node.js 24) | 191.2 ns / 416 B | 512.8 ns / 1.16 KB | 182.4 ns / 416 B |
| Python 3.15 | 709.9 ns / 361 B | 1664.2 ns / 387 B | 697.0 ns / 372 B |
| PHP 8.3 | 1167.4 ns / 488 B | 2613.5 ns / 504 B | 1037.5 ns / 496 B |

The numbers are steady state: the tokenized pattern and the compiled matching plan are cached, so parsing many values with the same pattern does not pay for compilation again. Memory is the memory allocated by one call (retained memory for PHP).

The modern .NET build is the fastest and the most allocation-light; .NET Standard 2.0 is 11-25% slower and allocates 1.9-3.6x more memory, the price of staying C# 7.3-compatible on `netstandard2.0` without the newer span APIs. JavaScript is within 1.5x of .NET 10, Python is about 5x and PHP 7-8x slower. A typical single-pattern parse stays between 0.15 and 1.2 microseconds in every implementation, and all of them allocate only the result map, the captured values and a small positions array.

## Repository layout

| Path | Contents |
| --- | --- |
| [`src/`](src) | One dependency-free parser per language |
| [`spec/`](spec) | Conformance vectors and instructions for running them |
| [`docs/`](docs) | Specification (English and Russian) and the end-user guide |
| [`tests/`](tests) | Unit tests and conformance runners for every parser |
| [`benchmarks/`](benchmarks) | Benchmark projects for every parser |
| [`playground/`](playground) | A small .NET console app that prints the parsing result for a few patterns and values, and HTML live demo page |

## Contributing

Any contributions are welcome - please read [CONTRIBUTING.md](CONTRIBUTING.md) first. The list of changes is kept in [CHANGELOG.md](CHANGELOG.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
