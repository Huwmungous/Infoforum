# Code Analysis MCP Server

A Model Context Protocol (MCP) server for source code analysis, specialized in Delphi.

## Features

- Parse Delphi source files
- Extract SQL statements from code
- Find database component usage
- Analyze stored procedure calls
- Extract class and method definitions
- Code metrics and statistics
- Pattern matching with regex

## Quick Start

```bash
dotnet build
dotnet run
```

## Test

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize"}' | dotnet run
```

## Tools Available

- parse_delphi_file - Complete Delphi file analysis
- extract_sql_statements - Find SQL in code
- find_database_calls - Locate ADO/FireDAC components
- extract_table_references - Get table names from SQL
- analyze_procedure_calls - Find stored procedure usage
- find_patterns - Custom regex search
- get_code_metrics - Line counts and statistics
- extract_class_definitions - Class structure
- extract_method_signatures - Method declarations
- map_data_structures - Record and type definitions
