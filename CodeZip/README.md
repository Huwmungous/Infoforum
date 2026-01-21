# CodeZip

Cross-platform tool for creating source-only zip archives, excluding build artifacts and dependencies.

## Quick Start

```powershell
# Build
dotnet build

# Install CLI tool globally
dotnet pack src/CodeZip.Cli -c Release
dotnet tool install -g --add-source src/CodeZip.Cli/bin/Release CodeZip.Cli

# Use it
codeship zip C:\Projects\MyApp
```

## Install Windows Context Menu

Run as Administrator:
```powershell
.\installer\install-context-menu.ps1
```

## Usage

```bash
codeship zip .                    # Zip current directory
codeship zip /path/to/project     # Zip specific project
codeship zip . --dry-run          # Preview without creating
codeship prune                    # Clean old zips
codeship config show              # View configuration
```

## Output

Zips are created in `C:\temp\CodeZipperData` (Windows) or `~/temp/CodeZipperData` (Linux).

Auto-pruned after 2 days. Path copied to clipboard.

## Supported Project Types

- **Delphi** - Excludes `__history`, `Win32`, `*.dcu`, etc.
- **C#/.NET** - Excludes `bin`, `obj`, `packages`, etc.
- **React/Angular/Node** - Excludes `node_modules`, `dist`, `build`, etc.
