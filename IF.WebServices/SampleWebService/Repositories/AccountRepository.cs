using Npgsql;
using SampleWebService.Models;
using IFGlobal.Models;

namespace SampleWebService.Repositories;

/// <summary>
/// Repository for Account operations.
/// Encapsulates all data access logic including Direct/Relay mode determination.
/// </summary>
public sealed class AccountRepository : IAsyncDisposable
{
    private readonly ILogger<AccountRepository> _logger;
    private readonly string _connectionString;
    private readonly bool _requiresRelay;

    private NpgsqlConnection? _connection;

    public AccountRepository(
        PGConnectionConfig pgConfig,
        ILogger<AccountRepository> logger)
    {
        _logger = logger;
        _connectionString = pgConfig.ToString();
        _requiresRelay = pgConfig.RequiresRelay;

        _logger.LogDebug("AccountRepository initialised - Mode: {Mode}, Database: {Host}:{Port}/{Database}",
            _requiresRelay ? "RELAY" : "DIRECT",
            pgConfig.Host,
            pgConfig.Port,
            pgConfig.Database);
    }

    /// <summary>
    /// Gets a database connection, creating one if necessary.
    /// </summary>
    private async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_requiresRelay)
        {
            throw new NotImplementedException(
                "Relay mode not yet implemented. Configure 'RequiresRelay: false' in ConfigService.");
        }

        if (_connection is null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection = new NpgsqlConnection(_connectionString);
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    /// <summary>
    /// Get all accounts.
    /// </summary>
    public async Task<IEnumerable<Account>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var accounts = new List<Account>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM account ORDER BY account_id";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(MapAccount(reader));
        }

        return accounts;
    }

    /// <summary>
    /// Get an account by ID.
    /// </summary>
    public async Task<Account?> GetByIdAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM account WHERE account_id = @AccountId";
        cmd.Parameters.AddWithValue("@AccountId", accountId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapAccount(reader);
        }

        return null;
    }

    /// <summary>
    /// Get the total count of accounts.
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM account";

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Get active accounts only.
    /// </summary>
    public async Task<IEnumerable<Account>> GetActiveAccountsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var accounts = new List<Account>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM account WHERE is_active = true ORDER BY account_name";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(MapAccount(reader));
        }

        return accounts;
    }

    /// <summary>
    /// Create a new account.
    /// </summary>
    public async Task<int> CreateAsync(Account account, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // Insert the account and return the generated ID (PostgreSQL RETURNING clause)
            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = @"
                INSERT INTO account (account_number, account_name, account_type, balance, created_date, is_active)
                VALUES (@AccountNumber, @AccountName, @AccountType, @Balance, @CreatedDate, @IsActive)
                RETURNING account_id";

            insertCmd.Parameters.AddWithValue("@AccountNumber", (object?)account.AccountNumber ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@AccountName", (object?)account.AccountName ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@AccountType", (object?)account.AccountType ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@Balance", (object?)account.Balance ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            insertCmd.Parameters.AddWithValue("@IsActive", true);

            var newId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync(cancellationToken));
            await transaction.CommitAsync(cancellationToken);

            return newId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Update an existing account.
    /// </summary>
    public async Task<bool> UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE account SET
                account_number = @AccountNumber,
                account_name = @AccountName,
                account_type = @AccountType,
                balance = @Balance,
                modified_date = @ModifiedDate,
                is_active = @IsActive
            WHERE account_id = @AccountId";

        cmd.Parameters.AddWithValue("@AccountId", account.AccountId);
        cmd.Parameters.AddWithValue("@AccountNumber", (object?)account.AccountNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AccountName", (object?)account.AccountName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AccountType", (object?)account.AccountType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Balance", (object?)account.Balance ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
        cmd.Parameters.AddWithValue("@IsActive", (object?)account.IsActive ?? DBNull.Value);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    /// <summary>
    /// Delete an account.
    /// </summary>
    public async Task<bool> DeleteAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM account WHERE account_id = @AccountId";
        cmd.Parameters.AddWithValue("@AccountId", accountId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    /// <summary>
    /// Maps a data reader row to an Account object.
    /// </summary>
    private static Account MapAccount(NpgsqlDataReader reader)
    {
        return new Account
        {
            AccountId = reader.GetInt32(reader.GetOrdinal("account_id")),
            AccountNumber = reader.IsDBNull(reader.GetOrdinal("account_number")) ? null : reader.GetString(reader.GetOrdinal("account_number")),
            AccountName = reader.IsDBNull(reader.GetOrdinal("account_name")) ? null : reader.GetString(reader.GetOrdinal("account_name")),
            AccountType = reader.IsDBNull(reader.GetOrdinal("account_type")) ? null : reader.GetString(reader.GetOrdinal("account_type")),
            Balance = reader.IsDBNull(reader.GetOrdinal("balance")) ? null : reader.GetDecimal(reader.GetOrdinal("balance")),
            IsActive = reader.IsDBNull(reader.GetOrdinal("is_active")) ? null : reader.GetBoolean(reader.GetOrdinal("is_active"))
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}