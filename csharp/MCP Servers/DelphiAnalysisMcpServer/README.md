# DelphiAnalysisMcpServer

An MCP (Model Context Protocol) server for analyzing and translating Delphi/Object Pascal applications to modern C# with a React frontend.

## Architecture

This server generates a **three-tier architecture**:

1. **React Frontend** (`ProjectName.Web/`) - TypeScript React application
2. **ASP.NET Core API** (`ProjectName.Api/`) - Controllers exposing REST endpoints  
3. **Repository Layer** - Data access using Dapper with Firebird, inheriting from `BaseRepository`

### Key Features

- **Database operations → API calls**: All Delphi database code is migrated to repository classes that inherit from `BaseRepository`
- **Transaction preservation**: Multi-operation transactions in Delphi are preserved as atomic operations in a single repository method
- **VCL Forms → React components**: Delphi forms are converted to TypeScript React functional components
- **Type-safe DTOs**: Generated for all data structures
- **Method extraction**: All methods are extracted from source code with full implementations, linked to their SQL queries

### Method Extraction

The server automatically extracts all methods from Delphi source code during project analysis:

1. **Source code retrieval**: Uses backed-up source code from the database, or reads from the file system as fallback
2. **Implementation parsing**: Extracts complete method bodies including all local variables and nested code blocks
3. **Query linking**: Automatically links SQL queries to their containing methods via `method_idx` foreign key
4. **Class association**: Methods are correctly associated with their containing classes or marked as standalone

This enables:
- Viewing the complete Delphi method implementation alongside its queries
- Generating C# controller methods that accurately reflect the original Delphi logic
- Understanding the context in which each SQL query is executed

## Prerequisites

- .NET 10.0 SDK
- Ollama with `qwen2.5-coder:32b` model (or similar)
- Node.js 18+ (for React frontend)

## Installation

```bash
# Clone and build
dotnet restore
dotnet build

# Pull the recommended Ollama model
ollama pull qwen2.5-coder:32b

# Run the server
dotnet run
```

## MCP Configuration

Add to your Claude Desktop or MCP client configuration:

```json
{
  "mcpServers": {
    "delphi-analysis": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/DelphiAnalysisMcpServer"],
      "env": {}
    }
  }
}
```

Or connect via URL:

```json
{
  "mcpServers": {
    "delphi-analysis": {
      "url": "http://localhost:5100/sse"
    }
  }
}
```

## Available Tools

### Project Scanning
| Tool | Description |
|------|-------------|
| `scan_delphi_project` | Scans a Delphi project and returns manifest of units/forms |

### Analysis
| Tool | Description |
|------|-------------|
| `analyze_unit` | AI-powered structural analysis of a unit |
| `analyze_database_operations` | Extracts SQL, parameters, and transaction boundaries |

### Code Generation
| Tool | Description |
|------|-------------|
| `generate_repository` | Creates C# repository class inheriting from BaseRepository |
| `generate_controller` | Creates ASP.NET Core controller using the repository |
| `generate_react_component` | Converts Delphi form to React TypeScript component |
| `translate_unit` | Translates unit to C# (replaces DB calls with API calls) |
| `translate_project` | Batch translation of all units |

### Configuration & Output
| Tool | Description |
|------|-------------|
| `configure_translation` | Sets namespace, UI target, Ollama model, etc. |
| `generate_output` | Creates final project structure (folder/zip/scripts) |
| `get_session_status` | Check session progress |
| `list_sessions` | List all active sessions |

## Typical Workflow

```
┌─────────────────────────┐
│  scan_delphi_project    │
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│  configure_translation  │  (set ui_target="React")
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│analyze_database_operations│  (for data modules/units with DB code)
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│   generate_repository   │  (creates repository from DB operations)
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│   generate_controller   │  (creates API controller)
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│ generate_react_component│  (for each form)
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│    translate_project    │  (remaining units, DB calls → API calls)
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│     generate_output     │
└─────────────────────────┘
```

### Step-by-Step Example

