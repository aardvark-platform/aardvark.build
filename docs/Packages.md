## Packaging

Aardvark.Build expects your project to use `paket` for managing dependencies and several parts of it may not work correctly without it.

If you want to create a package for a project you will need to place a `paket.template` file next to the `.(c|f)sproj` with standard paket behaviour.

We *replace* the standard behaviour of `dotnet pack` with `paket pack` using appropriate version/release-notes.