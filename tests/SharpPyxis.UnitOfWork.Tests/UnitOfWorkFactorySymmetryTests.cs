using SharpPyxis.UnitOfWork.Ado;
using SharpPyxis.UnitOfWork.InMemory;
using SharpPyxis.UnitOfWork.Tests.Support;
using Xunit;

namespace SharpPyxis.UnitOfWork.Tests;

/// <summary>
/// The whole point of the generic switch: one piece of consuming code, driven only by
/// <see cref="IUnitOfWorkFactory"/>, behaves the same whether the factory is relational or in-memory.
/// The scenario below is written once and exercised against both.
/// </summary>
public sealed class UnitOfWorkFactorySymmetryTests
{
    // Consumer code that depends on nothing but the generic factory.
    private static async Task WriteTwoThenReadBack(IUnitOfWorkFactory factory)
    {
        await using (var uow = await factory.OpenAndBeginAsync())
        {
            await uow.Repo<IWidgetRepository>().InsertAsync("alpha");
            await uow.Repo<IWidgetRepository>().InsertAsync("beta");
            await uow.CommitAsync();
        }

        await using var reader = await factory.OpenAsync();
        Assert.Equal(2, await reader.Repo<IWidgetRepository>().CountAsync());
    }

    [Fact]
    public async Task Relational_factory_runs_the_scenario()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var registry = new RepositoryRegistry()
            .Add<IWidgetRepository>((c, tx) => new WidgetRepository(c, tx));
        var factory = new AdoUnitOfWorkFactory(db.ProvideConnectionAsync, registry);

        await WriteTwoThenReadBack(factory);
    }

    [Fact]
    public async Task InMemory_factory_runs_the_same_scenario()
    {
        var shared = new InMemoryWidgetRepository();
        var registry = new InMemoryRepositoryRegistry()
            .Add<IWidgetRepository>(() => shared);
        var factory = new InMemoryUnitOfWorkFactory(registry);

        await WriteTwoThenReadBack(factory);
    }
}
