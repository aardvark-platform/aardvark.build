#! /bin/sh
dotnet build Aardvark.Build/Aardvark.Build.fsproj -c Debug
dotnet pack Aardvark.Build/Aardvark.Build.fsproj -c Release -o bin/pack