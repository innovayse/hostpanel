using Innovayse.Hostpanel.Domain.Products;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>Unit tests for the <see cref="ProductBundleItem"/> entity.</summary>
public sealed class ProductBundleItemTests
{
    /// <summary>Create keeps the product and the primary flag.</summary>
    [Fact]
    public void Create_SetsProductAndPrimary()
    {
        var item = ProductBundleItem.Create(7, true);

        item.ProductId.Should().Be(7);
        item.IsPrimary.Should().BeTrue();
    }

    /// <summary>A product id that is not positive is refused.</summary>
    /// <param name="productId">The id under test.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_RefusesAnIdThatIsNotPositive(int productId)
    {
        var act = () => ProductBundleItem.Create(productId, false);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>SetPrimary flips the flag.</summary>
    [Fact]
    public void SetPrimary_ChangesTheFlag()
    {
        var item = ProductBundleItem.Create(1, true);

        item.SetPrimary(false);

        item.IsPrimary.Should().BeFalse();
    }
}
