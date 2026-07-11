using System.Data;
using System.Data.Common;

namespace SharpPyxis.UnitOfWork.Ado;

/// <summary>
/// Creates <see cref="AdoUnitOfWork"/> instances that open their connection from a fixed provider and
/// resolve repositories from a shared <see cref="RepositoryRegistry"/>. Register one factory at
/// startup (e.g. as a singleton) and open a unit of work per operation. This is a plain object — it has
/// no dependency on any DI container — and it is the relational implementation of
/// <see cref="IUnitOfWorkFactory"/>.
/// </summary>
/// <param name="connectionProvider">Produces the connection for each unit of work this factory creates.</param>
/// <param name="registry">The shared catalog of repository factories.</param>
/// <param name="isolationLevel">Isolation level applied to transactions of the units of work this factory creates.</param>
public sealed class AdoUnitOfWorkFactory(
    Func<CancellationToken, Task<DbConnection>> connectionProvider,
    RepositoryRegistry registry,
    IsolationLevel isolationLevel = IsolationLevel.Unspecified) : IUnitOfWorkFactory
{
    private readonly Func<CancellationToken, Task<DbConnection>> _connectionProvider =
        connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
    private readonly RepositoryRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly IsolationLevel _isolationLevel = isolationLevel;

    /// <inheritdoc/>
    public async Task<IUnitOfWork> OpenAsync(CancellationToken cancellationToken = default) =>
        await AdoUnitOfWork.OpenAsync(
            _connectionProvider, _registry, beginTransaction: false, _isolationLevel, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IUnitOfWork> OpenAndBeginAsync(CancellationToken cancellationToken = default) =>
        await AdoUnitOfWork.OpenAsync(
            _connectionProvider, _registry, beginTransaction: true, _isolationLevel, cancellationToken)
            .ConfigureAwait(false);
}
