namespace SharpPyxis.UnitOfWork;

/// <summary>
/// A transactional unit of work that owns a database connection and, optionally, a transaction,
/// and hands out repositories "à la carte" through <see cref="Repo{TRepo}"/> instead of exposing a
/// fixed set of repository properties.
/// </summary>
/// <remarks>
/// The <b>type</b> returned by <see cref="Repo{TRepo}"/> is checked at compile time — it is a
/// generic method, so callers get full IntelliSense and never cast. The <b>availability</b> of a
/// repository (whether a factory was registered for it) is checked at run time: resolving an
/// unregistered repository fails fast with <see cref="RepositoryNotRegisteredException"/>. A startup
/// health check that asserts every expected factory is registered turns that runtime gap into a
/// boot-time error.
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>Whether a transaction is currently active.</summary>
    bool HasActiveTransaction { get; }

    /// <summary>Begins a transaction. The connection is opened first if it is not already open.</summary>
    Task BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>Commits the active transaction.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls the active transaction back.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the repository of type <typeparamref name="TRepo"/>, built on demand from its
    /// registered factory and cached for the lifetime of this unit of work. Repositories receive the
    /// connection and a late-bound accessor to the current transaction, so a cached repository always
    /// observes the live transaction — including none, or several across begin/commit cycles.
    /// </summary>
    /// <typeparam name="TRepo">The repository contract to resolve.</typeparam>
    /// <returns>The repository instance.</returns>
    /// <exception cref="RepositoryNotRegisteredException">No factory is registered for <typeparamref name="TRepo"/>.</exception>
    TRepo Repo<TRepo>() where TRepo : class;
}
