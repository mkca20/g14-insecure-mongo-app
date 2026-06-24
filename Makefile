# Makefile for a C# .NET project

PROJECT_NAME := InsecureMongoApp
CONFIGURATION := Debug
FRAMEWORK := net8.0

# Default target
.PHONY: all
all: build

# Build the project
.PHONY: build
build:
	dotnet build --configuration $(CONFIGURATION)

# Run the project
.PHONY: run
run:
	dotnet run --configuration $(CONFIGURATION)

# Restore dependencies
.PHONY: restore
restore:
	dotnet restore

# Test the project
.PHONY: test
test:
	dotnet test

# Publish for deployment
.PHONY: publish
publish:
	dotnet publish --configuration $(CONFIGURATION) --framework $(FRAMEWORK) --output ./publish

# Clean build artifacts
.PHONY: clean
clean:
	dotnet clean

# Linting or formatting (optional if dotnet-format is installed)
.PHONY: format
format:
	dotnet format

# Docker build
.PHONY: docker-build
docker-build:
	docker build -t $(PROJECT_NAME):latest .

# Docker run
.PHONY: docker-run
docker-run:
	docker run --rm -p 8000:5000 $(PROJECT_NAME):latest
