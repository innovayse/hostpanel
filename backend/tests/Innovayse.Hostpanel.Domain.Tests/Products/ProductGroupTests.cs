using Innovayse.Hostpanel.Domain.Products;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

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

    /// <summary>A new group restricts nothing: its allowed set is empty.</summary>
    [Fact]
    public void Create_AllowsEveryPaymentMethod()
    {
        var group = ProductGroup.Create("G", null);

        Assert.Empty(group.AllowedPaymentMethods);
    }

    /// <summary>SetAllowedPaymentMethods trims, drops blanks, keeps a method named twice once and keeps the order given.</summary>
    [Fact]
    public void SetAllowedPaymentMethods_TrimsDropsBlanksAndDeduplicates()
    {
        var group = ProductGroup.Create("G", null);

        group.SetAllowedPaymentMethods([" bank_transfer ", "", "  ", "Gateway-A", "BANK_TRANSFER", "gateway-a"]);

        group.AllowedPaymentMethods.Should().Equal("bank_transfer", "Gateway-A");
    }

    /// <summary>SetAllowedPaymentMethods replaces the earlier set, and an empty list lifts the restriction.</summary>
    [Fact]
    public void SetAllowedPaymentMethods_ReplacesAndCanBeCleared()
    {
        var group = ProductGroup.Create("G", null);
        group.SetAllowedPaymentMethods(["a"]);

        group.SetAllowedPaymentMethods(["b"]);
        group.AllowedPaymentMethods.Should().Equal("b");

        group.SetAllowedPaymentMethods([]);
        Assert.Empty(group.AllowedPaymentMethods);
    }

    /// <summary>No group restricted (or no group at all) leaves every method allowed: the answer is null.</summary>
    [Fact]
    public void CommonAllowedPaymentMethods_WithNoRestriction_IsNull()
    {
        Assert.Null(ProductGroup.CommonAllowedPaymentMethods([]));
        Assert.Null(ProductGroup.CommonAllowedPaymentMethods([ProductGroup.Create("A", null), ProductGroup.Create("B", null)]));
    }

    /// <summary>A group with no restriction takes no part: the restricted group's set stands.</summary>
    [Fact]
    public void CommonAllowedPaymentMethods_IgnoresUnrestrictedGroups()
    {
        var restricted = ProductGroup.Create("A", null);
        restricted.SetAllowedPaymentMethods(["x", "y"]);

        var common = ProductGroup.CommonAllowedPaymentMethods([restricted, ProductGroup.Create("B", null)]);

        common.Should().Equal("x", "y");
    }

    /// <summary>Restricted groups intersect, ignoring case.</summary>
    [Fact]
    public void CommonAllowedPaymentMethods_IntersectsRestrictedGroups()
    {
        var first = ProductGroup.Create("A", null);
        first.SetAllowedPaymentMethods(["x", "y", "z"]);
        var second = ProductGroup.Create("B", null);
        second.SetAllowedPaymentMethods(["Y", "z", "w"]);

        var common = ProductGroup.CommonAllowedPaymentMethods([first, second]);

        common.Should().Equal("y", "z");
    }

    /// <summary>Restricted groups that share nothing give an empty list, which is not the same as no restriction.</summary>
    [Fact]
    public void CommonAllowedPaymentMethods_WithNothingInCommon_IsEmpty()
    {
        var first = ProductGroup.Create("A", null);
        first.SetAllowedPaymentMethods(["x"]);
        var second = ProductGroup.Create("B", null);
        second.SetAllowedPaymentMethods(["y"]);

        var common = ProductGroup.CommonAllowedPaymentMethods([first, second]);

        Assert.NotNull(common);
        Assert.Empty(common);
    }
}
