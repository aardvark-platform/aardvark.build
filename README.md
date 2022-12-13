## The goal of aardvark.build

Before aardvark.build we had dozens of projects with custom build scripts based on fake (and aardvark.fake - some additional commonly used utilities).
We often had problems maintaining those.
In order to remove as much code as possible, this msbuild task & assembly provides features such as:
 * parsing release notes
 * injecting native dependencies as assembly resource
 * providing local package overrides
 * nupgk packing using paket instead of vanilla dotnet
 * automatic patching of assembly version (e.g. 1.2.3 ~> 1.2.0)
 * creating packages, creating tags, github releases and uploading packages
 
 
There are two components:
 * the msbuild targets file - Aardvark.Build.targets and the accompanying assembly aardvark.build: works on an assembly level and prepares the assembly by, injecting native libs, patching the assembly version etc etc.
 * the aardpack dotnet tool: builds a package and packs it into a .nupgk using paket, creates tags and so on.

## For Users

Documentation on usage is in ./docs.
 
## For maintainers

Some things to remember when working on the project or when fixing bugs:

 * Aardvark.Build.dll needs to be robust. Often it runs within msbuild executions, thus the environment it runs in cannot be specified. Unfortunately, we cannot use isolated dotnet environments to make assembly loading robust in this scenario (msbuild uses net48). This is particularly problematic when using third party libs in the lib referenced by the targets file. To be robust to different assembly versions (often fsharp.core) being already loaded when the tool is invoked we register `AssemblyResolveEventHandle`s in ./Aardvark.Build/Utilities.fs. As long as there are no isolated environments we cannot reference utility libraries in the build task since already loaded libraries could prevent the runtime from loading them leading to missing method exceptions. For example for parsing release notes we copied the FAKE code over in order to prevent dependency problems.
 * aardvark.build.fsproj references ```  <Import Project="..\bin\Debug\netstandard2.0\Aardvark.Build.targets" Condition="'$(Configuration)' == 'Release' AND Exists('..\bin\Debug\netstandard2.0\Aardvark.Build.targets')" />``` itself for handling its packaing. Multiple builds in build.sh|build.cmd make this possible.
 * Always make sure to test everything using the ./Test project which references the target file compiled using pack.sh|pack.cmd
 
 
## Credits

We use portions of FAKE for parsing release notes (see credits.md).