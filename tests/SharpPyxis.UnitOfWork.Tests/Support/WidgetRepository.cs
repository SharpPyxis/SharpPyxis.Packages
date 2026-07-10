using System.Data.Common;

namespace SharpPyxis.UnitOfWork.Tests.Support;

/// <summary>A minimal repository contract used to exercise the unit of work.</summary>
public interface IWidgetRepository
{
    Task InsertAsync(string name, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A "blind" repository: it receives the connection and a late-bound transaction accessor, forwards
/// whatever transaction is current to each command, and never opens its own connection. It has no
/// idea whether a transaction is active — mirrors the real-world Dapper repositories this package
/// targets, but with raw ADO.NET so the tests carry no extra dependency.
/// </summary>
internal sealed class WidgetRepository(DbConnection connection, Func<DbTransaction?> currentTransaction)
    : IWidgetRepository
{
    public async Task InsertAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = currentTransaction();
        cmd.CommandText = "INSERT INTO widgets (name) VALUES ($name)";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = name;
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = currentTransaction();
        cmd.CommandText = "SELECT COUNT(*) FROM widgets";
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }
}
