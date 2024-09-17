@echo off
CALL build.cmd
dotnet pack .\src\aardpack\aardpack.fsproj -c Release -o bin\pack

dotnet publish .\src\Aardvark.Build.Tool\Aardvark.Build.Tool.fsproj -c Release -o bin/publish/standalone-tool

FOR /F "tokens=*" %%g IN ('.\bin\Release\net8.0\aardpack.exe --parse-only') do (SET VERSION=%%g)
dotnet paket pack --template .\src\Aardvark.Build\paket.template --version %VERSION% bin/pack
