.PHONY: build test pack clean restore-tools restore help

# Default target
all: build

# Build the project
build:
ifeq ($(OS),Windows_NT)
	@cmd /c build.cmd
else
	@sh ./build.sh
endif

# Run tests
test: build
	@dotnet test src/Aardvark.Build.sln -c Release --no-build --nologo --logger:"console;verbosity=normal"

# Create packages
pack: build
ifeq ($(OS),Windows_NT)
	@cmd /c pack.cmd
else
	@sh ./pack.sh
endif

# Clean build outputs
clean:
	@echo "Cleaning build outputs..."
	@rm -rf bin/
	@find src -name "bin" -type d -exec rm -rf {} + 2>/dev/null || true
	@find src -name "obj" -type d -exec rm -rf {} + 2>/dev/null || true
	@echo "Clean completed"

# Restore dotnet tools
restore-tools:
	@dotnet tool restore

# Restore paket dependencies
restore: restore-tools
	@dotnet paket restore

# Show help
help:
	@echo "Aardvark.Build Makefile"
	@echo ""
	@echo "Available targets:"
	@echo "  build         Build the project (default)"
	@echo "  test          Run tests"
	@echo "  pack          Create packages"
	@echo "  clean         Clean build outputs"
	@echo "  restore-tools Restore dotnet tools"
	@echo "  restore       Restore all dependencies"
	@echo "  help          Show this help message"
	@echo ""
	@echo "Examples:"
	@echo "  make build    # Build the project"
	@echo "  make test     # Build and run tests"
	@echo "  make pack     # Build and create packages"