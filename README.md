# Aardvark.Build

[![Publish](https://github.com/aardvark-platform/aardvark.build/workflows/Publish/badge.svg)](https://github.com/aardvark-platform/aardvark.build/actions/workflows/pack.yml)
[![Nuget](https://img.shields.io/nuget/vpre/aardvark.build)](https://www.nuget.org/packages/aardvark.build/)
[![Downloads](https://img.shields.io/nuget/dt/aardvark.build)](https://www.nuget.org/packages/aardvark.build/)

MSBuild tasks for the Aardvark platform that automate versioning, native dependency management, and cross-repository development workflows.

## Why Aardvark.Build?

If you're developing .NET libraries that need consistent versioning from release notes, ship with platform-specific native dependencies, or require testing changes across multiple repositories, Aardvark.Build provides build-time automation for these scenarios without adding runtime dependencies to your packages.

## Installation

Add `Aardvark.Build` to your `paket.references`. It's marked as a development dependency, meaning:
- Only active during compilation, not at runtime
- Won't appear as a dependency in your published NuGet packages
- Consumers of your library don't need Aardvark.Build installed

## Features

### 1. Automatic Versioning from Release Notes

**Problem:** Maintaining version numbers in multiple places leads to inconsistencies.

**Solution:** Parse a single `RELEASE_NOTES.md` file to set all version attributes during build.

Create `RELEASE_NOTES.md` in your repository root:
```markdown
Pending changes (above first version):
- Work in progress items

### 1.2.3.4-alpha01
- Added new feature X

### 1.2.3.3
- Fixed critical bug Y
```

The latest version automatically becomes:
- **PackageVersion**: `1.2.3.4-alpha01` (full version for NuGet)
- **AssemblyVersion**: `1.2.0.0` (SemVer-compatible for runtime binding)
- **AssemblyInformationalVersion**: `1.2.3.4-alpha01` (queryable full version)

Query the embedded version at runtime:
```csharp
// C#
using System.Reflection;
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
```
```fsharp
// F#
open System.Reflection
let version = Assembly.GetExecutingAssembly().GetCustomAttributes(true)
              |> Array.tryPick (function :? AssemblyInformationalVersionAttribute as a -> Some a.InformationalVersion | _ -> None)
```

### 2. Native Dependency Embedding

**Problem:** Distributing platform-specific native libraries with managed assemblies is complex and error-prone.

**Solution:** Automatically embed native dependencies as resources that extract at runtime.

Structure your native dependencies:
```
libs/Native/<AssemblyName>/<Platform>/<Architecture>/your-native-lib.dll
```
Where:
- `<AssemblyName>`: Your project name (without .dll)
- `<Platform>`: `windows`, `linux`, or `mac`
- `<Architecture>`: `AMD64`, `x86`, or `ARM64`

During build, these files are:
1. Packed into a `native.zip` resource
2. Embedded in your assembly
3. Extracted to `LocalApplicationData` when `Aardvark.Init()` is called
4. Automatically loaded at runtime

**Note:** All paths are case-sensitive. AssemblyName should match your project file name.

### 3. Local Source Override

**Problem:** Testing changes across multiple interdependent repositories requires publishing packages or complex build scripts.

**Solution:** Override NuGet dependencies with locally-built packages during development.

Create `local.sources` in your repository root:
```
/path/to/aardvark.base
    dotnet build -c Debug
    dotnet paket pack --build-config Debug --version {VERSION} {OUTPUT}

../relative/other-repo
    dotnet build -c Debug MyProj/MyProj.fsproj
    dotnet paket pack --build-config Debug --version {VERSION} {OUTPUT}
```

This enables:
- **Cross-repository debugging**: Test changes without publishing packages
- **Rapid iteration**: Changes in dependencies immediately available
- **Git-aware caching**: Rebuilds only when source changes (works best with git repos)
- **Transitive overrides**: Dependencies can have their own `local.sources`

**Note:** IntelliSense might not reflect new APIs until restart.

## Configuration

All features work out-of-the-box. For special cases, customize via MSBuild properties:

| Property | Description | Default |
|----------|-------------|---------|
| `AardvarkBuildReleaseNotes` | Release notes file path | Auto-detected |
| `AardvarkBuildNativeDependencies` | Native dependencies folder | `libs/Native/<AssemblyName>` |
| `AardvarkBuildLocalSources` | Local sources file path | `local.sources` |
| `AardvarkBuildForceNativeRepack` | Force native dependency repacking | `False` |
| `AardvarkBuildDisableLocalSources` | Disable local source overrides | `False` |
| `AardvarkBuildVerbosity` | Output verbosity | `Normal` |

Verbosity levels: `Minimal` (errors) → `Normal` (warnings) → `Detailed` (info) → `Debug` (all)

## aardpack CLI Tool

For CI/CD pipelines, `aardpack` automates building, packaging, and releasing.

### Installation

```bash
dotnet tool install -g aardpack
```

Or add to `.config/dotnet-tools.json` for repository-local tools:
```bash
dotnet new tool-manifest  # if not already present
dotnet tool install aardpack
dotnet tool restore       # for other developers
```

### Usage

```
aardpack [options] <solution/project files>
```

Options:
| Option | Description |
|--------|-------------|
| `--version` | Show version and exit. |
| `--parse-only` | Extract version from release notes (returns `0.0.0.0` on failure). |
| `--configuration <config>` | Build configuration (default: Release). |
| `--release-notes <file>` | Path to the release notes file to use for all targets. If omitted, the release notes file is located automatically. |
| `--output <dir>` | Package output directory (default: `bin/pack`). |
| `--no-build` | Skip the build and pack steps. The files in arguments will be added to the Github release directly. |
| `--skip-build` | Skip the build step but create packages normally in contrast to `--no-build`. |
| `--no-release` | Skip GitHub release creation. |
| `--no-tag` | Do not create a Git tag. Note that this option only has an effect in conjunction with `--no-release` as a tag will be created for each release. |
| `--dry-run` | Simulate operations without executing. |
| `--per-project` | Create a tag and release for each project and package. E.g., Aardvark.Base.csproj results in a tag `aardvark.base/1.2.3.4` and release titled `Aardvark.Base - 1.2.3.4`. |

### GitHub Actions Integration

Typical workflow for automated releases on master/main branch:

```yaml
name: Publish
on:
  push:
    branches: [master, main]
    paths:
      - RELEASE_NOTES.md
      - '**.fs'
      - '**.fsproj'

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore tools
        run: dotnet tool restore
      
      - name: Build, Pack & Release
        run: dotnet aardpack src/MySolution.sln
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      
      - name: Push to NuGet
        run: dotnet nuget push "bin/pack/*.nupkg" -s https://api.nuget.org/v3/index.json -k ${{ secrets.NUGET_KEY }} --skip-duplicate
```

With a GitHub token present, `aardpack` will:
1. Parse version from RELEASE_NOTES.md
2. Build all projects
3. Create NuGet packages
4. Create git tag (e.g., `v1.2.3.4`)
5. Create GitHub release with packages attached

## Requirements

- [Paket](https://github.com/fsprojects/Paket) package manager
- [FAKE](https://github.com/fsprojects/FAKE) (used internally for release notes parsing)
- .NET SDK
