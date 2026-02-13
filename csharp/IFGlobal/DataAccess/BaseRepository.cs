using Npgsql;
using Microsoft.Extensions.Logging;
using IFGlobal.Models;
using System.Data;
using System.Data.Common;

namespace IFGlobal.DataAccess;

/// <summary>
/// Base class for PostgreSQL data access repositories.
/// Provides common database operations with async support and transaction management.
/// Can be used directly or extended by specific repositories.
/// </summary>
public class BaseRepository : IBaseRepository
{
    private readonly string _connectionString;
    private NpgsqlTransaction? _transaction;
    protected readonly ILogger _logger;

    public BaseRepository(PGConnectionConfig pgConnection, ILogger<BaseRepository> logger)
    {
        _connectionString = pgConnection.ToString();
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
    public NpgsqlTransaction? ExplicitTransaction
    {
        get => _transaction;
        set
        {
            if(value != _transaction)
                _transaction = value;
        }
    }

    /// <summary>
    /// Creates and opens a new connection, or returns the transaction's connection if set.
    /// </summary>
    protected async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if(_transaction is not null)
            return _transaction.Connection!;

        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>
    /// Creates and returns a new unopened connection, or returns the transaction's connection if set.
    /// The caller is responsible for opening and disposing the connection.
    /// </summary>
    public NpgsqlConnection GetConnection()
    {
        if(_transaction is not null)
            return _transaction.Connection!;

        return new NpgsqlConnection(_connectionString);
    }

    /// <summary>
    /// Returns true if the caller should dispose the connection (i.e., no active transaction).
    /// When in a transaction, the connection is owned by the transaction.
    /// </summary>
    protected bool ShouldDisposeConnection => _transaction is null;

    /// <summary>
    /// Executes a query and maps results using the provided mapper function.
    /// </summary>
    protected async Task<List<T>> ExecuteQueryAsync<T>(
        string sql,
        Func<DbDataReader, T?> mapper,
        CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            return await ReadResultsAsync(command, mapper, ct);
        }
        finally
        {
            if(ShouldDisposeConnection)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Executes a query with parameters and maps results using the provided mapper function.
    /// </summary>
    protected async Task<List<T>> ExecuteQueryAsync<T>(
        string sql,
        Action<NpgsqlParameterCollection> configureParameters,
        Func<DbDataReader, T?> mapper,
        CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            configureParameters(command.Parameters);
            return await ReadResultsAsync(command, mapper, ct);
        }
        finally
        {
            if(ShouldDisposeConnection)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Executes a scalar query and returns the result.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(string sql, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var result = await command.ExecuteScalarAsync(ct);
            return result is DBNull or null ? default : (T)Convert.ChangeType(result, typeof(T));
        }
        finally
        {
            if(ShouldDisposeConnection)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Executes a scalar query with parameters and returns the result.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        Action<NpgsqlParameterCollection> configureParameters,
        CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            configureParameters(command.Parameters);
            var result = await command.ExecuteScalarAsync(ct);
            return result is DBNull or null ? default : (T)Convert.ChangeType(result, typeof(T));
        }
        finally
        {
            if(ShouldDisposeConnection)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Executes a non-query command (INSERT, UPDATE, DELETE).
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            return await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if(ShouldDisposeConnection)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Executes a non-query command with parameters.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<NpgsqlParameterCollection> configureParameters,
        CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            configureParameters(command.Parameters);
            return await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if(ShouldDisposeConnection)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Executes a query and returns the first result, or default if none.
    /// </summary>
    protected async Task<T?> ExecuteQueryFirstOrDefaultAsync<T>(
        string sql,
        Action<NpgsqlParameterCollection> configureParameters,
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
        Func<NpgsqlTransaction, Task> operations,
        CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
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
        if(_transaction != null)
        {
            return await operation();
        }

        // Create a new transaction scope
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
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
        if(_transaction != null)
        {
            await operation();
            return;
        }

        // Create a new transaction scope
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
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
    /// 
    /// When not in a transaction, uses CommandBehavior.CloseConnection so the
    /// underlying connection is automatically closed when the reader is disposed.
    /// 
    /// Usage:
    ///   using var reader = repo.OpenQuery("SELECT ...");
    ///   while (reader.Read()) { ... }
    ///   // Connection is closed automatically when reader is disposed
    /// </summary>
    public NpgsqlDataReader OpenQuery(string sql)
    {
        // When in a transaction, use the transaction's connection (don't auto-close it)
        if(_transaction is not null)
        {
            var txCommand = _transaction.Connection!.CreateCommand();
            txCommand.CommandText = sql;
            txCommand.CommandType = CommandType.Text;
            txCommand.Transaction = _transaction;
            return txCommand.ExecuteReader();
        }

        // No transaction: create a new connection and use CloseConnection behaviour
        // so the connection is closed when the reader is disposed.
        // This fixes the original which leaked both the command and connection.
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        return command.ExecuteReader(CommandBehavior.CloseConnection);
    }

    private static async Task<List<T>> ReadResultsAsync<T>(
        NpgsqlCommand command,
        Func<DbDataReader, T?> mapper,
        CancellationToken ct)
    {
        var results = new List<T>();

        await using var reader = await command.ExecuteReaderAsync(ct);

        while(await reader.ReadAsync(ct))
        {
            var item = mapper(reader);
            if(item is not null)
                results.Add(item);
        }

        return results;
    }
}