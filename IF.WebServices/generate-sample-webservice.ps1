#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates the complete SampleWebService project.

.DESCRIPTION
    Creates all directories, C# files, documentation, and run scripts
    for the SampleWebService - a demonstration service that uses SfD.Global
    to fetch configuration and authenticate as a service.

.PARAMETER OutputPath
    The directory where the project will be created. Defaults to current directory.

.EXAMPLE
    .\Generate-SampleWebService.ps1
    
.EXAMPLE
    .\Generate-SampleWebService.ps1 -OutputPath "C:\Projects\Services"
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputPath = "."
)

$ErrorActionPreference = "Stop"

# Resolve to absolute path
$OutputPath = Resolve-Path $OutputPath -ErrorAction SilentlyContinue
if (-not $OutputPath) {
    $OutputPath = $PWD.Path
}

Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host "  SampleWebService Project Generator" -ForegroundColor Cyan
Write-Host "  Demonstrates SfD.Global integration" -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output Path: $OutputPath" -ForegroundColor Yellow
Write-Host ""

# Create directory structure
Write-Host "Creating directory structure..." -ForegroundColor Green

$directories = @(
    "SampleWebService",
    "SampleWebService\Controllers"
)

foreach ($dir in $directories) {
    $fullPath = Join-Path $OutputPath $dir
    if (-not (Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Gray
    }
}

Write-Host ""

# Function to create file with content
function New-ProjectFile {
    param(
        [string]$RelativePath,
        [string]$Content
    )
    
    $fullPath = Join-Path $OutputPath $RelativePath
    $Content | Out-File -FilePath $fullPath -Encoding UTF8 -Force
    Write-Host "  Created: $RelativePath" -ForegroundColor Gray
}

Write-Host "Creating project files..." -ForegroundColor Green

# ============================================================================
# SampleWebService.csproj
# ============================================================================
$csprojContent = @'
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="FirebirdSql.Data.FirebirdClient" Version="10.3.1" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference to your SfD.Global library -->
    <ProjectReference Include="..\SfD.Global\SfD.Global.csproj" />
  </ItemGroup>

</Project>
'@
New-ProjectFile "SampleWebService\SampleWebService.csproj" $csprojContent

# ============================================================================
# Program.cs
# ============================================================================
$programContent = @'
using Microsoft.AspNetCore.Mvc;
using SfD.Global;
using SfD.Global.Auth;
using SfD.Global.Config;
using SfD.Global.Models;

var builder = WebApplication.CreateBuilder(args);

// CRITICAL: Initialize ConfigService before anything else
Console.WriteLine("Setting AppType to Service...");
ConfigService.SetAppType(AppType.Service);

Console.WriteLine("Initializing ConfigService (fetching bootstrap)...");
await ConfigService.InitializeAsync();

Console.WriteLine($"ConfigService initialized successfully:");
Console.WriteLine($"  ClientId: {ConfigService.ClientId}");
Console.WriteLine($"  Realm: {ConfigService.Realm}");
Console.WriteLine($"  OpenIdConfig: {ConfigService.OpenIdConfig}");
Console.WriteLine($"  LoggerService: {ConfigService.LoggerService}");

// Authenticate and get access token
Console.WriteLine("\nAuthenticating service account...");
var accessToken = await ServiceAuthenticator.GetServiceAccessTokenAsync();
Console.WriteLine("Service authenticated successfully");

// Fetch Firebird database configuration from config service
Console.WriteLine("\nFetching Firebird database configuration...");
var firebirdConfig = await ConfigService.GetConfigAsync<FBConnection>("firebirddb", accessToken)
    ?? throw new InvalidOperationException("Failed to fetch Firebird database configuration");

Console.WriteLine($"Firebird config loaded:");
Console.WriteLine($"  Host: {firebirdConfig.Host}");
Console.WriteLine($"  Port: {firebirdConfig.Port}");
Console.WriteLine($"  Database: {firebirdConfig.Database}");
Console.WriteLine($"  UserName: {firebirdConfig.UserName}");
Console.WriteLine($"  Charset: {firebirdConfig.Charset}");

// CRITICAL: Use centralized port management via SfD.Global
int port = PortResolver.GetPort();
Console.WriteLine($"\nUsing port: {port}");

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

// Configure logging with SfD.Global logger
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddSfdLogger(); // This will use the LoggerService from bootstrap

// Register configuration objects for DI
builder.Services.AddSingleton(firebirdConfig);

builder.Services.AddSingleton<IConfigProvider>(new ConfigProvider
{
    ClientId = ConfigService.ClientId,
    OpenIdConfig = ConfigService.OpenIdConfig,
    LoggerService = ConfigService.LoggerService,
    Realm = ConfigService.Realm
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Sample Web Service", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("\n=======================================================");
Console.WriteLine("Sample Web Service Started Successfully");
Console.WriteLine($"Listening on: http://0.0.0.0:{port}");
Console.WriteLine("=======================================================\n");

app.Run();
'@
New-ProjectFile "SampleWebService\Program.cs" $programContent

# ============================================================================
# Controllers/SampleController.cs
# ============================================================================
$controllerContent = @'
using Microsoft.AspNetCore.Mvc;
using SfD.Global.Config;
using SfD.Global.Models;
using FirebirdSql.Data.FirebirdClient;

namespace SampleWebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SampleController : ControllerBase
{
    private readonly ILogger<SampleController> _logger;
    private readonly FBConnection _firebirdConfig;
    private readonly IConfigProvider _configProvider;

    public SampleController(
        ILogger<SampleController> logger,
        FBConnection firebirdConfig,
        IConfigProvider configProvider)
    {
        _logger = logger;
        _firebirdConfig = firebirdConfig;
        _configProvider = configProvider;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        _logger.LogInformation("Health check requested");
        
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            message = "Sample Web Service is running"
        });
    }

    /// <summary>
    /// Get the bootstrap configuration that was loaded on startup
    /// </summary>
    [HttpGet("config/bootstrap")]
    public IActionResult GetBootstrap()
    {
        _logger.LogInformation("Bootstrap configuration requested");

        return Ok(new
        {
            clientId = _configProvider.ClientId,
            realm = _configProvider.Realm,
            authority = _configProvider.OpenIdConfig,
            loggerService = _configProvider.LoggerService,
            message = "Bootstrap configuration loaded from config service on startup"
        });
    }

    /// <summary>
    /// Get the Firebird database configuration
    /// </summary>
    [HttpGet("config/firebird")]
    public IActionResult GetFirebirdConfig()
    {
        _logger.LogInformation("Firebird configuration requested");

        return Ok(new
        {
            host = _firebirdConfig.Host,
            port = _firebirdConfig.Port,
            database = _firebirdConfig.Database,
            username = _firebirdConfig.UserName,
            charset = _firebirdConfig.Charset,
            role = _firebirdConfig.Role,
            connectionString = _firebirdConfig.GetConnectionString(),
            message = "Firebird configuration loaded from config service on startup"
        });
    }

    /// <summary>
    /// Test the Firebird database connection
    /// </summary>
    [HttpGet("database/test")]
    public async Task<IActionResult> TestDatabaseConnection()
    {
        _logger.LogInformation("Testing Firebird database connection");

        try
        {
            var connectionString = _firebirdConfig.GetConnectionString();
            
            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var serverVersion = connection.ServerVersion;
            
            _logger.LogInformation("Successfully connected to Firebird database - Version: {Version}", serverVersion);

            return Ok(new
            {
                status = "connected",
                serverVersion,
                database = _firebirdConfig.Database,
                host = _firebirdConfig.Host,
                message = "Successfully connected to Firebird database"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Firebird database");
            
            return StatusCode(500, new
            {
                status = "error",
                error = ex.Message,
                message = "Failed to connect to Firebird database"
            });
        }
    }

    /// <summary>
    /// Execute a simple query to get database metadata
    /// </summary>
    [HttpGet("database/metadata")]
    public async Task<IActionResult> GetDatabaseMetadata()
    {
        _logger.LogInformation("Fetching database metadata");

        try
        {
            var connectionString = _firebirdConfig.GetConnectionString();
            
            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            // Get list of tables
            var tables = new List<string>();
            var command = new FbCommand(@"
                SELECT RDB$RELATION_NAME 
                FROM RDB$RELATIONS 
                WHERE RDB$SYSTEM_FLAG = 0 
                AND RDB$VIEW_BLR IS NULL
                ORDER BY RDB$RELATION_NAME", 
                connection);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0).Trim());
            }

            _logger.LogInformation("Retrieved {Count} tables from database", tables.Count);

            return Ok(new
            {
                database = _firebirdConfig.Database,
                serverVersion = connection.ServerVersion,
                tableCount = tables.Count,
                tables = tables.Take(20), // Limit to first 20 for display
                message = tables.Count > 20 
                    ? $"Showing first 20 of {tables.Count} tables" 
                    : $"Found {tables.Count} tables"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch database metadata");
            
            return StatusCode(500, new
            {
                status = "error",
                error = ex.Message,
                message = "Failed to fetch database metadata"
            });
        }
    }

    /// <summary>
    /// Test logging at all levels
    /// </summary>
    [HttpGet("test/logging")]
    public IActionResult TestLogging()
    {
        _logger.LogTrace("This is a TRACE message - most detailed");
        _logger.LogDebug("This is a DEBUG message - detailed info for debugging");
        _logger.LogInformation("This is an INFORMATION message - general info");
        _logger.LogWarning("This is a WARNING message - something needs attention");
        _logger.LogError("This is an ERROR message - an error occurred");

        try
        {
            throw new InvalidOperationException("This is a test exception for logging");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Caught test exception - this is expected for testing");
        }

        _logger.LogCritical("This is a CRITICAL message - severe error");

        return Ok(new
        {
            message = "Logging test completed",
            note = "Check your console output and remote logging service",
            loggerService = _configProvider.LoggerService,
            levels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" }
        });
    }

    /// <summary>
    /// Get all loaded configuration
    /// </summary>
    [HttpGet("config/all")]
    public IActionResult GetAllConfig()
    {
        _logger.LogInformation("All configuration requested");

        return Ok(new
        {
            bootstrap = new
            {
                clientId = _configProvider.ClientId,
                realm = _configProvider.Realm,
                authority = _configProvider.OpenIdConfig,
                loggerService = _configProvider.LoggerService
            },
            firebird = new
            {
                host = _firebirdConfig.Host,
                port = _firebirdConfig.Port,
                database = _firebirdConfig.Database,
                username = _firebirdConfig.UserName,
                charset = _firebirdConfig.Charset,
                role = _firebirdConfig.Role
            },
            message = "All configuration loaded successfully from config service"
        });
    }
}
'@
New-ProjectFile "SampleWebService\Controllers\SampleController.cs" $controllerContent

# ============================================================================
# appsettings.json
# ============================================================================
$appsettingsContent = @'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
'@
New-ProjectFile "SampleWebService\appsettings.json" $appsettingsContent

# ============================================================================
# run.sh
# ============================================================================
$runShContent = @'
#!/bin/bash
# Run script for Sample Web Service
# This sets the required environment variables and runs the service

echo "Setting environment variables..."
export SFD_CLIENT="dev-login"
export SFD_CONFIG_SERVICE="https://sfddevelopment.com/config"
export SFD_REALM="SfdDevelopment_Dev"

echo "Environment variables set:"
echo "  SFD_CLIENT=$SFD_CLIENT"
echo "  SFD_CONFIG_SERVICE=$SFD_CONFIG_SERVICE"
echo "  SFD_REALM=$SFD_REALM"
echo ""

echo "Starting Sample Web Service..."
dotnet run
'@
New-ProjectFile "SampleWebService\run.sh" $runShContent

# ============================================================================
# run.bat
# ============================================================================
$runBatContent = @'
@echo off
REM Run script for Sample Web Service
REM This sets the required environment variables and runs the service

echo Setting environment variables...
set SFD_CLIENT=dev-login
set SFD_CONFIG_SERVICE=https://sfddevelopment.com/config
set SFD_REALM=SfdDevelopment_Dev

echo Environment variables set:
echo   SFD_CLIENT=%SFD_CLIENT%
echo   SFD_CONFIG_SERVICE=%SFD_CONFIG_SERVICE%
echo   SFD_REALM=%SFD_REALM%
echo.

echo Starting Sample Web Service...
dotnet run
'@
New-ProjectFile "SampleWebService\run.bat" $runBatContent

# ============================================================================
# run.ps1
# ============================================================================
$runPs1Content = @'
#!/usr/bin/env pwsh
# Run script for Sample Web Service
# This sets the required environment variables and runs the service

Write-Host "Setting environment variables..." -ForegroundColor Cyan

$env:SFD_CLIENT = "dev-login"
$env:SFD_CONFIG_SERVICE = "https://sfddevelopment.com/config"
$env:SFD_REALM = "SfdDevelopment_Dev"

Write-Host "Environment variables set:" -ForegroundColor Green
Write-Host "  SFD_CLIENT=$env:SFD_CLIENT" -ForegroundColor Gray
Write-Host "  SFD_CONFIG_SERVICE=$env:SFD_CONFIG_SERVICE" -ForegroundColor Gray
Write-Host "  SFD_REALM=$env:SFD_REALM" -ForegroundColor Gray
Write-Host ""

Write-Host "Starting Sample Web Service..." -ForegroundColor Cyan
dotnet run
'@
New-ProjectFile "SampleWebService\run.ps1" $runPs1Content

Write-Host ""
Write-Host "Creating documentation files..." -ForegroundColor Green

# ============================================================================
# INDEX.md
# ============================================================================
$indexContent = @'
# Sample Web Service - START HERE

Welcome! This is a complete sample ASP.NET Core web service that demonstrates integration with your SfD infrastructure.

## 🎯 What This Demonstrates

Your web service that:
1. ✅ Fetches **bootstrap config** from your config service (no auth)
2. ✅ **Authenticates** as a service using client credentials
3. ✅ Fetches **Firebird database config** (requires auth)
4. ✅ Sends all logs to your **remote logger service**
5. ✅ Connects to **Firebird database** and queries it

## 🚀 Quick Start (30 Seconds)

### Windows
```cmd
run.bat
```

### Linux/macOS
```bash
./run.sh
```

### PowerShell
```powershell
./run.ps1
```

## 📚 Documentation

- **QUICKSTART.md** - Run in 5 minutes
- **SUMMARY.md** - High-level overview
- **README.md** - Complete documentation

## 🧪 Test It

```bash
curl http://localhost:5123/api/sample/health
curl http://localhost:5123/api/sample/config/bootstrap
curl http://localhost:5123/api/sample/config/firebird
curl http://localhost:5123/api/sample/database/test
```

Or open: `http://localhost:5123/swagger`

## ✨ What Happens

1. Fetches bootstrap config (clientId, authority, loggerService)
2. Authenticates as service (client credentials flow)
3. Fetches Firebird config (with access token)
4. Configures remote logging
5. Starts HTTP server

Ready? **Run one of the scripts above!** 🚀
'@
New-ProjectFile "SampleWebService\INDEX.md" $indexContent

# ============================================================================
# QUICKSTART.md
# ============================================================================
$quickstartContent = @'
# Quick Start Guide - Sample Web Service

Get the sample service running in under 5 minutes!

## Prerequisites

✅ .NET 8.0 SDK installed  
✅ SfD.Global library available (as project reference)  
✅ Access to config service (https://sfddevelopment.com/config)  
✅ Network connectivity

## Step 1: Navigate to Project

```bash
cd SampleWebService
```

## Step 2: Run the Service

Choose your platform:

### Windows (Command Prompt)
```cmd
run.bat
```

### Windows (PowerShell)
```powershell
.\run.ps1
```

### Linux/macOS
```bash
chmod +x run.sh
./run.sh
```

## Step 3: Verify It's Working

You should see:

```
ConfigService initialized successfully:
  ClientId: dev-login-svc
  Realm: SfdDevelopment_Dev
  
Service authenticated successfully

Firebird config loaded:
  Host: 10.9.8.14
  Database: C:\ReferenceDBs\DEV\ENGLAND.FDB

Sample Web Service Started Successfully
Listening on: http://0.0.0.0:5123
```

## Step 4: Test Endpoints

```bash
curl http://localhost:5123/api/sample/health
curl http://localhost:5123/api/sample/config/bootstrap
curl http://localhost:5123/api/sample/config/firebird
curl http://localhost:5123/api/sample/database/test
```

Or open Swagger: `http://localhost:5123/swagger`

## Success! 🎉

Your service is now:
- ✅ Fetching configuration
- ✅ Authenticating as a service
- ✅ Logging remotely
- ✅ Connecting to database

See **README.md** for full documentation.
'@
New-ProjectFile "SampleWebService\QUICKSTART.md" $quickstartContent

# ============================================================================
# SUMMARY.md
# ============================================================================
$summaryContent = @'
# Sample Web Service - Summary

## Overview

A complete ASP.NET Core web service demonstrating SfD.Global integration:
- Fetches bootstrap config from config service
- Authenticates as a service
- Fetches database config with authentication
- Logs to remote service
- Connects to Firebird database

## Files

- **Program.cs** - Initialization: config, auth, logging setup
- **Controllers/SampleController.cs** - REST API endpoints
- **SampleWebService.csproj** - Project with dependencies
- **run.sh / run.bat / run.ps1** - Run scripts

## Startup Flow

1. Set AppType to Service
2. Fetch bootstrap config (no auth)
3. Authenticate with client credentials
4. Fetch Firebird config (requires auth token)
5. Configure remote logging
6. Start HTTP server

## Environment Variables

Set by run scripts:
- `SFD_CLIENT=dev-login`
- `SFD_CONFIG_SERVICE=https://sfddevelopment.com/config`
- `SFD_REALM=SfdDevelopment_Dev`

## API Endpoints

- `GET /api/sample/health` - Health check
- `GET /api/sample/config/bootstrap` - View bootstrap config
- `GET /api/sample/config/firebird` - View Firebird config
- `GET /api/sample/database/test` - Test DB connection
- `GET /api/sample/database/metadata` - Get DB tables
- `GET /api/sample/test/logging` - Test all log levels

## Quick Run

```bash
./run.sh          # Linux/macOS
run.bat           # Windows
./run.ps1         # PowerShell
```

See **README.md** for complete documentation.
'@
New-ProjectFile "SampleWebService\SUMMARY.md" $summaryContent

# ============================================================================
# README.md (simplified version for generator)
# ============================================================================
$readmeContent = @'
# Sample Web Service

Complete sample demonstrating SfD.Global integration.

## Quick Start

```bash
# Run the service
./run.sh          # Linux/macOS
run.bat           # Windows
./run.ps1         # PowerShell
```

## Environment Variables

Set automatically by run scripts:
- `SFD_CLIENT=dev-login`
- `SFD_CONFIG_SERVICE=https://sfddevelopment.com/config`
- `SFD_REALM=SfdDevelopment_Dev`

## What It Does

1. Fetches bootstrap configuration (no auth)
2. Authenticates as service (client credentials)
3. Fetches Firebird database configuration (requires auth)
4. Configures remote logging
5. Starts HTTP server with REST API

## Testing

```bash
curl http://localhost:5123/api/sample/health
curl http://localhost:5123/api/sample/config/bootstrap
curl http://localhost:5123/api/sample/config/firebird
curl http://localhost:5123/api/sample/database/test
```

Or use Swagger UI: `http://localhost:5123/swagger`

## API Endpoints

- `/api/sample/health` - Health check
- `/api/sample/config/bootstrap` - Bootstrap config
- `/api/sample/config/firebird` - Firebird config
- `/api/sample/config/all` - All config
- `/api/sample/database/test` - Test DB connection
- `/api/sample/database/metadata` - Get DB metadata
- `/api/sample/test/logging` - Test logging

## Key Code

### Program.cs
```csharp
ConfigService.SetAppType(AppType.Service);
await ConfigService.InitializeAsync();
var accessToken = await ServiceAuthenticator.GetServiceAccessTokenAsync();
var firebirdConfig = await ConfigService.GetConfigAsync<FBConnection>("firebirddb", accessToken);
builder.Logging.AddSfdLogger();
```

### SampleController.cs
```csharp
public SampleController(
    FBConnection firebirdConfig,
    ILogger<SampleController> logger)
{
    // Use injected config and logger
}
```

## Dependencies

- SfD.Global library (project reference)
- FirebirdSql.Data.FirebirdClient NuGet package
- .NET 8.0

## Documentation

- **INDEX.md** - Overview and navigation
- **QUICKSTART.md** - 5-minute guide
- **SUMMARY.md** - High-level summary
- **README.md** - This file

## Troubleshooting

**Config service not reachable**: Check URL and network  
**Authentication failed**: Check client exists in Keycloak  
**Database connection failed**: Check Firebird server running  
**Logs not appearing**: Check logger service running  

## Extending

Add more config items:
```csharp
var myConfig = await ConfigService.GetConfigAsync<MyType>("myconfigname", accessToken);
```

Add more endpoints:
```csharp
[HttpGet("myendpoint")]
public IActionResult MyEndpoint() { ... }
```

## License

MIT
'@
New-ProjectFile "SampleWebService\README.md" $readmeContent

Write-Host ""
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host "  Project Generation Complete!" -ForegroundColor Green
Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Generated files in: $OutputPath\SampleWebService" -ForegroundColor Yellow
Write-Host ""
Write-Host "Project Structure:" -ForegroundColor Cyan
Write-Host "  SampleWebService/" -ForegroundColor White
Write-Host "    ├── Controllers/" -ForegroundColor White
Write-Host "    │   └── SampleController.cs" -ForegroundColor Gray
Write-Host "    ├── Program.cs" -ForegroundColor Gray
Write-Host "    ├── SampleWebService.csproj" -ForegroundColor Gray
Write-Host "    ├── appsettings.json" -ForegroundColor Gray
Write-Host "    ├── Run scripts (run.sh, run.bat, run.ps1)" -ForegroundColor Gray
Write-Host "    └── Documentation (INDEX.md, README.md, etc.)" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. cd $OutputPath\SampleWebService" -ForegroundColor White
Write-Host "  2. Read INDEX.md for overview" -ForegroundColor White
Write-Host "  3. Run: .\run.ps1 (Windows) or ./run.sh (Linux)" -ForegroundColor White
Write-Host "  4. Test: curl http://localhost:5123/api/sample/health" -ForegroundColor White
Write-Host ""
Write-Host "Quick Run:" -ForegroundColor Cyan
Write-Host "  cd $OutputPath\SampleWebService && .\run.ps1" -ForegroundColor White
Write-Host ""
Write-Host "Documentation:" -ForegroundColor Cyan
Write-Host "  INDEX.md     - Start here" -ForegroundColor White
Write-Host "  QUICKSTART.md - 5-minute guide" -ForegroundColor White
Write-Host "  SUMMARY.md   - Overview" -ForegroundColor White
Write-Host "  README.md    - Complete docs" -ForegroundColor White
Write-Host ""