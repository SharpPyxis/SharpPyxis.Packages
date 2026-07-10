namespace SharpPyxis.UnitOfWork.Ado;

/// <summary>
/// A catalog of repository factories, populated once at startup and reused by every unit of work.
/// Each factory receives the unit of work's connection and a late-bound transaction accessor and
/// returns a repository instance — no reflection is ever used, and the single internal cast in
/// <see cref="AdoUnitOfWork.Repo{TRepo}"/> is guaranteed correct by the registration site.
/// </summary>
/// <remarks>
/// Populate the registry once during application start-up, then share the same instance across units
/// of work. Reads are safe from multiple threads once population is complete; do not call
/// <see cref="Add{TRepo}"/> once the registry is being used concurrently.
/// </remarks>
public sealed class RepositoryRegistry
{
    private readonly Dictionary<Type, RepositoryFactory<object>> _factories = new();

    /// <summary>
    /// Registers the factory that builds <typeparamref name="TRepo"/>. Returns the same registry so
    /// registrations can be chained.
    /// </summary>
    /// <typeparam name="TRepo">The repository contract to register.</typeparam>
    /// <param name="factory">Builds the repository from a connection and a transaction accessor.</param>
    /// <returns>This registry, for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A factory is already registered for <typeparamref name="TRepo"/>.</exception>
    public RepositoryRegistry Add<TRepo>(RepositoryFactory<TRepo> factory) where TRepo : class
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        var type = typeof(TRepo);

        // Covariant conversion: RepositoryFactory<TRepo> is a RepositoryFactory<object> since TRepo : class.
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
    internal RepositoryFactory<object> Resolve(Type repositoryType) =>
        _factories.TryGetValue(repositoryType, out var factory)
            ? factory
            : throw new RepositoryNotRegisteredException(repositoryType);
}
