using SharpPyxis.UnitOfWork.Ado;
using SharpPyxis.UnitOfWork.Tests.Support;
using Xunit;

namespace SharpPyxis.UnitOfWork.Tests;

public sealed class AdoUnitOfWorkTests
{
    private static RepositoryRegistry BuildRegistry() =>
        new RepositoryRegistry().Add<IWidgetRepository>((c, tx) => new WidgetRepository(c, tx));

    [Fact]
    public async Task Commit_persists_changes()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        await using (var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry()))
        {
            await uow.BeginAsync();
            await uow.Repo<IWidgetRepository>().InsertAsync("alpha");
            await uow.CommitAsync();
        }

        Assert.Equal(1, await db.CountWidgetsAsync());
    }

    [Fact]
    public async Task Rollback_discards_changes()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        await using (var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry()))
        {
            await uow.BeginAsync();
            await uow.Repo<IWidgetRepository>().InsertAsync("alpha");
            await uow.RollbackAsync();
        }

        Assert.Equal(0, await db.CountWidgetsAsync());
    }

    [Fact]
    public async Task Dispose_without_commit_rolls_back()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        await using (var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry()))
        {
            await uow.BeginAsync();
            await uow.Repo<IWidgetRepository>().InsertAsync("alpha");
            // No commit — leaving the scope disposes the unit of work and must roll back.
        }

        Assert.Equal(0, await db.CountWidgetsAsync());
    }

    [Fact]
    public async Task Repo_is_cached_returning_the_same_instance()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry());

        var first = uow.Repo<IWidgetRepository>();
        var second = uow.Repo<IWidgetRepository>();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Repo_unregistered_type_fails_fast()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, new RepositoryRegistry());

        var ex = Assert.Throws<RepositoryNotRegisteredException>(() => uow.Repo<IWidgetRepository>());
        Assert.Equal(typeof(IWidgetRepository), ex.RepositoryType);
    }

    [Fact]
    public async Task Repo_reads_without_a_transaction()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        // Seed one row out of band.
        await using (var seed = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry()))
        {
            await seed.BeginAsync();
            await seed.Repo<IWidgetRepository>().InsertAsync("alpha");
            await seed.CommitAsync();
        }

        await using var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry());

        Assert.False(uow.HasActiveTransaction);
        Assert.Equal(1, await uow.Repo<IWidgetRepository>().CountAsync());
    }

    [Fact]
    public async Task Cached_repo_observes_transactions_across_begin_commit_cycles()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry());

        // Resolve the repository before any transaction — its late-bound accessor reads null now.
        var repo = uow.Repo<IWidgetRepository>();

        // First cycle: committed.
        await uow.BeginAsync();
        await repo.InsertAsync("alpha");
        await uow.CommitAsync();

        // Second cycle on the same cached repo: rolled back.
        await uow.BeginAsync();
        await repo.InsertAsync("beta");
        await uow.RollbackAsync();

        Assert.Equal(1, await db.CountWidgetsAsync());
    }

    [Fact]
    public async Task Begin_twice_throws()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry());

        await uow.BeginAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.BeginAsync());
    }

    [Fact]
    public async Task Commit_without_begin_throws()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry());

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CommitAsync());
    }

    [Fact]
    public async Task HasActiveTransaction_tracks_lifecycle()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var uow = await AdoUnitOfWork.OpenAsync(db.ProvideConnectionAsync, BuildRegistry());

        Assert.False(uow.HasActiveTransaction);
        await uow.BeginAsync();
        Assert.True(uow.HasActiveTransaction);
        await uow.CommitAsync();
        Assert.False(uow.HasActiveTransaction);
    }

    [Fact]
    public async Task Repo_on_closed_connection_throws()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        // Direct constructor with an unopened connection and no Begin — resolving must fail clearly.
        await using var uow = new AdoUnitOfWork(db.NewConnection(), BuildRegistry());

        Assert.Throws<InvalidOperationException>(() => uow.Repo<IWidgetRepository>());
    }

    [Fact]
    public async Task Not_owning_the_connection_leaves_it_open_after_dispose()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var connection = await db.NewOpenConnectionAsync();

        var uow = new AdoUnitOfWork(connection, BuildRegistry(), ownsConnection: false);
        await uow.DisposeAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Factory_CreateAndBeginAsync_starts_a_transaction()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var factory = new AdoUnitOfWorkFactory(db.ProvideConnectionAsync, BuildRegistry());

        await using var uow = await factory.OpenAndBeginAsync();

        Assert.True(uow.HasActiveTransaction);
        await uow.Repo<IWidgetRepository>().InsertAsync("alpha");
        await uow.CommitAsync();

        Assert.Equal(1, await db.CountWidgetsAsync());
    }

    [Fact]
    public async Task Factory_CreateAsync_does_not_start_a_transaction()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var factory = new AdoUnitOfWorkFactory(db.ProvideConnectionAsync, BuildRegistry());

        await using var uow = await factory.OpenAsync();

        Assert.False(uow.HasActiveTransaction);
    }
}
