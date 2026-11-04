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
- An IDE that supports .NET 10 and the `.slnx` solution format: Visual Studio 2022 17.14+, JetBrains Rider 2024.3+ or VS Code with the C# Dev Kit

## Repository Structure

| Path | Description |
| --- | --- |
| [`benchmarks/UFP.Benchmarks`](benchmarks/UFP.Benchmarks) | BenchmarkDotNet benchmarks: single pattern, mega pattern and greedy variables for both implementations |
| [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) | Normative specification of the format (grammar, algorithm in language-neutral pseudocode, conformance requirements, edge cases) and [its Russian translation](docs/SPECIFICATION.ru.md) |
| [`playground/UFP.Console.App`](playground/UFP.Console.App) | `net10.0` - console application that demonstrates both implementations |
| [`spec`](spec) | Machine-readable conformance suite for any implementation in any language |
| [`src/UFP.NET`](src/UFP.NET) | `net10.0` - the primary implementation with nullable reference types enabled |
| [`src/UFP.NET.Standard`](src/UFP.NET.Standard) | `netstandard2.0` - the same parser for legacy frameworks (.NET Framework, UWP, Unity, etc.) |
| [`src/UFP.Python`](src/UFP.Python) | Python 3 - reference port of the algorithm with a runner for the conformance suite (further ports, e.g. JavaScript or PHP, belong here too) |
| [`tests/UFP.NET.Tests`](tests/UFP.NET.Tests) | xUnit v3 tests (Microsoft Testing Platform) that verify both implementations return the same result |
| [`tests/UFP.Python.Tests`](tests/UFP.Python.Tests) | `unittest` tests of the Python port: the conformance suite plus unit tests of the primitives, the tokenizer, matching and pattern sets |

## Usage

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

```bash
# run all benchmarks (BenchmarkDotNet requires the Release configuration)
dotnet run --project benchmarks/UFP.Benchmarks --configuration Release

# run a single benchmark class
dotnet run --project benchmarks/UFP.Benchmarks --configuration Release -- --filter "*ModernParserBenchmarks*"
```

### Results

Mean time and allocated memory per single `Parse` call (BenchmarkDotNet 0.15.8, .NET 10.0.14 / RyuJIT x64, AMD Ryzen 7 7800X3D 4.20 GHz, Windows 10 22H2, 2026-11-04):

| Benchmark | `UFP.NET` (`net10.0`) | `UFP.NET.Standard` (`netstandard2.0`) |
| --- | ---: | ---: |
| `Single` - `<Date>_<Name>.<Extension>` | 275.4 ns / 1.26 KB | 1.899 μs / 1.48 KB |
| `MegaPattern` - three alternatives in one pattern | 918.4 ns / 4.3 KB | 8.676 μs / 5.07 KB |
| `GreedyVariables` - `<Path*>/<Filename>.<Extension>` | 276.7 ns / 1.31 KB | 1.952 μs / 1.54 KB |

The modern implementation is about 7x faster on single and greedy patterns and about 9.4x faster on mega patterns, while the allocated memory is almost the same in both implementations.

## Contributing

Any contributions are welcome - please read [CONTRIBUTING.md](CONTRIBUTING.md) first. The list of changes is kept in [CHANGELOG.md](CHANGELOG.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
