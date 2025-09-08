@echo off
REM Test script for Aardvark.Build (Windows)

echo Running Aardvark.Build tests...

REM Ensure we have a clean build first
if not exist "bin\Release" (
    echo Release build not found. Running build first...
    call build.cmd
    if errorlevel 1 (
        echo Build failed. Cannot run tests.
        exit /b 1
    )
)

REM Run tests with various options
if "%1"=="--verbose" goto verbose
if "%1"=="-v" goto verbose
if "%1"=="--watch" goto watch
if "%1"=="-w" goto watch
if "%1"=="--help" goto help
if "%1"=="-h" goto help
goto normal

:verbose
echo Running tests with verbose output...
dotnet test src\Aardvark.Build.sln -c Release --no-build --logger:"console;verbosity=detailed"
goto end

:watch
echo Running tests in watch mode...
dotnet watch test src\Tests\Aardvark.Build.Tests\Aardvark.Build.Tests.fsproj -c Release
goto end

:help
echo Usage: test.cmd [options]
echo.
echo Options:
echo   --verbose, -v    Run tests with verbose output
echo   --watch, -w      Run tests in watch mode
echo   --help, -h       Show this help message
echo.
echo Examples:
echo   test.cmd              # Run all tests
echo   test.cmd --verbose    # Run with detailed output
echo   test.cmd --watch       # Run in watch mode
goto end

:normal
echo Running tests...
dotnet test src\Aardvark.Build.sln -c Release --no-build --nologo --logger:"console;verbosity=normal"

:end
if errorlevel 1 (
    echo Some tests failed!
    exit /b 1
) else (
    echo All tests passed!
)