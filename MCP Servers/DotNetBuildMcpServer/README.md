# DotNet Build MCP Server

A Model Context Protocol (MCP) server for .NET build operations.

## Features

- Build, clean, restore .NET projects
- Run unit tests
- Manage NuGet packages
- Analyze C# code syntax
- Get build errors and warnings

## Quick Start

```bash
dotnet build
dotnet run
```

## Test

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize"}' | dotnet run
```
