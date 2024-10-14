# Aardvark.Build

![Publish](https://github.com/aardvark-platform/aardvark.build/workflows/Publish/badge.svg)
[![NuGet](https://badgen.net/nuget/v/Aardvark.Build)](https://www.nuget.org/packages/Aardvark.Build/)
[![NuGet](https://badgen.net/nuget/dt/Aardvark.Build)](https://www.nuget.org/packages/Aardvark.Build/)

Build tools for the Aardvark platform.

## Basic Usage
Aardvark.Build simplifies the build process for projects using the Aardvark platform and [Paket](https://github.com/fsprojects/Paket). It consists of various MSBuild tasks for the build process itself and the `aardpack` dotnet tool for creating packages. The features of Aardvark.Build include:
* Parsing release notes with [FAKE](https://github.com/fsprojects/FAKE)
* Injecting native dependencies as assembly resource
* Providing local package overrides
* Automatic patching of assembly version (e.g. 1.2.3 ~> 1.2.0)
* Creating packages, creating tags, github releases and uploading packages via `aardpack`

Reference the `Aardvark.Build` package in your project to install the custom MSBuild tasks.

## Configuration
Aardvark.Build should work out of the box without further configuration. Nevertheless, there are some MSBuild properties that can be modified for special scenarios and troubleshooting:

* `AardvarkBuildReleaseNotes`: Path to the release notes file.
  
* `AardvarkBuildNativeDependencies`: Path to the folder containing the native dependencies of the project. The contents of the folders will be added to the `native.zip` archive that is embedded into the assembly.

* `AardvarkBuildLocalSources`: Path to the local sources file.

* `AardvarkBuildForceNativeRepack`: If set to `True`, the up-to-date check for packing native dependencies is skipped. As a consequence, native dependencies will be rezipped even if nothing changed.
  
* `AardvarkBuildDisableLocalSources`: If set to `True`, local sources are not built and injected.

* `AardvarkBuildToolAssembly`: Path to the `Aardvark.Build.dll` assembly. By default `..\standalone-tool\Aardvark.Build.dll` from the location of the `*.targets` file is used.

* `AardvarkBuildVerbosity`: Determines how much information is printed to the console.
  * `Minimal` - errors only
  * `Normal` - warnings and errors (default)
  * `Detailed` - warnings, errors, and informational messages
  * `Debug` - everything including debug messages

## Release Notes & Versioning

Aardvark.Build expects your project to have a file called `RELEASE_NOTES.md` (case insensitive and also allowing some variations of the name) in the repository-root directory. This file will be used to get `AssemblyVersion` and also `PackageVersion` during build/pack. The syntax for the file sticks to the one defined by `Fake.Core.ReleaseNotes` and may for example look like:

```markdown
Lines above the first version are ignored an can be used to record preliminary release notes.
- Pending change
- Another pending change

### 0.1.2.3
- fixed problem 1
- added feature XY

### 0.1.2.3-alpha01
- some changes
```

The latest (highest) version in this file will be used for creating packages via `paket pack` and `dotnet pack` and also determines the `AssemblyVersion` for resulting DLLs. Since `AssemblyVersion` cannot handle *prerelease-suffixes* and in order to be fully compliant to **SemVer** the `AssemblyVersion` will only include the first two digits of the package-version (this way SemVer-compatible dlls can be interchanged at runtime without dotnet complaining). For example a PackageVersion `1.2.3.4-alpha01` will result in AssemblyVersion `1.2.0.0`.

The build process also includes the full PackageVersion in the resulting DLLs as `AssemblyInformationalVersion` which can easily be queried during runtime (for example for logging, etc.)

## Native Dependencies

Aardvark embeds native dependencies into the DLLs. These dependencies are extracted to a special directory in `LocalApplicationData` during `Aardvark.Init()` s.t. they can be loaded at runtime. In order to add native dependencies to your DLL you just need to create a directory-structure in your repository-root like `<root>/libs/Native/<ManagedDllName>/<Platform>/<Architecture>/` that contains the desired files with these possible values for `<Platform>` and `<Architecture>`:

| `<Platform>`  |
| ------------- |
| `mac`        |
| `linux`      |
| `windows`    |

| `<Architecture>`  |
| ----------------- |
| `AMD64`           |
| `x86`             |
| `ARM64`           |

Please note that the `<ManagedDllName>` needs to be the `AssemblyName` of your project (excluding the `.dll` extension). For example `MyLib.dll` needs a subfolder `MyLib`. Also note that all strings are **case-sensitive**.

Currently `Aardvark.Build` may run into problems when your `AssemblyName` is not the same as the project-file-name so for the moment we recommend sticking to default `AssemblyName`s.

## Local Source Packages

Aardvark.Build allows you to use locally-built packages that **override** nuget-dependencies. This is especially useful for testing things across multiple repositories and debugging.

For adding an external repository you need to create a file called `local.sources` in your repository-root that holds the source-project's path and build/pack commands. A little example:

```
/home/dev/myrepo
    dotnet build -c Debug
    dotnet paket pack --build-config Debug --version {VERSION} {OUTPUT}

../otherrepo
    dotnet tool restore
    dotnet build -c Debug MyProj/MyProj.fsproj
    dotnet paket pack --build-config Debug --version {VERSION} {OUTPUT}
```

Non-indented strings are interpreted as paths to the repository (absolute or relative to the `.sources` file) and all indented lines following are commands that create packages in the spliced folder-path `{OUTPUT}` provided by Aardvark.Build.

All packages created this way will override their nuget/paket counterparts during build and startup. However we experienced some problems with auto-completion for newly added functions, etc.

Since building projects can be costly we reuse the packages whenever the source is unchanged. For this caching to work best it is strongly recommended that you use git-repositories as sources. It will work on non-git directories but might trigger rebuilds too often.

*Transitive* `local.sources` overrides across repositories are also supported.

## Creating Packages with `aardpack`
The dotnet tool `aardpack` is useful for creating packages with Github CI workflows.

`aardpack [--version] [--parse-only] [--configuration <config>] [--release-notes <file>] [--output <output directory>] [--no-build] [--skip-build] [--no-release] [--no-tag] [--dry-run] [--per-project] <file>...`

The tool builds the given solution and project files, creates packages, Git tags and Github releases (tags and releases are only created if a Github token is defined).

Available options:
* `--version`: Print the version of `aardpack` and exit.
* `--parse-only`: Parse the release notes, print the latest version and exit. Prints `0.0.0.0` on failure.
* `--configuration <config>`: The configuration to use for building and packing. Default is Release.
* `--release-notes <file>`: Path to the release notes file to use for all targets. If omitted, the release notes file is located automatically.
* `--output <output directory>`: Output directory for *.nupkg files. Defaults to `bin/pack`.
* `--no-build`: Skip the build and pack steps. The files in arguments will be added to the Github release directly.
* `--skip-build`: Skip the build step but create packages normally in contrast to `--no-build`.
* `--no-release`: Do not create a Github release even if a Github token is defined.
* `--no-tag`: Do not create a Git tag. Note that this option only has an effect in conjunction with `--no-release` as a tag will be created for each release.
* `--dry-run`: Only simulate creating tags and releases (works without Github token).
* `--per-project`: Create a tag and release for each project and package. E.g. Aardvark.Base.csproj results in a tag `aardvark.base/1.2.3.4` and release titled `Aardvark.Base - 1.2.3.4`.