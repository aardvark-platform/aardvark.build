## ReleaseNotes & Versions

Aardvark.Build expects your project to have a file called `RELEASE_NOTES.md` (case insensitive and also allowing some variations of the name) in the repository-root directory. This file will be used to get `AssemblyVersion` and also `PackageVersion` during build/pack. The syntax for the file sticks to the one defined by `Fake.Core.ReleaseNotes` and may for example look like:

```markdown
### 0.1.2.3
* fixed problem 1
* added feature XY

### 0.1.2.3-alpha01
* some changes
```

The latest (highest) version in this file will be used for creating packages via `dotnet pack` and also determines the `AssemblyVersion` for resulting DLLs. Since `AssemblyVersion` cannot handle *prerelease-suffixes* and in order to be fully compliant to **SemVer** the `AssemblyVersion` will only include the first two digits of the package-version (this way SemVer-compatible dlls can be interchanged at runtime without dotnet complaining). For example a PackageVersion `1.2.3.4-alpha01` will result in AssemblyVersion `1.2.0.0`.

The build process also includes the full PackageVersion in the resulting DLLs as `AssemblyInformationalVersion` which can easily be queried during runtime (for example for logging, etc.)
