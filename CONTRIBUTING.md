# Contributing

Thanks for taking the time to contribute to UFP!

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer (the version is pinned in [`global.json`](global.json))
- [Node.js](https://nodejs.org/) 20 or newer - only needed for the JavaScript port; it uses the standard library only
- [Python 3.6](https://www.python.org/downloads/) or newer - only needed for the Python port; it uses the standard library only
- [PHP 8.0](https://www.php.net/downloads) or newer - only needed for the PHP port; it uses the standard library only

## Workflow

1. Fork the repository and clone your fork.
2. Create a feature branch: `git checkout -b feature/my-change`.
3. Make the change.
4. Make sure the build and the tests pass:

   ```bash
   dotnet build UFP.slnx --configuration Release
   dotnet test UFP.slnx --configuration Release

   # Python port (Python 3.6+, standard library only)
   py -m unittest discover -s tests/UFP.Python.Tests -v

   # JavaScript port (Node.js 20+, standard library only)
   node --test tests/UFP.JavaScript.Tests/ufp.test.js

   # PHP port (PHP 8.0+, standard library only)
   php tests/UFP.PHP.Tests/ufp.test.php
   ```

5. Commit the change and open a pull request against `main`.

## Code Style

- Follow the rules from [`.editorconfig`](.editorconfig).
- Keep the two parser implementations - `src/UFP.NET/UfpParser.cs` (`net10.0`) and `src/UFP.NET.Standard/UfpParser.cs` (`netstandard2.0`) - in sync.
- Ports in other languages live in `src/` (for example `src/UFP.Python/ufp.py`, `src/UFP.JavaScript/ufp.js` and `src/UFP.PHP/ufp.php`), have their tests in `tests/` (for example `tests/UFP.Python.Tests`, `tests/UFP.JavaScript.Tests` and `tests/UFP.PHP.Tests`) and must pass the conformance suite: `py -m unittest discover -s tests/UFP.Python.Tests -v`, `node --test tests/UFP.JavaScript.Tests/ufp.test.js` and `php tests/UFP.PHP.Tests/ufp.test.php`.
- Every behavior change (new syntax, greedy variables, mega patterns, edge cases) must be covered by a test in `tests/UFP.NET.Tests`.
- Keep [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) and its [Russian translation](docs/SPECIFICATION.ru.md) in sync with the parser, and add a vector to [`spec/ufp-1.2.test-vectors.json`](spec/ufp-1.2.test-vectors.json) for every new behavior - the conformance suite is what other implementations verify themselves against.
- Update [`CHANGELOG.md`](CHANGELOG.md) for every parser behavior change ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format). Only the parser itself is listed there - tests, CI and repository restructuring are not.

## Reporting Issues

Use [GitHub Issues](https://github.com/Jagailo/User-Friendly-Pattern-Format/issues). For parser problems include the pattern, the input string and the expected and actual parsing results.
