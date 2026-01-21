using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Logging;
using IFGlobal.Models;
using System.Data;
using System.Data.Common;

namespace IFGlobal.DataAccess;

/// <summary>
/// Base class for Firebird data access repositories.
/// Provides common database operations with async support and transaction management.
/// Can be used directly or extended by specific repositories.
/// </summary>
public class BaseRepository : IBaseRepository
{
    private readonly string _connectionString;
    private FbTransaction? _transaction;
    protected readonly ILogger _logger;

    public BaseRepository(FBConnectionConfig fbConnection, ILogger<BaseRepository> logger)
    {
        _connectionString = fbConnection.ToString();
        _logger = logger;
    }

    protected BaseRepository(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the current data access mode.
    /// </summary>
    public DataAccessMode Mode { get; set; }

    /// <summary>
    /// Gets or sets an explicit transaction for operations.
    /// </summary>
    public FbTransaction? ExplicitTransaction
    {
        get => _transaction;
        set
        {
            if (value != _transaction)
                _transaction = value;
        }
    }

    /// <summary>
    /// Creates and opens a new connection, or returns the transaction's connection if set.
    /// </summary>
    protected async Task<FbConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            return _transaction.Connection!;

        var connection = new FbConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>
    /// Creates and opens a new connection synchronously.
    /// </summary>
    public FbConnection GetConnection()
    {
        if (_transaction is not null)
            return _transaction.Connection!;

        var connection = new FbConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Executes a query and maps results using the provided mapper function.
    /// </summary>
    protected async Task<List<T>> ExecuteQueryAsync<T>(
        string sql,
        Func<DbDataReader, T?> mapper,
        CancellationToken ct = default)
    {
        await using var connection = await GetConnectionAsync(ct);
        await using var command = new FbCommand(sql, connection);

        return await ReadResultsAsync(command, mapper, ct);
    }

    /// <summary>
    /// Executes a query with parameters and maps results using the provided mapper function.
    /// </summary>
    protected async Task<List<T>> ExecuteQueryAsync<T>(
        string sql,
        Action<FbParameterCollection> configureParameters,
        Func<DbDataReader, T?> mapper,
        CancellationToken ct = default)
    {
        await using var connection = await GetConnectionAsync(ct);
        await using var command = new FbCommand(sql, connection);

        configureParameters(command.Parameters);

        return await ReadResultsAsync(command, mapper, ct);
    }

    /// <summary>
    /// Executes a scalar query and returns the result.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(string sql, CancellationToken ct = default)
    {
        await using var connection = await GetConnectionAsync(ct);
        await using var command = new FbCommand(sql, connection);

        var result = await command.ExecuteScalarAsync(ct);
        return result is DBNull or null ? default : (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Executes a scalar query with parameters and returns the result.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        Action<FbParameterCollection> configureParameters,
        CancellationToken ct = default)
    {
        await using var connection = await GetConnectionAsync(ct);
        await using var command = new FbCommand(sql, connection);

        configureParameters(command.Parameters);

        var result = await command.ExecuteScalarAsync(ct);
        return result is DBNull or null ? default : (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Executes a non-query command (INSERT, UPDATE, DELETE).
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default)
    {
        await using var connection = await GetConnectionAsync(ct);
        await using var command = new FbCommand(sql, connection);

        return await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Executes a non-query command with parameters.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<FbParameterCollection> configureParameters,
        CancellationToken ct = default)
    {
        await using var connection = await GetConnectionAsync(ct);
        await using var command = new FbCommand(sql, connection);

        configureParameters(command.Parameters);

        return await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Executes a query and returns the first result, or default if none.
    /// </summary>
    protected async Task<T?> ExecuteQueryFirstOrDefaultAsync<T>(
        string sql,
        Action<FbParameterCollection> configureParameters,
        Func<DbDataReader, T?> mapper,
        CancellationToken ct = default)
    {
        var results = await ExecuteQueryAsync(sql, configureParameters, mapper, ct);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Executes operations within a transaction.
    /// Legacy method - prefer ExecuteInTransactionScopeAsync for better composability.
    /// </summary>
    protected async Task ExecuteInTransactionAsync(
        Func<FbTransaction, Task> operations,
        CancellationToken ct = default)
    {
        await using var connection = await GetConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            await operations(transaction);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Executes an operation within a transaction scope.
    /// If already in a transaction (ExplicitTransaction is set), executes directly.
    /// Otherwise, creates a new transaction, executes the operation, and commits/rollbacks as needed.
    /// This enables Delphi transaction patterns to be replicated in C#.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute within the transaction scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    protected async Task<T> ExecuteInTransactionScopeAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct = default)
    {
        // If already in a transaction, just execute the operation
        if (_transaction != null)
        {
            return await operation();
        }

        // Create a new transaction scope
        await using var connection = await GetConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var previousTransaction = _transaction;
        _transaction = transaction;

        try
        {
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            _transaction = previousTransaction;
        }
    }

    /// <summary>
    /// Executes an operation within a transaction scope (void return).
    /// If already in a transaction (ExplicitTransaction is set), executes directly.
    /// Otherwise, creates a new transaction, executes the operation, and commits/rollbacks as needed.
    /// </summary>
    /// <param name="operation">The operation to execute within the transaction scope.</param>
    /// <param name="ct">Cancellation token.</param>
    protected async Task ExecuteInTransactionScopeAsync(
        Func<Task> operation,
        CancellationToken ct = default)
    {
        // If already in a transaction, just execute the operation
        if (_transaction != null)
        {
            await operation();
            return;
        }

        // Create a new transaction scope
        await using var connection = await GetConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var previousTransaction = _transaction;
        _transaction = transaction;

        try
        {
            await operation();
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            _transaction = previousTransaction;
        }
    }

    /// <summary>
    /// Opens a synchronous query reader.
    /// </summary>
    public FbDataReader OpenQuery(string sql)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        return command.ExecuteReader();
    }

    private static async Task<List<T>> ReadResultsAsync<T>(
        FbCommand command,
        Func<DbDataReader, T?> mapper,
        CancellationToken ct)
    {
        var results = new List<T>();

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var item = mapper(reader);
            if (item is not null)
                results.Add(item);
        }

        return results;
    }
}
