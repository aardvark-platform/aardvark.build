- Fixed local sources cache

### 2.0.3
- Update FAKE packages (fixes missing method exception due to Octokit breaking change)

### 2.0.2
- [aardpack] Added --output argument
- [aardpack] Fixed issue with project file targets

### 2.0.1
- Added AardvarkBuildReleaseNotes property
- Added AardvarkBuildNativeDependencies property
- Added AardvarkBuildLocalSources property
- Added AardvarkBuildDisableLocalSources property
- Removed AardvarkBuildRepositoryRoot property
- [aardpack] Added --configuration argument and remvoe --debug switch
- [aardpack] Added --release-notes argument

### 2.0.0
- Reworked as standalone tool
- Added support for preliminary release notes
- Added support for relative paths in local.sources files
- [aardpack] Added debug switch for debug configuration
- [aardpack] Fixed support for empty target

### 1.0.25
- Make locating of release notes more flexible to support repositories with separate release notes for each project (previous implementation lead to problem with native dependencies).

### 1.0.24
- [aardpack] Handle solution files properly when --per-project is specified
- [aardpack] Added --skip-build option

### 1.0.23
- [aardpack] Added --version option
- [aardpack] Added --dry-run option for testing
- [aardpack] Added --per-project option to create tags and releases for individual projects. E.g. Aardvark.Base.csproj results in a tag "aardvark.base/1.2.3.4" and a release titled "Aardvark.Base - 1.2.3.4"

### 1.0.22
* updated paket (8.0.3)

### 1.0.21
* updated paket (7.2.0)

### 1.0.20
* Ignore readme, license and build scripts for determining root

### 1.0.19
* Updated FAKE and disable bin logger in aardpack to resolve issue with MSBuild

### 1.0.18
* ReleaseNotesTask finally works in newest visual studio 2022 preview 

### 1.0.17
* version bump

### 1.0.16
* version bump

### 1.0.15
* ReleaseNotesTask is now more robust and debuggable.

### 1.0.14
* aardpack: added --parseonly option only printing the version to stdout

### 1.0.13
* aardpack: added --nobuild option (will pack existing artifacts supplied as paths)

### 1.0.12
* fixed AssemblyResolve problem

### 1.0.11
* improved local.sources support (not touching anything when inactive)

### 1.0.10
* fixed parsing of HTTPS remotes

### 1.0.9
* fixed Aardvark.Build dependencies

### 1.0.8
* fixed release creation

### 1.0.7
* improved release creation

### 1.0.6
* pushing tag

### 1.0.5
* fixed output for pack (correctly reporting all packages)

### 1.0.4
* fixed build-package

### 1.0.3
* tag only created on CI (when GITHUB_TOKEN is set)

### 1.0.2
* added project/sln option

### 1.0.1
* aardpack

### 1.0.0
* initial version

### 1.0.0-prerelease0011 
* fixes