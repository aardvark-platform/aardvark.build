#! /bin/sh
dotnet tool restore
dotnet paket restore
# Build Debug first, which is used for the Release build. Avoids Aardvark.Build.dll being locked by MSBuild.
dotnet build -c Debug src/Aardvark.Build/Aardvark.Build.csproj
dotnet build -c Debug src/Aardvark.Build.Tool/Aardvark.Build.Tool.fsproj
dotnet build -c Release src/Aardvark.Build/Aardvark.Build.csproj
dotnet build -c Release src/Aardvark.Build.Tool/Aardvark.Build.Tool.fsproj
dotnet build -c Release src/Aardvark.Build.sln