```
1. Scan the project:
   scan_delphi_project path="/path/to/MyApp.dpr"
   → Returns session_id

2. Configure translation options:
   configure_translation session_id="..." base_namespace="MyApp" ui_target="React"

3. Analyze database operations in key units:
   analyze_database_operations session_id="..." unit_name="DataModule"
   → Returns SQL statements, parameters, transaction groups

4. Generate repository (handles all DB access):
   generate_repository session_id="..." repository_name="CustomerRepository"
   → Creates repository inheriting from BaseRepository

5. Generate controller:
   generate_controller session_id="..." controller_name="CustomersController" repository_name="CustomerRepository"
   → Creates ASP.NET Core controller

6. Generate React components from forms:
   generate_react_component session_id="..." form_name="CustomerForm"
   → Creates TypeScript React component

7. Translate remaining units:
   translate_project session_id="..."
   → All DB calls replaced with API calls

8. Generate final output:
   generate_output session_id="..." output_path="/output" format="Zip" generate_scripts=true
```

## BaseRepository Pattern

The generated repositories inherit from this base class:

```csharp
public abstract class BaseRepository
{
    private readonly AsyncLocal<FbTransaction?> _ambient = new();
    
    protected IDbConnection GetConnection(FbTransaction? external = null)
    {
        if (external?.Connection is not null) return external.Connection;
        if (_ambient.Value?.Connection is not null) return _ambient.Value.Connection;
        throw new InvalidOperationException("No active connection available");
    }
    
    protected FbTransaction? GetTransaction(FbTransaction? external = null)
    {
        if (external is not null) return external;
        return _ambient.Value;
    }
    
    public void Enlist(FbTransaction transaction) => _ambient.Value = transaction;
    
    protected void RollbackTransaction()
    {
        try { _ambient.Value?.Rollback(); }
        catch { /* swallow */ }
        finally
        {
            _ambient.Value?.Dispose();
            _ambient.Value = null;
        }
    }
}
```

### Transaction Handling

When Delphi code has multiple operations in a transaction:

```pascal
// Delphi
procedure TDataModule.TransferFunds(FromAcc, ToAcc: Integer; Amount: Currency);
begin
  Database.StartTransaction;
  try
    Query.SQL.Text := 'UPDATE Accounts SET Balance = Balance - :Amount WHERE ID = :ID';
    Query.ParamByName('Amount').AsCurrency := Amount;
    Query.ParamByName('ID').AsInteger := FromAcc;
    Query.ExecSQL;
    
    Query.SQL.Text := 'UPDATE Accounts SET Balance = Balance + :Amount WHERE ID = :ID';
    Query.ParamByName('Amount').AsCurrency := Amount;
    Query.ParamByName('ID').AsInteger := ToAcc;
    Query.ExecSQL;
    
    Database.Commit;
  except
    Database.Rollback;
    raise;
  end;
end;
```

This becomes a **single repository method**:

```csharp
// Generated Repository
public class AccountRepository : BaseRepository
{
    public async Task<bool> TransferFundsAsync(
        int fromAcc, 
        int toAcc, 
        decimal amount, 
        FbTransaction? external = null)
    {
        var conn = GetConnection(external);
        var tran = GetTransaction(external);
        
        await conn.ExecuteAsync(
            "UPDATE Accounts SET Balance = Balance - @Amount WHERE ID = @ID",
            new { Amount = amount, ID = fromAcc }, tran);
        
        await conn.ExecuteAsync(
            "UPDATE Accounts SET Balance = Balance + @Amount WHERE ID = @ID",
            new { Amount = amount, ID = toAcc }, tran);
        
        return true;
    }
}
```

And the **controller** manages the transaction:

```csharp
[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly AccountRepository _repository;
    private readonly string _connectionString;
    
    public AccountsController(AccountRepository repository, IConfiguration config)
    {
        _repository = repository;
        _connectionString = config.GetConnectionString("Firebird")!;
    }
    
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(TransferResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TransferFunds([FromBody] TransferRequest request)
    {
        await using var conn = new FbConnection(_connectionString);
        await conn.OpenAsync();
        await using var tran = await conn.BeginTransactionAsync();
        
        try
        {
            _repository.Enlist(tran);
            await _repository.TransferFundsAsync(
                request.FromAccount, 
                request.ToAccount, 
                request.Amount, 
                tran);
            await tran.CommitAsync();
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            await tran.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

## VCL to React Mappings

| Delphi VCL | React Equivalent |
|------------|------------------|
| TButton | `<button onClick={...}>` |
| TEdit | `<input type="text" value={...} onChange={...}>` |
| TLabel | `<label>` or `<span>` |
| TCheckBox | `<input type="checkbox">` |
| TMemo | `<textarea>` |
| TComboBox | `<select>` with `<option>` |
| TListBox | `<select multiple>` or custom list |
| TStringGrid | `<table>` or data grid component |
| TDBGrid | `<table>` with mapped data + API fetch |
| TPanel | `<div>` with className |
| TGroupBox | `<fieldset>` with `<legend>` |
| TPageControl | Tab component with state |
| TTreeView | Nested lists or tree component |
| TMainMenu | Navigation component |
| TPopupMenu | Context menu component |
| TTimer | useEffect with setInterval |

### React Component Pattern

Generated React components use:
- Functional components with hooks
- TypeScript for type safety
- Controlled inputs with useState
- API calls via fetch with async/await
- Loading and error states

Example generated component:

```tsx
import { useState, useEffect, useCallback } from 'react';
import { Customer } from '../types';

