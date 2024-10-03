#! /bin/sh
sh ./build.sh
dotnet pack ./src/aardpack/aardpack.fsproj -c Release -o bin/pack

dotnet publish ./src/Aardvark.Build/Aardvark.Build.fsproj -c Release -o bin/publish/standalone-tool

version=`./bin/Release/aardpack/net8.0/aardpack --parse-only`
dotnet paket pack --template ./src/Aardvark.Build/paket.template --version $version bin/pack
