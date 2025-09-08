# Contributing to Aardvark.Build

Thank you for your interest in contributing to Aardvark.Build! This document provides guidelines and information for contributors.

## Development Setup

### Prerequisites
- .NET 8.0 SDK (see `global.json` for exact version)
- Git

### Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/aardvark-platform/aardvark.build.git
   cd aardvark.build
   ```

2. Restore tools and dependencies:
   ```bash
   dotnet tool restore
   dotnet paket restore
   ```

3. Build the project:
   ```bash
   # Linux/macOS
   ./build.sh
   
   # Windows
   build.cmd
   ```

4. Run tests:
   ```bash
   dotnet test src/Aardvark.Build.sln -c Release --no-build
   ```

## Development Workflow

### Building
- Use `build.sh` (Linux/macOS) or `build.cmd` (Windows)
- Debug build is required first, then Release build (Debug is used by Release)

### Testing
- Run all tests: `dotnet test src/Aardvark.Build.sln -c Release --no-build`
- Tests are located in `src/Tests/Aardvark.Build.Tests/`
- Integration test projects: `TestApp`, `TestLibA`, `TestLibB`

### Code Style
- This project uses F# with specific formatting rules defined in `.editorconfig`
- Follow existing code patterns and naming conventions
- XML documentation is encouraged for public APIs

### Local Development with External Projects
Use the `local.sources` feature to test changes with other repositories:

1. Create a `local.sources` file in the target repository
2. Add the path to your Aardvark.Build development directory
3. Include build commands for creating packages

Example `local.sources`:
```
/path/to/aardvark.build
    dotnet build -c Debug src/Aardvark.Build/Aardvark.Build.fsproj
    dotnet pack src/aardpack/aardpack.fsproj -c Debug -o {OUTPUT}
```

## Pull Request Process

1. **Fork and Branch**: Create a feature branch from `master`
2. **Make Changes**: Implement your changes following the coding guidelines
3. **Test**: Ensure all tests pass and add new tests for new functionality
4. **Documentation**: Update documentation if needed
5. **Commit**: Use clear, descriptive commit messages
6. **Pull Request**: Submit a PR with a clear description of changes

### PR Requirements
- All tests must pass on all platforms (Linux, macOS, Windows)
- Code follows existing style conventions
- New functionality includes appropriate tests
- Breaking changes are clearly documented

## Project Structure

- `src/Aardvark.Build/`: MSBuild tasks library
  - `Commands/`: Core functionality (ReleaseNotes, NativeDependencies, LocalSources)
  - `Common/`: Shared utilities
  - `Aardvark.Build.targets`: MSBuild integration

- `src/aardpack/`: Command-line packaging tool
- `src/Tests/`: Test projects and integration tests

## Release Process

Releases are automated via GitHub Actions when `RELEASE_NOTES.md` is updated:

1. Update `RELEASE_NOTES.md` with new version and changes
2. Commit and push to `master`
3. GitHub Actions builds, tests, and publishes packages

## Getting Help

- Check existing issues and discussions on GitHub
- Review the README.md and CLAUDE.md for project information
- Look at existing code patterns and tests for examples

## License

By contributing to Aardvark.Build, you agree that your contributions will be licensed under the Apache-2.0 License.