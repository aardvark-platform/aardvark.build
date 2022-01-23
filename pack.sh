#! /bin/sh
dotnet build Aardvark.Build/Aardvark.Build.fsproj -c Release
dotnet pack Aardvark.Build/Aardvark.Build.fsproj -c Release -o bin/pack