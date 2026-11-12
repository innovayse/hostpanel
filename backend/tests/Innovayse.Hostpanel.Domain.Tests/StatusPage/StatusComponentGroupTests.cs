using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.StatusPage;

/// <summary>Unit tests for the <see cref="StatusComponentGroup"/> aggregate and the <see cref="StatusComponent"/> it creates.</summary>
public sealed class StatusComponentGroupTests
{
    /// <summary>Create stores the name and the order and starts with no components.</summary>
    [Fact]
    public void Create_StoresFields_AndStartsWithoutComponents()
    {
        var group = StatusComponentGroup.Create("Hosting servers", 3);

        Assert.Equal("Hosting servers", group.Name);
        Assert.Equal(3, group.DisplayOrder);
        Assert.Empty(group.Components);
    }

    /// <summary>A group needs a name.</summary>
    /// <param name="name">A name that is empty or whitespace.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Throws(string name)
    {
        var ex = Assert.Throws<DomainRuleException>(() => StatusComponentGroup.Create(name, 0));

        Assert.Equal(StatusPageErrors.NameRequired, ex.Code);
    }

    /// <summary>A display order cannot be negative.</summary>
    [Fact]
    public void Create_WithNegativeOrder_Throws()
    {
        var ex = Assert.Throws<DomainRuleException>(() => StatusComponentGroup.Create("Network", -1));

        Assert.Equal(StatusPageErrors.DisplayOrderNegative, ex.Code);
    }

    /// <summary>A component added to a group starts operational and keeps its fields.</summary>
    [Fact]
    public void AddComponent_StartsOperational_AndKeepsItsFields()
    {
        var group = StatusComponentGroup.Create("Network", 0);

        var component = group.AddComponent("Edge routers", "Public routing", 2);

        Assert.Same(component, Assert.Single(group.Components));
        Assert.Equal("Edge routers", component.Name);
        Assert.Equal("Public routing", component.Description);
        Assert.Equal(2, component.DisplayOrder);
        Assert.Equal(StatusComponentState.Operational, component.CurrentState);
    }

    /// <summary>A component needs a name and a non-negative order, like the group.</summary>
    [Fact]
    public void AddComponent_WithBadInput_Throws()
    {
        var group = StatusComponentGroup.Create("Network", 0);

        Assert.Equal(StatusPageErrors.NameRequired, Assert.Throws<DomainRuleException>(() => group.AddComponent(" ", null, 0)).Code);
        Assert.Equal(StatusPageErrors.DisplayOrderNegative, Assert.Throws<DomainRuleException>(() => group.AddComponent("Edge", null, -1)).Code);
        Assert.Empty(group.Components);
    }
}
