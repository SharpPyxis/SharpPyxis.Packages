using System.Data.Common;
using SharpPyxis.UnitOfWork.Ado;
using SharpPyxis.UnitOfWork.Tests.Support;
using Xunit;

namespace SharpPyxis.UnitOfWork.Tests;

public sealed class RepositoryRegistryTests
{
    [Fact]
    public void Add_returns_same_registry_for_chaining()
    {
        var registry = new RepositoryRegistry();
        var returned = registry.Add<IWidgetRepository>((c, tx) => new WidgetRepository(c, tx));
        Assert.Same(registry, returned);
    }

    [Fact]
    public void Add_null_factory_throws()
    {
        var registry = new RepositoryRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.Add<IWidgetRepository>((RepositoryFactory<IWidgetRepository>)null!));
    }

    [Fact]
    public void Add_duplicate_type_throws()
    {
        var registry = new RepositoryRegistry()
            .Add<IWidgetRepository>((c, tx) => new WidgetRepository(c, tx));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Add<IWidgetRepository>((c, tx) => new WidgetRepository(c, tx)));
    }

    [Fact]
    public void Contains_reflects_registration()
    {
        var registry = new RepositoryRegistry();
        Assert.False(registry.Contains<IWidgetRepository>());

        registry.Add<IWidgetRepository>((c, tx) => new WidgetRepository(c, tx));
        Assert.True(registry.Contains<IWidgetRepository>());
    }

    [Fact]
    public void RegisteredTypes_lists_registered_contracts()
    {
        var registry = new RepositoryRegistry()
            .Add<IWidgetRepository>((c, tx) => new WidgetRepository(c, tx));

        Assert.Contains(typeof(IWidgetRepository), registry.RegisteredTypes);
    }
}
