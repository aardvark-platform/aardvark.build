@echo off
dotnet tool restore
dotnet paket restore
dotnet build Aardvark.Build\Aardvark.Build.fsproj -c Release
dotnet build Aardvark.Build\Aardvark.Build.fsproj -c Debug
dotnet pack aardpack\aardpack.fsproj -c Release -o bin\pack
REM did not work :/ never used paket pack correctly...
REM dotnet pack Aardvark.Build\Aardvark.Build.fsproj -c Release -o bin\pack
dotnet run --project getversion/getversion.fsproj
for /f "delims=" %%x in (.version) do set VERSION=%%x
echo "VERSION: %VERSION%"
dotnet paket pack bin/pack --version %VERSION% --exclude Hans

REM todo .sh version
REM #!/bin/sh
REM dotnet run --project getversion/getversion.fsproj
REM version=`cat .version`
REM echo "VERSION: $version"
