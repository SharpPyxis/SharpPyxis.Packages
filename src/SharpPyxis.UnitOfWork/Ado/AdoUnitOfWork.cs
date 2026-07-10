using System.Data;
using System.Data.Common;

namespace SharpPyxis.UnitOfWork.Ado;

/// <summary>
/// A unit of work over any ADO.NET provider. It owns a <see cref="DbConnection"/> and an optional
/// <see cref="DbTransaction"/>, and resolves repositories on demand from a shared
/// <see cref="RepositoryRegistry"/>. Nothing here is provider-specific — it works with Npgsql,
/// SqlClient, SQLite, and any other <see cref="DbConnection"/>.
/// </summary>
/// <param name="connection">
/// The connection this unit of work uses. It may be open or closed; <see cref="BeginAsync"/> opens
/// it if needed. Repositories resolved before any transaction runs against no transaction.
/// </param>
/// <param name="registry">The shared catalog of repository factories.</param>
/// <param name="ownsConnection">
/// When <see langword="true"/> (default), the connection is disposed together with this unit of work.
/// Set to <see langword="false"/> when the connection's lifetime is managed elsewhere (e.g. a DI scope).
/// </param>
/// <param name="isolationLevel">
/// Isolation level for transactions started by <see cref="BeginAsync"/>.
/// <see cref="IsolationLevel.Unspecified"/> lets the provider choose.
/// </param>
public sealed class AdoUnitOfWork(
    DbConnection connection,
    RepositoryRegistry registry,
    bool ownsConnection = true,
    IsolationLevel isolationLevel = IsolationLevel.Unspecified) : IUnitOfWork
{
    private readonly DbConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly RepositoryRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly bool _ownsConnection = ownsConnection;
    private readonly IsolationLevel _isolationLevel = isolationLevel;
    private readonly Dictionary<Type, object> _repositories = new();

    private DbTransaction? _transaction;
    private bool _disposed;

    /// <summary>
    /// Opens a connection from <paramref name="connectionProvider"/> and wraps it in a unit of work,
    /// optionally beginning a transaction. Generalizes the tenant-scoped "open a connection for this
    /// request" pattern: the provider resolves and returns the connection (already open or not).
    /// </summary>
    /// <param name="connectionProvider">Produces the connection for this unit of work (e.g. a tenant-scoped factory).</param>
    /// <param name="registry">The shared catalog of repository factories.</param>
    /// <param name="beginTransaction">
    /// When <see langword="true"/>, begins a transaction before returning. Defaults to
    /// <see langword="false"/> — starting the transaction is left to the caller.
    /// </param>
    /// <param name="isolationLevel">Isolation level for the transaction, when one is begun.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open unit of work owning the resolved connection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connectionProvider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The provider returned null.</exception>
    public static async Task<AdoUnitOfWork> OpenAsync(
        Func<CancellationToken, Task<DbConnection>> connectionProvider,
        RepositoryRegistry registry,
        bool beginTransaction = false,
        IsolationLevel isolationLevel = IsolationLevel.Unspecified,
        CancellationToken cancellationToken = default)
    {
        if (connectionProvider is null) throw new ArgumentNullException(nameof(connectionProvider));

        var connection = await connectionProvider(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The connection provider returned null.");

        var uow = new AdoUnitOfWork(connection, registry, ownsConnection: true, isolationLevel);
        try
        {
            await uow.EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
            if (beginTransaction)
                await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
            return uow;
        }
        catch
        {
            await uow.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public bool HasActiveTransaction => _transaction is not null;

    /// <inheritdoc/>
    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already active.");

        await EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
        _transaction = _isolationLevel == IsolationLevel.Unspecified
            ? await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : await _connection.BeginTransactionAsync(_isolationLevel, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to commit.");

        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to roll back.");

        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }

    /// <inheritdoc/>
    public TRepo Repo<TRepo>() where TRepo : class
    {
        ThrowIfDisposed();
        if (_connection.State != ConnectionState.Open)
            throw new InvalidOperationException(
                "The connection is not open. Call BeginAsync (or open the connection) before resolving repositories.");

        var type = typeof(TRepo);
        if (_repositories.TryGetValue(type, out var cached))
            return (TRepo)cached;

        var factory = _registry.Resolve(type);
        var repository = factory(_connection, () => _transaction);
        _repositories[type] = repository;
        return (TRepo)repository;
    }

    /// <summary>
    /// Rolls back any active transaction (best effort) and disposes the transaction, then disposes the
    /// connection when this unit of work owns it. Safe to call more than once.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_transaction is not null)
        {
            try { await _transaction.RollbackAsync().ConfigureAwait(false); }
            catch { /* best effort — the transaction may already be completed */ }
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }

        if (_ownsConnection)
            await _connection.DisposeAsync().ConfigureAwait(false);

        _repositories.Clear();
    }

    private async Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdoUnitOfWork));
    }
}
