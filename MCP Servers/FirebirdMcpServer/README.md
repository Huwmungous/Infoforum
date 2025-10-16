# Firebird Database MCP Server

A Model Context Protocol (MCP) server for Firebird database operations.

## Features

- Database connection and metadata
- Table schema inspection
- Stored procedure analysis
- Trigger examination
- Query execution
- DDL generation
- Foreign key relationships

## Quick Start

```bash
dotnet build
dotnet run
```

## Example Connection String

```
Server=localhost;Database=C:\Data\MyDatabase.fdb;User=SYSDBA;Password=masterkey;
```

## Test

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize"}' | dotnet run
```

## Tools Available

- connect_database, test_connection, get_database_metadata
- list_tables, get_table_schema, get_table_columns
- get_table_indexes, get_table_constraints, get_foreign_keys
- list_stored_procedures, get_procedure_definition, get_procedure_parameters
- list_triggers, get_trigger_definition
- execute_query, generate_ddl
