# UFP Format (User Friendly Pattern)

[![Build](https://github.com/Jagailo/User-Friendly-Pattern-Format/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/Jagailo/User-Friendly-Pattern-Format/actions/workflows/build.yml)
[![Version](https://img.shields.io/badge/version-1.2.0-blue.svg)](https://github.com/Jagailo/User-Friendly-Pattern-Format/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4.svg)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

UFP - user-friendly pattern format that is used to find data in search strings. Patterns consist of 2 parts: static and dynamic.

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer - required to build, test and run every project; the exact version is pinned in [`global.json`](global.json)
- [.NET Standard 2.0](https://learn.microsoft.com/dotnet/standard/net-standard) support - required to use [`UFP.NET.Standard`](src/UFP.NET.Standard): it targets `netstandard2.0`, so the parser works on .NET Framework 4.6.1+, .NET Core 2.0+, Mono, Unity and other compatible runtimes
- [Node.js](https://nodejs.org/) 20 or newer - required to run the JavaScript port, its tests and its benchmarks; they use only the standard library, so no `npm install` is needed
- [PHP](https://www.php.net/) 8.0 or newer - required to run the PHP port, its tests and its benchmarks; it uses only the standard library, so no `composer install` is needed
- An IDE that supports .NET 10 and the `.slnx` solution format: Visual Studio 2022 17.14+, JetBrains Rider 2024.3+ or VS Code with the C# Dev Kit

## Repository Structure

| Path | Description |
| --- | --- |
| [`benchmarks/UFP.JavaScript.Benchmarks`](benchmarks/UFP.JavaScript.Benchmarks) | `process.hrtime` and `process.memoryUsage` benchmarks of the JavaScript port: time and allocated bytes for the same three scenarios |
| [`benchmarks/UFP.NET.Benchmarks`](benchmarks/UFP.NET.Benchmarks) | BenchmarkDotNet benchmarks: single pattern, mega pattern and greedy variables for both .NET implementations |
| [`benchmarks/UFP.PHP.Benchmarks`](benchmarks/UFP.PHP.Benchmarks) | `hrtime` and `memory_get_usage` benchmarks of the PHP port: time and retained bytes for the same three scenarios |
| [`benchmarks/UFP.Python.Benchmarks`](benchmarks/UFP.Python.Benchmarks) | `timeit` benchmarks of the Python port: the same three scenarios |
| [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) | Normative specification of the format (grammar, algorithm in language-neutral pseudocode, conformance requirements, edge cases) and [its Russian translation](docs/SPECIFICATION.ru.md) |
| [`playground/UFP.Console.App`](playground/UFP.Console.App) | `net10.0` - console application that demonstrates both implementations |
| [`spec`](spec) | Machine-readable conformance suite for any implementation in any language |
| [`src/UFP.JavaScript`](src/UFP.JavaScript) | JavaScript (Node.js 20+) port of the parser: a single ES module with no dependencies |
| [`src/UFP.NET`](src/UFP.NET) | `net10.0` - the primary implementation |
| [`src/UFP.NET.Standard`](src/UFP.NET.Standard) | `netstandard2.0` - the same parser for legacy frameworks (.NET Framework, UWP, Unity, etc.); C# 7.3-compatible, dependency-free and allocation-light |
| [`src/UFP.PHP`](src/UFP.PHP) | PHP 8.0+ port of the parser: a single file with no dependencies |
| [`src/UFP.Python`](src/UFP.Python) | Python 3 port of the parser |
| [`tests/UFP.JavaScript.Tests`](tests/UFP.JavaScript.Tests) | `node:test` tests of the JavaScript port: the conformance suite plus unit tests of the primitives, the tokenizer, matching and pattern sets |
| [`tests/UFP.NET.Tests`](tests/UFP.NET.Tests) | xUnit v3 tests (Microsoft Testing Platform) that verify both implementations return the same result |
| [`tests/UFP.PHP.Tests`](tests/UFP.PHP.Tests) | Dependency-free test runner of the PHP port: the conformance suite plus unit tests of the primitives, the tokenizer, matching and pattern sets |
| [`tests/UFP.Python.Tests`](tests/UFP.Python.Tests) | `unittest` tests of the Python port: the conformance suite plus unit tests of the primitives, the tokenizer, matching and pattern sets |

## Usage

### .NET

The parser is distributed as source code: either reference the project (`ProjectReference`) or copy [`UfpParser.cs`](src/UFP.NET/UfpParser.cs) into your own project.

```csharp
using UFP.NET;

const string pattern = "<Date>_<Name>.<Extension>";

Dictionary<string, string> pairs = UfpParser.Parse("04-05-2024_scan.jpg", pattern);

// Date: "04-05-2024"
// Name: "scan"
// Extension: "jpg"
```

`Parse` returns an empty dictionary when the value does not match the pattern or when `null`/empty arguments are passed.

Target frameworks below .NET 10 should use `UFP.NET.Standard` (namespace `UFP.NET.Standard`) - the API is identical.

### JavaScript

[`src/UFP.JavaScript/ufp.js`](src/UFP.JavaScript/ufp.js) is a single ES module with no dependencies (Node.js 20+): copy it into your project or import it directly.

```js
import { parse } from "./src/UFP.JavaScript/ufp.js";

const pattern = "<Date>_<Name>.<Extension>";

parse("04-05-2024_scan.jpg", pattern);
// { Date: "04-05-2024", Name: "scan", Extension: "jpg" }
```

`parse` returns a plain object with one own property per captured variable, and `{}` when the value does not match the pattern or when `null`/`undefined`/empty arguments are passed. The port also exports `parseSingle`, `tokenize`, `find` and `rfindWithin` - the tokenizer and the primitives defined by the specification - for tests and for other ports.

Run its tests with `npm test` and the conformance suite with `npm run conformance` (both need only Node.js).

### PHP

[`src/UFP.PHP/ufp.php`](src/UFP.PHP/ufp.php) is a single file with no dependencies (PHP 8.0+): copy it into your project or require it directly.

```php
require_once "src/UFP.PHP/ufp.php";

$pattern = "<Date>_<Name>.<Extension>";

UFP\parse("04-05-2024_scan.jpg", $pattern);
// ["Date" => "04-05-2024", "Name" => "scan", "Extension" => "jpg"]
```

`UFP\parse` returns an array with one entry per captured variable, and `[]` when the value does not match the pattern or when `null`/empty arguments are passed. A variable name that is a canonical integer (for example `<0>`) becomes an integer array key, because PHP casts such keys; the captured pairs are the specified ones. The file also exports `UFP\parseSingle`, `UFP\tokenize`, `UFP\find` and `UFP\rfindWithin` - the tokenizer and the primitives defined by the specification - for tests and for other ports.

Run its tests with `php tests/UFP.PHP.Tests/ufp.test.php` and the conformance suite with `php src/UFP.PHP/conformance.php` (both need only PHP, no PHPUnit or composer).

## Specification

The format is defined by an open specification: [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) ([Russian translation](docs/SPECIFICATION.ru.md)). It contains the grammar, the complete parsing algorithm in language-neutral pseudocode, the failure semantics, an edge-case catalogue and the conformance requirements - everything needed to implement UFP in any language. The current format version is `UFP-1.2`.

```text
value:   04-05-2024_scan.jpg
pattern: <Date>_<Name>.<Extension>
result:  Date = "04-05-2024", Name = "scan", Extension = "jpg"
```

The specification is published as a dated public disclosure, so the format stays free for everyone: no patent is claimed and any implementation may use it royalty-free.

Any implementation can be verified against the machine-readable conformance suite [`spec/ufp-1.2.test-vectors.json`](spec/ufp-1.2.test-vectors.json) - see [`spec/README.md`](spec/README.md).

## Pattern Components

### Dynamic Parts (Variables)
- Data that may differ in searched strings
- Must have a unique name wrapped in angle brackets (`<`, `>`)
  - Examples: `<Date>`, `<Patient.FirstName>`, `<UselessData1>`
- Cannot be placed next to each other without a separator
- Can be marked as greedy by adding `*` before the closing bracket (e.g., `<StudyUid*>`)

### Static Parts (Separators)
- Data that never changes in searched strings
- Can be any character except `<`, `>`, and `|`
- Can be of any length
- A static part at the beginning of a pattern must match the beginning of the string and is not part of the captured value: `IMG_<Number>.jpg` gives `Number = "1234"` for `IMG_1234.jpg` and matches nothing for `scan_99.jpg`

## Greedy Variables
A greedy variable (marked with `*`) will match as much text as possible until reaching the next separator. This is useful when:
- The variable content may contain characters that would normally match subsequent separators
- You want to capture all remaining text until a specific pattern

Example:
`<Path*>/<Filename>.<Extension>`

For input `docs/reports/2024/annual_report.pdf`:
- Path: "docs/reports/2024"
- Filename: "annual_report"
- Extension: "pdf"

Without greedy marking, the pattern might incorrectly split on intermediate `/` characters.

## Example of Pattern and String

**String with data for parsing:**

![image](https://github.com/Jagailo/User-Friendly-Pattern-Format/assets/10468120/8719317d-fafe-4ab0-87ef-c1f226b37dab)

**User friendly pattern:**

![image](https://github.com/Jagailo/User-Friendly-Pattern-Format/assets/10468120/af59bcd8-c065-4a5a-973f-88eae2396edf)

**Parsing result:**
- Date: "22-04-2024"
- Phone: "1288724"
- Patient.FirstName: "Alexey"
- Patient.LastName: "Yagelo"
- Patient.Gender: "male"
- Reference: "KH2708530"
- UID: "3111196K001PB8"
- Extension: "doc"

## Mega Pattern
A mega pattern consists of several patterns separated by `|`. The parser will use the pattern that most closely matches the input string.

**Mega pattern example:**

`<Protocol>://<Domain>/<Page>?<FirstParameter.Key>=<FirstParameter.Value>|<Date>_<Name>.<Extension>|<StudyUid*>.<Type>.<Extension>`

**Values for parsing:**
- `04-05-2024_fh5_image.png`
- `https://youtu.be/KoFSQeOAYz4?t=10`
- `04-05-2024_scan.jpg`
- `1.2.840.52394.3.152.235.2.12.187636473.patient.csv`

**Parsing result:**
- Date: "04-05-2024"
- Name: "fh5_image"
- Extension: "png"
----
- Protocol: "https"
- Domain: "youtu.be"
- Page: "KoFSQeOAYz4"
- FirstParameter.Key: "t"
- FirstParameter.Value: "10"
----
- Date: "04-05-2024"
- Name: "scan"
- Extension: "jpg"
----
- StudyUid: "1.2.840.52394.3.152.235.2.12.187636473"
- Type: "patient"
- Extension: "csv"

## Benchmarks

### .NET

```bash
# run all .NET benchmarks (BenchmarkDotNet requires the Release configuration)
dotnet run --project benchmarks/UFP.NET.Benchmarks --configuration Release

# run a single benchmark class
dotnet run --project benchmarks/UFP.NET.Benchmarks --configuration Release -- --filter "*ModernUfpParserBenchmarks*"
```

### Python

```bash
# run the Python benchmarks (standard library only; --iterations and --repeat are optional)
py benchmarks/UFP.Python.Benchmarks/benchmark.py
```

### JavaScript

```bash
# run the JavaScript benchmarks (standard library only; --iterations, --repeat and --samples are optional)
node benchmarks/UFP.JavaScript.Benchmarks/benchmark.js

# or through npm
npm run benchmark -- --iterations 100000 --repeat 7

# time only, without the allocated-memory measurement
node benchmarks/UFP.JavaScript.Benchmarks/benchmark.js --no-memory
```

### PHP

```bash
# run the PHP benchmarks (standard library only; --iterations, --repeat and --samples are optional)
php benchmarks/UFP.PHP.Benchmarks/benchmark.php

# time only, without the retained-memory measurement
php benchmarks/UFP.PHP.Benchmarks/benchmark.php --no-memory
```

### Results

Mean time and allocated memory per single `Parse` call for .NET (BenchmarkDotNet 0.15.8, .NET 10.0.14 / RyuJIT x64, AMD Ryzen 7 7800X3D 4.20 GHz, Windows 10 22H2, 2026-11-04):

| Benchmark | `UFP.NET` (`net10.0`) | `UFP.NET.Standard` (`netstandard2.0`) |
| --- | ---: | ---: |
| `Single` - `<Date>_<Name>.<Extension>` | 146.3 ns / 440 B | 164.2 ns / 864 B |
| `MegaPattern` - three alternatives in one pattern | 342.6 ns / 664 B | 428.7 ns / 2.33 KB |
| `GreedyVariables` - `<Path*>/<Filename>.<Extension>` | 146.3 ns / 464 B | 162.5 ns / 896 B |

Both implementations only allocate the result map, the captured values and the variable names. The modern implementation is still the fastest and the most allocation-light: it is about 10-25% faster and allocates 2-3.5x less memory than the standard one. `UFP.NET.Standard` itself became about 11-12x faster on single and greedy patterns and about 17x faster on mega patterns compared with 1.1.0, and it allocates 1.7-2.2x less memory: it scans a pattern set in place and tokenizes into ranges of a `char` buffer without LINQ, `StringBuilder` or intermediate substrings, while staying C# 7.3-compatible and dependency-free.

Mean time per single `Parse` call for the Python port (`timeit`, Python 3.15.2, same machine, 2026-11-04):

| Benchmark | Mean |
| --- | ---: |
| `Single` - `<Date>_<Name>.<Extension>` | 712.3 ns |
| `MegaPattern` - three alternatives in one pattern | 1617.1 ns |
| `GreedyVariables` - `<Path*>/<Filename>.<Extension>` | 717.3 ns |

The tokenization and the compiled matching plan are cached (a bounded `lru_cache`), so these are steady-state numbers for parsing many values with the same pattern. A unique pattern on every call pays a small compilation and cache-lookup cost.

Mean time and allocated memory per single `Parse` call for the JavaScript port (`process.hrtime` and `process.memoryUsage`, Node.js 24.16.0 / V8, same machine, 2026-11-04):

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| `Single` - `<Date>_<Name>.<Extension>` | 191.2 ns | 416 B |
| `MegaPattern` - three alternatives in one pattern | 512.8 ns | 1.16 KB |
| `GreedyVariables` - `<Path*>/<Filename>.<Extension>` | 182.4 ns | 416 B |

The tokenization and the compiled matching plan are cached (bounded `Map` caches), so these are steady-state numbers as well. A `Parse` call allocates the result object, the captured values and a small positions array; the variable names and the compiled plans are shared with the cache. `Allocated` is measured from `heapUsed` deltas around one and two `Parse` calls after a forced garbage collection - the difference removes the cost of the measurement itself - so the benchmark re-runs itself with `--expose-gc` (use `--no-memory` to skip this part).

Mean time and retained memory per single `Parse` call for the PHP port (`hrtime` and `memory_get_usage`, PHP 8.3.27 CLI, same machine, 2026-11-04):

| Benchmark | Mean | Retained |
| --- | ---: | ---: |
| `Single` - `<Date>_<Name>.<Extension>` | 1167.4 ns | 488 B |
| `MegaPattern` - three alternatives in one pattern | 2613.5 ns | 504 B |
| `GreedyVariables` - `<Path*>/<Filename>.<Extension>` | 1037.5 ns | 496 B |

The tokenization and the compiled matching plan are cached in bounded static arrays, so these are steady-state numbers as well. A `Parse` call allocates the result array, the captured substrings and a small positions array; the variable names and the compiled plans are shared with the cache. `Retained` is measured from `memory_get_usage()` deltas around one and two `Parse` calls - the difference removes the cost of the measurement itself - and shows the memory that a caller keeps alive after the call (use `--no-memory` to skip this part).

## Contributing

Any contributions are welcome - please read [CONTRIBUTING.md](CONTRIBUTING.md) first. The list of changes is kept in [CHANGELOG.md](CHANGELOG.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
