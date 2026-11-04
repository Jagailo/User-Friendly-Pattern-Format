# Contributing

Thanks for taking the time to contribute to UFP!

## Where things live

- `docs/SPECIFICATION.md` and its [Russian translation](docs/SPECIFICATION.ru.md) - the normative description of the format (the English text is normative, the Russian text mirrors it).
- `spec/ufp-1.2.test-vectors.json` - the conformance suite that every implementation must pass.
- `src/` - one dependency-free parser per language, `tests/` - their tests, `benchmarks/` - benchmark projects.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer (the version is pinned in [`global.json`](global.json)) - needed for the .NET parsers, tests and benchmarks.
- [Node.js](https://nodejs.org/) 20 or newer - only for the JavaScript port.
- [Python 3.6](https://www.python.org/downloads/) or newer - only for the Python port.
- [PHP 8.0](https://www.php.net/downloads) or newer - only for the PHP port.

Every parser uses only the standard library of its language: no NuGet, npm, pip or Composer packages.

## Workflow

1. Fork the repository and clone your fork.
2. Create a feature branch: `git checkout -b feature/my-change`.
3. Make the change.
4. Build and test the .NET solution:

   ```bash
   dotnet build UFP.slnx --configuration Release
   dotnet test UFP.slnx --configuration Release
   ```

5. If you changed an implementation other than the .NET ones, run its tests and its conformance runner. The exact commands are in the [CI workflow](.github/workflows/build.yml) and in [`spec/README.md`](spec/README.md).
6. Commit the change and open a pull request against `main`.

## What to change together

A behaviour change is not complete until all of these agree:

- **Specification.** Update [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) and its [Russian translation](docs/SPECIFICATION.ru.md). The English text is normative; keep the translation in sync.
- **Conformance suite.** Add a vector for the new behaviour to [`spec/ufp-1.2.test-vectors.json`](spec/ufp-1.2.test-vectors.json) with a stable kebab-case `id` and a `description` that states the rule.
- **Parsers.** Implement the behaviour in every parser you touch and keep the two .NET parsers (`net10.0` and `netstandard2.0`) in sync; the standard one stays C# 7.3-compatible.
- **Tests.** Cover the behaviour with a unit test and make sure the conformance runner passes for every parser you changed.
- **Changelog.** Add an entry to [`CHANGELOG.md`](CHANGELOG.md). It lists only the format and the parsing behaviour - not tests, CI, benchmarks or new ports.

If you only fix a typo or improve the documentation, changing that file alone is enough.

## Code style

- Match the style of the file you are editing; there is no shared linter or formatter configuration.
- Keep each parser a single copy-pasteable file with no dependencies outside the standard library.
- Do not add features that the format does not specify. If a new syntax or rule is needed, discuss it in an issue first: the format is intentionally kept small.

## Reporting issues

Use [GitHub Issues](https://github.com/Jagailo/User-Friendly-Pattern-Format/issues). For parser problems include the pattern, the input text and the expected and actual results.

## License

By contributing you agree that your contribution is published under the [MIT License](LICENSE).
