## Native Dependencies

Aardvark embeds native dependencies into the DLLs needing them via EmbeddedResource. These dependencies are extracted to a special directory in `LocalApplicationData` during `Aardvark.Init()` s.t. they can be loaded at runtime.

In order to add native dependencies to your DLL you just need to create a directory-structure in your repository-root like `<root>/libs/Native/<ManagedDllName>/<Platform>/<Architecture>/` that contains the desired files with these possible values for `<Platform>` and `<Architecture>`:

| `<Platform>`  |
| ------------- |
| `mac/`        |
| `linux/`      |
| `windows/`    |

| `<Architecture>`  |
| ----------------- |
| `AMD64`           |
| `x86`             |
| `ARM64`           |

Please note that the `<ManagedDllName>` needs to be the `AssemblyName` of your project (excluding the `.dll` extension). For example `MyLib.dll` needs a subfolder `MyLib`. Also note that all strings are **case-sensitive**.

Currently `Aardvark.Build` may run into problems when your `AssemblyName` is not the same as the project-file-name so for the moment we recommend sticking to default `AssemblyName`s.