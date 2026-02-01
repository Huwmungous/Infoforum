using Npgsql;
using SampleWebService.Models;
using IFGlobal.Models;

namespace SampleWebService.Repositories;

/// <summary>
/// Repository for Coach operations.
/// Demonstrates data access patterns using IFGlobal infrastructure.
/// </summary>
public sealed class CoachRepository : IAsyncDisposable
{
    private readonly ILogger<CoachRepository> _logger;
    private readonly string _connectionString;
    private NpgsqlConnection? _connection;

    public CoachRepository(
        PGConnectionConfig pgConfig,
        ILogger<CoachRepository> logger)
    {
        _logger = logger;
        _connectionString = pgConfig.ToString();

        _logger.LogDebug(
            "CoachRepository initialised - Database: {Host}:{Port}/{Database}",
            pgConfig.Host,
            pgConfig.Port,
            pgConfig.Database);
    }

    /// <summary>
    /// Gets a database connection, creating one if necessary.
    /// </summary>
    private async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection = new NpgsqlConnection(_connectionString);
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    /// <summary>
    /// Get all coaches ordered by idx.
    /// </summary>
    public async Task<IEnumerable<Coach>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var coaches = new List<Coach>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT idx, email, name 
            FROM dbo.coach
            ORDER BY idx ASC";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            coaches.Add(MapCoach(reader));
        }

        _logger.LogDebug("Retrieved {Count} coaches", coaches.Count);
        return coaches;
    }

    /// <summary>
    /// Get a coach by idx.
    /// </summary>
    public async Task<Coach?> GetByIdAsync(int idx, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT idx, email, name 
            FROM dbo.coach 
            WHERE idx = @Idx";
        cmd.Parameters.AddWithValue("@Idx", idx);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapCoach(reader);
        }

        return null;
    }

    /// <summary>
    /// Get the total count of coaches.
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.coach";

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Maps a data reader row to a Coach object.
    /// </summary>
    private static Coach MapCoach(NpgsqlDataReader reader)
    {
        return new Coach
        {
            Idx = reader.GetInt32(reader.GetOrdinal("idx")),
            Email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name"))
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
