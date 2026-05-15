namespace Innovayse.Domain.Tests.Products;

using Innovayse.Domain.Products;

/// <summary>Unit tests for the <see cref="ProductGroup"/> aggregate.</summary>
public class ProductGroupTests
{
    /// <summary>Create sets name, description, and activates the group.</summary>
    [Fact]
    public void Create_SetsNameAndDescription()
    {
        var group = ProductGroup.Create("Shared Hosting", "Hosting plans");

        Assert.Equal("Shared Hosting", group.Name);
        Assert.Equal("Hosting plans", group.Description);
        Assert.True(group.IsActive);
        Assert.Empty(group.Products);
    }

    /// <summary>Update changes the name and description.</summary>
    [Fact]
    public void Update_ChangesName()
    {
        var group = ProductGroup.Create("Old", null);
        group.Update("New", "desc");
        Assert.Equal("New", group.Name);
        Assert.Equal("desc", group.Description);
    }

    /// <summary>Deactivate sets IsActive to false.</summary>
    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var group = ProductGroup.Create("G", null);
        group.Deactivate();
        Assert.False(group.IsActive);
    }
}
