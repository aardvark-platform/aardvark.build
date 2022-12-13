#! /bin/sh
dotnet tool restore
dotnet paket restore
dotnet build Aardvark.Build\Aardvark.Build.fsproj -c Release
dotnet build Aardvark.Build\Aardvark.Build.fsproj -c Debug
dotnet pack aardpack/aardpack.fsproj -c Release -o bin/pack
dotnet run --project getversion/getversion.fsproj
version=`cat .version`
echo "VERSION: $version"
dotnet paket pack bin/pack --version $version --exclude Hans
