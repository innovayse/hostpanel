namespace Innovayse.Domain.Tests.Products;

using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Events;

/// <summary>Unit tests for the <see cref="Product"/> aggregate.</summary>
public class ProductTests
{
    /// <summary>Create sets all properties and raises ProductCreatedEvent.</summary>
    [Fact]
    public void Create_SetsAllProperties_AndRaisesEvent()
    {
        var product = Product.Create(
            groupId: 1,
            name: "Starter",
            description: "Entry plan",
            website: null,
            type: ProductType.SharedHosting,
            monthlyPrice: 5.99m,
            annualPrice: 59.99m);

        Assert.Equal(1, product.GroupId);
        Assert.Equal("Starter", product.Name);
        Assert.Equal(ProductType.SharedHosting, product.Type);
        Assert.Equal(ProductStatus.Active, product.Status);
        Assert.Equal(5.99m, product.MonthlyPrice);
        Assert.Equal(59.99m, product.AnnualPrice);
        Assert.Single(product.DomainEvents);
        Assert.IsType<ProductCreatedEvent>(product.DomainEvents[0]);
    }

    /// <summary>Update changes name and prices.</summary>
    [Fact]
    public void Update_ChangesNameAndPrices()
    {
        var product = Product.Create(1, "Old", null, null, ProductType.SharedHosting, 5m, 50m);
        product.Update("New", "desc", null, 9m, 90m);
        Assert.Equal("New", product.Name);
        Assert.Equal(9m, product.MonthlyPrice);
        Assert.Equal(90m, product.AnnualPrice);
    }

    /// <summary>Deactivate changes status to Inactive.</summary>
    [Fact]
    public void Deactivate_ChangesStatus()
    {
        var product = Product.Create(1, "P", null, null, ProductType.SharedHosting, 5m, 50m);
        product.Deactivate();
        Assert.Equal(ProductStatus.Inactive, product.Status);
    }
}
