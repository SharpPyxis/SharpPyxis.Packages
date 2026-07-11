namespace SharpPyxis.UnitOfWork.InMemory;

/// <summary>
/// A catalog of in-memory repository factories, populated once at startup and reused by every unit of
/// work. Unlike the relational <c>RepositoryRegistry</c> (SharpPyxis.UnitOfWork.Ado), which binds each
/// factory to a database connection and transaction, an in-memory repository has neither — so its
/// factory is a plain parameterless <see cref="Func{TRepo}"/>. This is why the two modes keep separate
/// registries: the switch between them cannot be reduced to nothing, but it stays generic through
/// <see cref="IUnitOfWorkFactory"/> rather than being wired per domain.
/// </summary>
/// <remarks>
/// <para>
/// Populate the registry once during application start-up, then share the same instance across units of
/// work. Reads are safe from multiple threads once population is complete; do not call
/// <see cref="Add{TRepo}"/> once the registry is being used concurrently.
/// </para>
/// <para>
/// <b>Persistence across units of work.</b> Whether data survives from one unit of work to the next is
/// entirely the factory's choice. Register a <b>singleton</b> — <c>Add&lt;IParts&gt;(() =&gt; sharedParts)</c> —
/// and every unit of work shares one backing store, which is what makes in-memory a realistic stand-in
/// for a database in demos. Return a fresh instance each call for throwaway, per-operation state.
/// </para>
/// </remarks>
public sealed class InMemoryRepositoryRegistry
{
    private readonly Dictionary<Type, Func<object>> _factories = new();

    /// <summary>
    /// Registers the factory that builds <typeparamref name="TRepo"/>. Returns the same registry so
    /// registrations can be chained.
    /// </summary>
    /// <typeparam name="TRepo">The repository contract to register.</typeparam>
    /// <param name="factory">Builds the repository. Capture a shared instance to persist state across units of work.</param>
    /// <returns>This registry, for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A factory is already registered for <typeparamref name="TRepo"/>.</exception>
    public InMemoryRepositoryRegistry Add<TRepo>(Func<TRepo> factory) where TRepo : class
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        var type = typeof(TRepo);

        // Covariant conversion: Func<TRepo> is a Func<object> since TRepo : class.
        if (!_factories.TryAdd(type, factory))
            throw new InvalidOperationException(
                $"A factory is already registered for repository type '{type.FullName}'.");

        return this;
    }

    /// <summary>Whether a factory is registered for <typeparamref name="TRepo"/>.</summary>
    /// <typeparam name="TRepo">The repository contract to look up.</typeparam>
    public bool Contains<TRepo>() where TRepo : class => _factories.ContainsKey(typeof(TRepo));

    /// <summary>The repository types that currently have a registered factory.</summary>
    public IReadOnlyCollection<Type> RegisteredTypes => _factories.Keys;

    /// <summary>Resolves the factory for <paramref name="repositoryType"/>. Used by the unit of work.</summary>
    internal Func<object> Resolve(Type repositoryType) =>
        _factories.TryGetValue(repositoryType, out var factory)
            ? factory
            : throw new RepositoryNotRegisteredException(repositoryType);
}
