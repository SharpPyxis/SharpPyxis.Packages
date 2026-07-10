namespace SharpPyxis.UnitOfWork;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.Repo{TRepo}"/> when the requested repository type has no
/// registered factory. Signals a composition gap — the intent is to fail fast on first use.
/// </summary>
public sealed class RepositoryNotRegisteredException : InvalidOperationException
{
    /// <summary>The repository type that was requested but not registered.</summary>
    public Type RepositoryType { get; }

    /// <summary>Initializes the exception for <paramref name="repositoryType"/>.</summary>
    /// <param name="repositoryType">The repository type that could not be resolved.</param>
    public RepositoryNotRegisteredException(Type repositoryType)
        : base($"No factory is registered for repository type '{repositoryType?.FullName}'. " +
               "Register it on the RepositoryRegistry at startup before resolving it.")
    {
        RepositoryType = repositoryType!;
    }
}
