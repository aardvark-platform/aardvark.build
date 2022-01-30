@echo off
dotnet build Aardvark.Build\Aardvark.Build.fsproj -c Debug
dotnet pack Aardvark.Build\Aardvark.Build.fsproj -c Release -o bin\pack
dotnet pack aardpack\aardpack.fsproj -c Release -o bin\pack