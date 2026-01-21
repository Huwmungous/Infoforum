using FirebirdSql.Data.FirebirdClient;
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

    private FbConnection? _connection;

    public AccountRepository(
        FBConnectionConfig fbConfig,
        ILogger<AccountRepository> logger)
    {
        _logger = logger;
        _connectionString = fbConfig.ToString();
        _requiresRelay = fbConfig.RequiresRelay;

        _logger.LogDebug("AccountRepository initialised - Mode: {Mode}, Database: {Host}:{Port}/{Database}",
            _requiresRelay ? "RELAY" : "DIRECT",
            fbConfig.Host,
            fbConfig.Port,
            fbConfig.Database);
    }

    /// <summary>
    /// Gets a database connection, creating one if necessary.
    /// </summary>
    private async Task<FbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_requiresRelay)
        {
            throw new NotImplementedException(
                "Relay mode not yet implemented. Configure 'RequiresRelay: false' in ConfigService.");
        }

        if (_connection is null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection = new FbConnection(_connectionString);
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
        cmd.CommandText = "SELECT * FROM ACCOUNT ORDER BY ACCOUNT_ID";

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
        cmd.CommandText = "SELECT * FROM ACCOUNT WHERE ACCOUNT_ID = @AccountId";
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
        cmd.CommandText = "SELECT COUNT(*) FROM ACCOUNT";

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
        cmd.CommandText = "SELECT * FROM ACCOUNT ORDER BY NAME";

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
            // Get next ID (Firebird generator)
            await using var idCmd = connection.CreateCommand();
            idCmd.Transaction = transaction;
            idCmd.CommandText = "SELECT GEN_ID(GEN_ACCOUNT_ID, 1) FROM RDB$DATABASE";
            var newId = Convert.ToInt32(await idCmd.ExecuteScalarAsync(cancellationToken));

            // Insert the account
            await using var insertCmd = connection.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = @"
                INSERT INTO ACCOUNT (ACCOUNT_ID, ACCOUNT_NUMBER, ACCOUNT_NAME, ACCOUNT_TYPE, BALANCE, CREATED_DATE, IS_ACTIVE)
                VALUES (@AccountId, @AccountNumber, @AccountName, @AccountType, @Balance, @CreatedDate, @IsActive)";

            insertCmd.Parameters.AddWithValue("@AccountId", newId);
            insertCmd.Parameters.AddWithValue("@AccountNumber", account.AccountNumber);
            insertCmd.Parameters.AddWithValue("@AccountName", account.AccountName);
            insertCmd.Parameters.AddWithValue("@AccountType", account.AccountType);
            insertCmd.Parameters.AddWithValue("@Balance", account.Balance);
            insertCmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            insertCmd.Parameters.AddWithValue("@IsActive", true);

            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
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
            UPDATE ACCOUNT SET
                ACCOUNT_NUMBER = @AccountNumber,
                ACCOUNT_NAME = @AccountName,
                ACCOUNT_TYPE = @AccountType,
                BALANCE = @Balance,
                MODIFIED_DATE = @ModifiedDate,
                IS_ACTIVE = @IsActive
            WHERE ACCOUNT_ID = @AccountId";

        cmd.Parameters.AddWithValue("@AccountId", account.AccountId);
        cmd.Parameters.AddWithValue("@AccountNumber", account.AccountNumber);
        cmd.Parameters.AddWithValue("@AccountName", account.AccountName);
        cmd.Parameters.AddWithValue("@AccountType", account.AccountType);
        cmd.Parameters.AddWithValue("@Balance", account.Balance);
        cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
        cmd.Parameters.AddWithValue("@IsActive", account.IsActive);

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
        cmd.CommandText = "DELETE FROM ACCOUNT WHERE ACCOUNT_ID = @AccountId";
        cmd.Parameters.AddWithValue("@AccountId", accountId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    /// <summary>
    /// Maps a data reader row to an Account object.
    /// </summary>
    private static Account MapAccount(FbDataReader reader)
    {
        return new Account
        {
            AccountId = reader.GetInt32(reader.GetOrdinal("ACCOUNT_ID")),
            AccountNumber = reader.GetString(reader.GetOrdinal("ACCOUNT_NUMBER")),
            AccountName = reader.GetString(reader.GetOrdinal("ACCOUNT_NAME")),
            AccountType = reader.GetString(reader.GetOrdinal("ACCOUNT_TYPE")),
            Balance = reader.GetDecimal(reader.GetOrdinal("BALANCE")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IS_ACTIVE"))
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