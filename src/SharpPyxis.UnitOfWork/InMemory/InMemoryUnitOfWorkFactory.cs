namespace SharpPyxis.UnitOfWork.InMemory;

/// <summary>
/// Creates <see cref="InMemoryUnitOfWork"/> instances from a shared <see cref="InMemoryRepositoryRegistry"/>.
/// This is the in-memory implementation of <see cref="IUnitOfWorkFactory"/>: swap it in for the
/// relational <c>AdoUnitOfWorkFactory</c> (SharpPyxis.UnitOfWork.Ado) at the composition root — for
/// demos, prototyping, or tests — without touching any code that depends on <see cref="IUnitOfWorkFactory"/>.
/// </summary>
/// <param name="registry">The shared catalog of in-memory repository factories.</param>
public sealed class InMemoryUnitOfWorkFactory(InMemoryRepositoryRegistry registry) : IUnitOfWorkFactory
{
    private readonly InMemoryRepositoryRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    /// <inheritdoc/>
    public Task<IUnitOfWork> OpenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWork>(new InMemoryUnitOfWork(_registry));

    /// <inheritdoc/>
    public async Task<IUnitOfWork> OpenAndBeginAsync(CancellationToken cancellationToken = default)
    {
        var uow = new InMemoryUnitOfWork(_registry);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        return uow;
    }
}
