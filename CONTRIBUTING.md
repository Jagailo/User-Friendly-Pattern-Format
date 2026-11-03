# Contributing

Thanks for taking the time to contribute to UFP!

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer (the version is pinned in [`global.json`](global.json))

## Workflow

1. Fork the repository and clone your fork.
2. Create a feature branch: `git checkout -b feature/my-change`.
3. Make the change.
4. Make sure the build and the tests pass:

   ```bash
   dotnet build UFP.slnx --configuration Release
   dotnet test UFP.slnx --configuration Release
   ```

5. Commit the change and open a pull request against `main`.

## Code Style

- Follow the rules from [`.editorconfig`](.editorconfig).
- Keep the two parser implementations - `src/UFP.NET/UfpParser.cs` (`net10.0`) and `src/UFP.NET.Standard/UfpParser.cs` (`netstandard2.0`) - in sync.
- Every behavior change (new syntax, greedy variables, mega patterns, edge cases) must be covered by a test in `tests/UFP.NET.Tests`.
- Update [`CHANGELOG.md`](CHANGELOG.md) for every parser behavior change ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format). Only the parser itself is listed there - tests, CI and repository restructuring are not.

## Reporting Issues

Use [GitHub Issues](https://github.com/Jagailo/User-Friendly-Pattern-Format/issues). For parser problems include the pattern, the input string and the expected and actual parsing results.
