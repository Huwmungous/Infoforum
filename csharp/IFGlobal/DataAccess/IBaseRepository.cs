using Npgsql;
using IFGlobal.Models;

namespace IFGlobal.DataAccess;

/// <summary>
/// Interface for the base repository functionality.
/// </summary>
public interface IBaseRepository
{
    /// <summary>
    /// Gets or sets the current data access mode.
    /// </summary>
    DataAccessMode Mode { get; set; }

    /// <summary>
    /// Gets or sets an explicit transaction for operations.
    /// </summary>
    NpgsqlTransaction? ExplicitTransaction { get; set; }

    /// <summary>
    /// Creates and opens a new connection synchronously.
    /// </summary>
    NpgsqlConnection GetConnection();

    /// <summary>
    /// Opens a synchronous query reader.
    /// </summary>
    NpgsqlDataReader OpenQuery(string sql);
}