interface CustomerFormProps {
  customerId?: number;
  onSave?: (customer: Customer) => void;
}

export default function CustomerForm({ customerId, onSave }: CustomerFormProps) {
  const [customer, setCustomer] = useState<Customer | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCustomer = useCallback(async () => {
    if (!customerId) return;
    setIsLoading(true);
    setError(null);
    try {
      const response = await fetch(`/api/customers/${customerId}`);
      if (!response.ok) throw new Error('Failed to fetch customer');
      const data = await response.json();
      setCustomer(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setIsLoading(false);
    }
  }, [customerId]);

  useEffect(() => {
    fetchCustomer();
  }, [fetchCustomer]);

  const handleSubmit = async () => {
    if (!customer) return;
    setIsLoading(true);
    try {
      const response = await fetch('/api/customers', {
        method: customerId ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(customer),
      });
      if (!response.ok) throw new Error('Failed to save');
      onSave?.(customer);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading) return <div>Loading...</div>;
  if (error) return <div className="error">{error}</div>;

  return (
    <div className="customer-form">
      <div className="form-group">
        <label>Name:</label>
        <input
          type="text"
          value={customer?.name ?? ''}
          onChange={(e) => setCustomer(c => c ? {...c, name: e.target.value} : null)}
        />
      </div>
      <button onClick={handleSubmit}>Save</button>
    </div>
  );
}
```

## Generated Project Structure

```
output/
├── MyApp.sln
├── MyApp.Api/
│   ├── MyApp.Api.csproj
│   ├── Program.cs
│   ├── GlobalUsings.cs
│   ├── appsettings.json
│   ├── Classes/
│   │   └── BaseRepository.cs
│   ├── Repositories/
│   │   ├── CustomerRepository.cs
│   │   └── OrderRepository.cs
│   ├── Controllers/
│   │   ├── CustomersController.cs
│   │   └── OrdersController.cs
│   ├── Dtos/
│   │   ├── CustomerDto.cs
│   │   └── OrderDto.cs
│   └── README.md
├── MyApp.Web/
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts
│   ├── index.html
│   └── src/
│       ├── main.tsx
│       ├── App.tsx
│       ├── index.css
│       ├── components/
│       │   └── CustomerForm.tsx
│       ├── pages/
│       │   └── CustomersPage.tsx
│       ├── services/
│       │   └── api.ts
│       └── types/
│           └── index.ts
└── scripts/
    ├── deploy-MyApp.ps1
    └── deploy-MyApp.sh
```

## Delphi Type Mappings

| Delphi | C# |
|--------|-----|
| String | string |
| Integer | int |
| Int64 | long |
| Boolean | bool |
| Double | double |
| Currency | decimal |
| TDateTime | DateTime |
| TStringList | List\<string\> |
| TList\<T\> | List\<T\> |
| TDictionary\<K,V\> | Dictionary\<K,V\> |
| TObject | object |
| TStream | Stream |
| TMemoryStream | MemoryStream |

## Troubleshooting

### Ollama Connection Issues
- Ensure Ollama is running: `ollama serve`
- Check the model is available: `ollama list`
- Verify URL in configuration (default: `http://localhost:11434`)

### Large Unit Timeouts
- The default timeout is 10 minutes per translation
- For very large units, consider splitting or using `max_units` parameter

### Transaction Detection Issues
- Review `analyze_database_operations` output
- Manually adjust repository method grouping if needed

### React Build Issues
- Ensure Node.js 18+ is installed
- Run `npm install` in the Web project folder
- Check for TypeScript errors with `npm run build`

## Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /` | Server info and tool list |
| `GET /sse` | MCP SSE connection |
| `POST /mcp` | MCP JSON-RPC endpoint |
| `GET /health` | Health check |

## Environment Variables

- `ASPNETCORE_ENVIRONMENT` - Development/Production
- `ASPNETCORE_URLS` - Override listening URLs

## License

MIT
