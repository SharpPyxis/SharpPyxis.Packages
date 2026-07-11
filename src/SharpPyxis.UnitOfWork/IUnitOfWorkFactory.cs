namespace SharpPyxis.UnitOfWork;

/// <summary>
/// Creates <see cref="IUnitOfWork"/> instances, one per operation. This is the single, storage-agnostic
/// seam a service depends on to stay independent of how its unit of work is backed: a relational
/// implementation (over ADO.NET) and an in-memory one both satisfy it, so switching between them is a
/// composition-root decision, not a change to consuming code.
/// </summary>
/// <remarks>
/// Register one factory at startup (e.g. as a singleton) and open a unit of work per operation.
/// Whatever the backing store, <see cref="IUnitOfWork.Repo{TRepo}"/> resolves repositories the same way,
/// so a service written against this interface reads and writes identically in both modes.
/// </remarks>
public interface IUnitOfWorkFactory
{
    /// <summary>Opens a unit of work, ready to resolve repositories. No transaction is started.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open unit of work.</returns>
    Task<IUnitOfWork> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a unit of work and begins a transaction before returning it.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open unit of work with an active transaction.</returns>
    Task<IUnitOfWork> OpenAndBeginAsync(CancellationToken cancellationToken = default);
}
