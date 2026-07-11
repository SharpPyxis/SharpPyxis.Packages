using SharpPyxis.UnitOfWork.InMemory;
using SharpPyxis.UnitOfWork.Tests.Support;
using Xunit;

namespace SharpPyxis.UnitOfWork.Tests;

public sealed class InMemoryRepositoryRegistryTests
{
    [Fact]
    public void Add_returns_same_registry_for_chaining()
    {
        var registry = new InMemoryRepositoryRegistry();
        var returned = registry.Add<IWidgetRepository>(() => new InMemoryWidgetRepository());
        Assert.Same(registry, returned);
    }

    [Fact]
    public void Add_null_factory_throws()
    {
        var registry = new InMemoryRepositoryRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.Add<IWidgetRepository>((Func<IWidgetRepository>)null!));
    }

    [Fact]
    public void Add_duplicate_type_throws()
    {
        var registry = new InMemoryRepositoryRegistry()
            .Add<IWidgetRepository>(() => new InMemoryWidgetRepository());

        Assert.Throws<InvalidOperationException>(() =>
            registry.Add<IWidgetRepository>(() => new InMemoryWidgetRepository()));
    }

    [Fact]
    public void Contains_reflects_registration()
    {
        var registry = new InMemoryRepositoryRegistry();
        Assert.False(registry.Contains<IWidgetRepository>());

        registry.Add<IWidgetRepository>(() => new InMemoryWidgetRepository());
        Assert.True(registry.Contains<IWidgetRepository>());
    }

    [Fact]
    public void RegisteredTypes_lists_registered_contracts()
    {
        var registry = new InMemoryRepositoryRegistry()
            .Add<IWidgetRepository>(() => new InMemoryWidgetRepository());

        Assert.Contains(typeof(IWidgetRepository), registry.RegisteredTypes);
    }
}
