namespace Innovayse.Domain.Tests.Products;

using Innovayse.Domain.Products;

/// <summary>Unit tests for the <see cref="ProductFeature"/> entity.</summary>
public class ProductFeatureTests
{
    /// <summary>Create keeps the product, label, value and position.</summary>
    [Fact]
    public void Create_SetsAllFields()
    {
        var feature = ProductFeature.Create(7, "Disk", "10 GB", 2);

        Assert.Equal(7, feature.ProductId);
        Assert.Equal("Disk", feature.Label);
        Assert.Equal("10 GB", feature.Value);
        Assert.Equal(2, feature.SortOrder);
    }

    /// <summary>
    /// Surrounding whitespace is stripped. The storefront lines products up by
    /// label, so " Disk" and "Disk" arriving as different rows would split a
    /// comparison row in two for no reason an operator could see.
    /// </summary>
    [Fact]
    public void Create_TrimsLabelAndValue()
    {
        var feature = ProductFeature.Create(1, "  Disk  ", "  10 GB  ", 0);

        Assert.Equal("Disk", feature.Label);
        Assert.Equal("10 GB", feature.Value);
    }

    /// <summary>A blank label or value is rejected rather than stored.</summary>
    /// <param name="label">Label under test.</param>
    /// <param name="value">Value under test.</param>
    [Theory]
    [InlineData("", "10 GB")]
    [InlineData("   ", "10 GB")]
    [InlineData("Disk", "")]
    [InlineData("Disk", "   ")]
    public void Create_RejectsBlankLabelOrValue(string label, string value) =>
        Assert.Throws<ArgumentException>(() => ProductFeature.Create(1, label, value, 0));

    /// <summary>Update replaces the label, value and position in place.</summary>
    [Fact]
    public void Update_ReplacesFields()
    {
        var feature = ProductFeature.Create(1, "Disk", "10 GB", 0);

        feature.Update("Storage", "50 GB", 3);

        Assert.Equal("Storage", feature.Label);
        Assert.Equal("50 GB", feature.Value);
        Assert.Equal(3, feature.SortOrder);
        Assert.Equal(1, feature.ProductId);
    }

    /// <summary>Update applies the same blank guard as creation.</summary>
    [Fact]
    public void Update_RejectsBlankValue()
    {
        var feature = ProductFeature.Create(1, "Disk", "10 GB", 0);

        Assert.Throws<ArgumentException>(() => feature.Update("Disk", "  ", 0));
        Assert.Equal("10 GB", feature.Value);
    }
}
