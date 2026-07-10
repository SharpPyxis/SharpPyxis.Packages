using System.Data;
using System.Data.Common;

namespace SharpPyxis.UnitOfWork.Ado;

/// <summary>
/// Creates <see cref="AdoUnitOfWork"/> instances that open their connection from a fixed provider and
/// resolve repositories from a shared <see cref="RepositoryRegistry"/>. Register one factory at
/// startup (e.g. as a singleton) and call <see cref="CreateAsync"/> per operation. This is a plain
/// object — it has no dependency on any DI container.
/// </summary>
/// <param name="connectionProvider">Produces the connection for each unit of work this factory creates.</param>
/// <param name="registry">The shared catalog of repository factories.</param>
/// <param name="isolationLevel">Isolation level applied to transactions of the units of work this factory creates.</param>
public sealed class AdoUnitOfWorkFactory(
    Func<CancellationToken, Task<DbConnection>> connectionProvider,
    RepositoryRegistry registry,
    IsolationLevel isolationLevel = IsolationLevel.Unspecified)
{
    private readonly Func<CancellationToken, Task<DbConnection>> _connectionProvider =
        connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
    private readonly RepositoryRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly IsolationLevel _isolationLevel = isolationLevel;

    /// <summary>Creates a unit of work with an open connection. No transaction is started.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AdoUnitOfWork> CreateAsync(CancellationToken cancellationToken = default) =>
        AdoUnitOfWork.OpenAsync(
            _connectionProvider, _registry, beginTransaction: false, _isolationLevel, cancellationToken);

    /// <summary>Creates a unit of work with an open connection and an already-begun transaction.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AdoUnitOfWork> CreateAndBeginAsync(CancellationToken cancellationToken = default) =>
        AdoUnitOfWork.OpenAsync(
            _connectionProvider, _registry, beginTransaction: true, _isolationLevel, cancellationToken);
}
