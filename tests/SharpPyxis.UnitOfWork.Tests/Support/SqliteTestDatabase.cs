using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace SharpPyxis.UnitOfWork.Tests.Support;

/// <summary>
/// A private, shared-cache in-memory SQLite database for a single test. An "anchor" connection is
/// kept open so the in-memory database survives while the unit of work opens and closes its own
/// connections. Every test gets an isolated database (unique data source name).
/// </summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _anchor;

    /// <summary>The connection string every unit of work under test connects with.</summary>
    public string ConnectionString { get; }

    private SqliteTestDatabase(string connectionString, SqliteConnection anchor)
    {
        ConnectionString = connectionString;
        _anchor = anchor;
    }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();

        await using var cmd = anchor.CreateCommand();
        cmd.CommandText = "CREATE TABLE widgets (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL)";
        await cmd.ExecuteNonQueryAsync();

        return new SqliteTestDatabase(connectionString, anchor);
    }

    /// <summary>Creates a new, unopened connection to this database.</summary>
    public DbConnection NewConnection() => new SqliteConnection(ConnectionString);

    /// <summary>Creates and opens a new connection to this database.</summary>
    public async Task<DbConnection> NewOpenConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>Connection provider suitable for <c>AdoUnitOfWork.OpenAsync</c> / the factory.</summary>
    public Task<DbConnection> ProvideConnectionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<DbConnection>(new SqliteConnection(ConnectionString));

    /// <summary>Counts rows through an independent connection (external observer).</summary>
    public async Task<int> CountWidgetsAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM widgets";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async ValueTask DisposeAsync() => await _anchor.DisposeAsync();
}
