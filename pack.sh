#! /bin/sh
source ./build.sh
dotnet pack ./src/aardpack/aardpack.fsproj -c Release -o bin/pack

dotnet publish ./src/Aardvark.Build.Tool/Aardvark.Build.Tool.fsproj -c Release -o bin/publish/standalone-tool

version=`./bin/Release/net8.0/aardpack.exe --parse-only`
dotnet paket pack --template ./src/Aardvark.Build/paket.template --version $version bin/pack
