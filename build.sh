#! /bin/sh
dotnet tool restore
dotnet paket restore
# Build Debug first, which is used for the Release build-
dotnet build -c Debug src/Aardvark.Build/Aardvark.Build.fsproj
dotnet build -c Release src/Aardvark.Build/Aardvark.Build.fsproj
dotnet build -c Release src/Aardvark.Build.sln