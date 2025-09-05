#!/bin/sh
# Test script for Aardvark.Build (Linux/macOS)

echo "Running Aardvark.Build tests..."

# Ensure we have a clean build first
if [ ! -d "bin/Release" ]; then
    echo "Release build not found. Running build first..."
    sh ./build.sh
    if [ $? -ne 0 ]; then
        echo "Build failed. Cannot run tests."
        exit 1
    fi
fi

# Run tests with various options
if [ "$1" = "--verbose" ] || [ "$1" = "-v" ]; then
    echo "Running tests with verbose output..."
    dotnet test src/Aardvark.Build.sln -c Release --no-build --logger:"console;verbosity=detailed"
elif [ "$1" = "--watch" ] || [ "$1" = "-w" ]; then
    echo "Running tests in watch mode..."
    dotnet watch test src/Tests/Aardvark.Build.Tests/Aardvark.Build.Tests.fsproj -c Release
elif [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
    echo "Usage: test.sh [options]"
    echo ""
    echo "Options:"
    echo "  --verbose, -v    Run tests with verbose output"
    echo "  --watch, -w      Run tests in watch mode"
    echo "  --help, -h       Show this help message"
    echo ""
    echo "Examples:"
    echo "  ./test.sh              # Run all tests"
    echo "  ./test.sh --verbose    # Run with detailed output"
    echo "  ./test.sh --watch      # Run in watch mode"
else
    echo "Running tests..."
    dotnet test src/Aardvark.Build.sln -c Release --no-build --nologo --logger:"console;verbosity=normal"
fi

if [ $? -eq 0 ]; then
    echo "All tests passed!"
else
    echo "Some tests failed!"
    exit 1
fi