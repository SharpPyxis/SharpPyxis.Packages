namespace SharpPyxis.UnitOfWork.Tests.Support;

/// <summary>
/// An in-memory <see cref="IWidgetRepository"/> backed by a plain list. Registered as a shared singleton
/// it persists across units of work (a realistic database stand-in); registered as a fresh instance per
/// call it gives throwaway, per-operation state. It knows nothing about transactions — the in-memory
/// unit of work's commit/rollback have no effect on it.
/// </summary>
internal sealed class InMemoryWidgetRepository : IWidgetRepository
{
    private readonly List<string> _widgets = [];

    public Task InsertAsync(string name, CancellationToken cancellationToken = default)
    {
        _widgets.Add(name);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_widgets.Count);
}
