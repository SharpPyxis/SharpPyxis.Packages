using SharpPyxis.UnitOfWork.InMemory;
using SharpPyxis.UnitOfWork.Tests.Support;
using Xunit;

namespace SharpPyxis.UnitOfWork.Tests;

public sealed class InMemoryUnitOfWorkTests
{
    private static InMemoryRepositoryRegistry SingletonRegistry(out InMemoryWidgetRepository shared)
    {
        shared = new InMemoryWidgetRepository();
        var captured = shared;
        return new InMemoryRepositoryRegistry().Add<IWidgetRepository>(() => captured);
    }

    [Fact]
    public async Task Commit_keeps_changes_visible_to_a_later_unit_of_work()
    {
        var registry = SingletonRegistry(out _);
        var factory = new InMemoryUnitOfWorkFactory(registry);

        await using (var uow = await factory.OpenAndBeginAsync())
        {
            await uow.Repo<IWidgetRepository>().InsertAsync("alpha");
            await uow.CommitAsync();
        }

        await using var reader = await factory.OpenAsync();
        Assert.Equal(1, await reader.Repo<IWidgetRepository>().CountAsync());
    }

    [Fact]
    public async Task Rollback_does_not_undo_changes()
    {
        // Documents the deliberate limitation: there is no snapshot, so rollback only ends the
        // "transaction" flag — the mutation already applied to the repository stays.
        var registry = SingletonRegistry(out _);
        await using var uow = new InMemoryUnitOfWork(registry);

        await uow.BeginAsync();
        await uow.Repo<IWidgetRepository>().InsertAsync("alpha");
        await uow.RollbackAsync();

        Assert.False(uow.HasActiveTransaction);
        Assert.Equal(1, await uow.Repo<IWidgetRepository>().CountAsync());
    }

    [Fact]
    public async Task Repo_is_cached_returning_the_same_instance()
    {
        var registry = SingletonRegistry(out _);
        await using var uow = new InMemoryUnitOfWork(registry);

        var first = uow.Repo<IWidgetRepository>();
        var second = uow.Repo<IWidgetRepository>();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Repo_resolves_without_beginning_a_transaction()
    {
        var registry = SingletonRegistry(out _);
        await using var uow = new InMemoryUnitOfWork(registry);

        Assert.False(uow.HasActiveTransaction);
        Assert.Equal(0, await uow.Repo<IWidgetRepository>().CountAsync());
    }

    [Fact]
    public async Task Repo_unregistered_type_fails_fast()
    {
        await using var uow = new InMemoryUnitOfWork(new InMemoryRepositoryRegistry());

        var ex = Assert.Throws<RepositoryNotRegisteredException>(() => uow.Repo<IWidgetRepository>());
        Assert.Equal(typeof(IWidgetRepository), ex.RepositoryType);
    }

    [Fact]
    public async Task Begin_twice_throws()
    {
        await using var uow = new InMemoryUnitOfWork(SingletonRegistry(out _));

        await uow.BeginAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.BeginAsync());
    }

    [Fact]
    public async Task Commit_without_begin_throws()
    {
        await using var uow = new InMemoryUnitOfWork(SingletonRegistry(out _));

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CommitAsync());
    }

    [Fact]
    public async Task Rollback_without_begin_throws()
    {
        await using var uow = new InMemoryUnitOfWork(SingletonRegistry(out _));

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.RollbackAsync());
    }

    [Fact]
    public async Task HasActiveTransaction_tracks_lifecycle()
    {
        await using var uow = new InMemoryUnitOfWork(SingletonRegistry(out _));

        Assert.False(uow.HasActiveTransaction);
        await uow.BeginAsync();
        Assert.True(uow.HasActiveTransaction);
        await uow.CommitAsync();
        Assert.False(uow.HasActiveTransaction);
    }

    [Fact]
    public async Task Repo_after_dispose_throws()
    {
        var uow = new InMemoryUnitOfWork(SingletonRegistry(out _));
        await uow.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => uow.Repo<IWidgetRepository>());
    }

    [Fact]
    public async Task Singleton_factory_shares_state_across_units_of_work()
    {
        var registry = SingletonRegistry(out _);
        var factory = new InMemoryUnitOfWorkFactory(registry);

        await using (var first = await factory.OpenAsync())
            await first.Repo<IWidgetRepository>().InsertAsync("alpha");

        await using var second = await factory.OpenAsync();
        Assert.Equal(1, await second.Repo<IWidgetRepository>().CountAsync());
    }

    [Fact]
    public async Task Transient_factory_isolates_state_across_units_of_work()
    {
        // A fresh instance per resolution: state does not leak from one unit of work to the next.
        var registry = new InMemoryRepositoryRegistry()
            .Add<IWidgetRepository>(() => new InMemoryWidgetRepository());
        var factory = new InMemoryUnitOfWorkFactory(registry);

        await using (var first = await factory.OpenAsync())
            await first.Repo<IWidgetRepository>().InsertAsync("alpha");

        await using var second = await factory.OpenAsync();
        Assert.Equal(0, await second.Repo<IWidgetRepository>().CountAsync());
    }

    [Fact]
    public async Task Factory_OpenAndBeginAsync_starts_a_transaction()
    {
        var factory = new InMemoryUnitOfWorkFactory(SingletonRegistry(out _));

        await using var uow = await factory.OpenAndBeginAsync();

        Assert.True(uow.HasActiveTransaction);
    }

    [Fact]
    public async Task Factory_OpenAsync_does_not_start_a_transaction()
    {
        var factory = new InMemoryUnitOfWorkFactory(SingletonRegistry(out _));

        await using var uow = await factory.OpenAsync();

        Assert.False(uow.HasActiveTransaction);
    }
}
