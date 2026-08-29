# Contributing to DXLog QSO Exporter

## Getting Started & Development Setup

### Prerequisites
- **Operating System:** Windows 10 or Windows 11
- **.NET SDK:** .NET SDK 10.0+ (as defined in `global.json`)
- **IDE:** Visual Studio 2022 / 2026 or VS Code with C# Dev Kit

### Building and Running Tests

From PowerShell:

```powershell
# Restore packages
dotnet restore .\DxLogQsoExporter.sln -p:Platform=x64

# Format verification
dotnet format .\DxLogQsoExporter.sln --verify-no-changes --no-restore

# Build Release x64
dotnet build .\DxLogQsoExporter.sln --configuration Release -p:Platform=x64 --no-restore

# Run unit tests
dotnet test .\DxLogQsoExporter.sln --configuration Release -p:Platform=x64 --no-build --no-restore
```

## Synthetic Test Fixture Policy

> [!IMPORTANT]
> **Never commit real station `.dxn` databases or private audio recordings to the repository.**
> 
> All unit tests must construct synthetic in-memory or temporary fixtures using the provided test builders:
> - `SyntheticDxn` (`tests/DxLogQsoExporter.Tests/Builders/SyntheticDxn.cs`) for SQLite database schemas and QSO records.
> - `SyntheticMp3` (`tests/DxLogQsoExporter.Tests/Builders/SyntheticMp3.cs`) for valid MPEG-1 Layer III audio frames.
> - `TemporaryWorkspace` for managing and cleaning up temporary test directories.

## Code Style & Craftsmanship

- **C# Best Practices:** Code must adhere to the rules defined in `.editorconfig`.
- **Formatting:** Run `dotnet format .\DxLogQsoExporter.sln` before submitting pull requests.
- **Warnings as Errors:** Release builds and CI enforce zero compiler warnings and zero code style diagnostics.
- **Nullability:** Nullable reference types are enabled across all projects (`<Nullable>enable</Nullable>`). Ensure proper nullability annotations on all APIs.
- **Disposal:** Always properly dispose unmanaged resources, streams, and SQLite connections using `using` statements.

## Submitting Pull Requests

1. Fork the repository and create a descriptive branch name.
2. Ensure all existing tests pass and add unit tests covering new features or bug fixes.
3. Verify that `dotnet format .\DxLogQsoExporter.sln --verify-no-changes` passes cleanly.
4. Submit a Pull Request with a clear summary of changes and rationale.