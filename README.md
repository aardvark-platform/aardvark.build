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

## Release Notes & Versioning

Aardvark.Build expects your project to have a file called `RELEASE_NOTES.md` (case insensitive and also allowing some variations of the name) in the repository-root directory. This file will be used to get `AssemblyVersion` and also `PackageVersion` during build/pack. The syntax for the file sticks to the one defined by `Fake.Core.ReleaseNotes` and may for example look like:

```markdown
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
    dotnet build
    dotnet pack -o {OUTPUT}

/home/dev/otherrepo
    dotnet tool restore
    dotnet build MyProj/MyProj.fsproj
    dotnet paket pack {OUTPUT}
```

Non-indented strings are interpreted as paths to the repository (absolute or relative to the `.sources` file) and all indented lines following are commands that create packages in the spliced folder-path `{OUTPUT}` provided by Aardvark.Build. 

All packages created this way will override their nuget/paket counterparts during build and startup. However we experienced some problems with auto-completion for newly added functions, etc.

Since building projects can be costly we reuse the packages whenever the source is unchanged. For this caching to work best it is strongly recommended that you use git-repositories as sources. It will work on non-git directories but might trigger rebuilds too often.

*Transitive* `local.sources` overrides across repositories are also supported.