namespace SharpPyxis.UnitOfWork.InMemory;

/// <summary>
/// An in-memory unit of work that resolves repositories from an <see cref="InMemoryRepositoryRegistry"/>.
/// It mirrors the transaction <b>lifecycle</b> of the relational <c>AdoUnitOfWork</c>
/// (SharpPyxis.UnitOfWork.Ado) — <see cref="BeginAsync"/> / <see cref="CommitAsync"/> /
/// <see cref="RollbackAsync"/> follow the same rules and <see cref="HasActiveTransaction"/> tracks the
/// same way — so a service written against <see cref="IUnitOfWork"/> is substitutable between the two.
/// Intended for demos, prototyping, and tests.
/// </summary>
/// <remarks>
/// <b>There is no real transaction.</b> <see cref="CommitAsync"/> has no effect, and — critically —
/// <see cref="RollbackAsync"/> does <b>not</b> undo mutations already applied to the repositories: they
/// hold live objects, and no snapshot is taken. This implementation gives you the same call shape as the
/// relational one, not its isolation or atomicity. Do not rely on it where rollback semantics matter.
/// </remarks>
/// <param name="registry">The shared catalog of in-memory repository factories.</param>
public sealed class InMemoryUnitOfWork(InMemoryRepositoryRegistry registry) : IUnitOfWork
{
    private readonly InMemoryRepositoryRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly Dictionary<Type, object> _repositories = new();

    private bool _hasActiveTransaction;
    private bool _disposed;

    /// <inheritdoc/>
    public bool HasActiveTransaction => _hasActiveTransaction;

    /// <inheritdoc/>
    /// <remarks>Marks a transaction as active for lifecycle symmetry with the relational unit of work; no real transaction is started.</remarks>
    public Task BeginAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_hasActiveTransaction)
            throw new InvalidOperationException("A transaction is already active.");

        _hasActiveTransaction = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>Ends the active transaction with no effect on the data — in-memory repositories keep whatever was written.</remarks>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_hasActiveTransaction)
            throw new InvalidOperationException("No active transaction to commit.");

        _hasActiveTransaction = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>Ends the active transaction but does <b>not</b> undo mutations — there is no snapshot to restore.</remarks>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_hasActiveTransaction)
            throw new InvalidOperationException("No active transaction to roll back.");

        _hasActiveTransaction = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public TRepo Repo<TRepo>() where TRepo : class
    {
        ThrowIfDisposed();

        var type = typeof(TRepo);
        if (_repositories.TryGetValue(type, out var cached))
            return (TRepo)cached;

        var factory = _registry.Resolve(type);
        var repository = factory();
        _repositories[type] = repository;
        return (TRepo)repository;
    }

    /// <summary>
    /// Drops this unit of work's repository cache and marks it disposed. The repositories themselves are
    /// owned by the registry's factories (often shared singletons) and are never disposed here. Safe to
    /// call more than once.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _hasActiveTransaction = false;
        _repositories.Clear();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InMemoryUnitOfWork));
    }
}
