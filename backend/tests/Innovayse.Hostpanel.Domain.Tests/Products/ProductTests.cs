using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Products.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>Unit tests for the <see cref="Product"/> aggregate.</summary>
public class ProductTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Create sets all properties and raises ProductCreatedEvent.</summary>
    [Fact]
    public void Create_SetsAllProperties_AndRaisesEvent()
    {
        var product = Product.Create(
            groupId: 1,
            name: "Starter",
            description: "Entry plan",
            website: null,
            slug: null,
            packageName: null,
            type: ProductType.SharedHosting,
            monthlyPrice: 5.99m,
            annualPrice: 59.99m,
            now: Now);

        Assert.Equal(1, product.GroupId);
        Assert.Equal("Starter", product.Name);
        Assert.Equal(ProductType.SharedHosting, product.Type);
        Assert.Equal(ProductStatus.Active, product.Status);
        Assert.Equal(5.99m, product.MonthlyPrice);
        Assert.Equal(59.99m, product.AnnualPrice);
        Assert.Single(product.DomainEvents);
        var created = Assert.IsType<ProductCreatedEvent>(product.DomainEvents.Single());
        Assert.Equal(Now, created.OccurredAt);
    }

    /// <summary>Update changes name and prices.</summary>
    [Fact]
    public void Update_ChangesNameAndPrices()
    {
        var product = Product.Create(1, "Old", null, null, null, null, ProductType.SharedHosting, 5m, 50m, Now);
        product.Update("New", "desc", null, null, null, 9m, 90m, null);
        Assert.Equal("New", product.Name);
        Assert.Equal(9m, product.MonthlyPrice);
        Assert.Equal(90m, product.AnnualPrice);
    }

    /// <summary>Deactivate changes status to Inactive.</summary>
    [Fact]
    public void Deactivate_ChangesStatus()
    {
        var product = Product.Create(1, "P", null, null, null, null, ProductType.SharedHosting, 5m, 50m, Now);
        product.Deactivate();
        Assert.Equal(ProductStatus.Inactive, product.Status);
    }

    /// <summary>Create with ServerGroupId sets the property.</summary>
    [Fact]
    public void Create_WithServerGroupId_SetsProperty()
    {
        var product = Product.Create(1, "Linked", null, null, null, null,
            ProductType.SharedHosting, 5m, 50m, Now, serverGroupId: 42);

        Assert.Equal(42, product.ServerGroupId);
    }

    /// <summary>Create without ServerGroupId defaults to null.</summary>
    [Fact]
    public void Create_WithoutServerGroupId_DefaultsToNull()
    {
        var product = Product.Create(1, "Unlinked", null, null, null, null,
            ProductType.SharedHosting, 5m, 50m, Now);

        Assert.Null(product.ServerGroupId);
    }

    /// <summary>Update with ServerGroupId changes the value.</summary>
    [Fact]
    public void Update_WithServerGroupId_ChangesValue()
    {
        var product = Product.Create(1, "P", null, null, null, null, ProductType.SharedHosting, 5m, 50m, Now);
        Assert.Null(product.ServerGroupId);

        product.Update("P", null, null, null, null, 5m, 50m, serverGroupId: 7);

        Assert.Equal(7, product.ServerGroupId);
    }

    /// <summary>A new product is neither featured nor ordered until the operator says so.</summary>
    [Fact]
    public void Create_Defaults_NotFeaturedAndSortOrderZero()
    {
        var product = Product.Create(1, "P", null, null, null, null, ProductType.SharedHosting, 5m, 50m, Now);

        Assert.False(product.IsFeatured);
        Assert.Equal(0, product.SortOrder);
    }

    /// <summary>Create carries the featured flag and the sort order it is given.</summary>
    [Fact]
    public void Create_WithFeaturedAndSortOrder_SetsBoth()
    {
        var product = Product.Create(1, "P", null, null, null, null, ProductType.SharedHosting, 5m, 50m,
            Now,
            isFeatured: true, sortOrder: 3);

        Assert.True(product.IsFeatured);
        Assert.Equal(3, product.SortOrder);
    }

    /// <summary>Feature and Unfeature toggle the recommended-plan mark.</summary>
    [Fact]
    public void FeatureAndUnfeature_ToggleTheFlag()
    {
        var product = Product.Create(1, "P", null, null, null, null, ProductType.SharedHosting, 5m, 50m, Now);

        product.Feature();
        Assert.True(product.IsFeatured);

        product.Unfeature();
        Assert.False(product.IsFeatured);
    }

    /// <summary>SetSortOrder moves the product inside its group.</summary>
    [Fact]
    public void SetSortOrder_ChangesThePosition()
    {
        var product = Product.Create(1, "P", null, null, null, null, ProductType.SharedHosting, 5m, 50m, Now);

        product.SetSortOrder(9);

        Assert.Equal(9, product.SortOrder);
    }

    /// <summary>Update applies the featured flag and sort order when given.</summary>
    [Fact]
    public void Update_WithFeaturedAndSortOrder_ChangesBoth()
    {
        var product = Product.Create(1, "P", null, null, null, null, ProductType.SharedHosting, 5m, 50m, Now);

        product.Update("P", null, null, null, null, 5m, 50m, null, isFeatured: true, sortOrder: 4);

        Assert.True(product.IsFeatured);
        Assert.Equal(4, product.SortOrder);
    }

    /// <summary>Update leaves the featured flag and sort order alone when they are not given.</summary>
    [Fact]
    public void Update_WithoutFeaturedAndSortOrder_KeepsBoth()
    {
        var product = Product.Create(1, "P", null, null, null, null, ProductType.SharedHosting, 5m, 50m,
            Now,
            isFeatured: true, sortOrder: 6);

        product.Update("P2", null, null, null, null, 5m, 50m, null);

        Assert.True(product.IsFeatured);
        Assert.Equal(6, product.SortOrder);
    }

    /// <summary>The Digital type keeps its own number after the existing values, which are not renumbered.</summary>
    [Fact]
    public void ProductType_Digital_IsAppendedWithoutRenumbering()
    {
        Assert.Equal(0, (int)ProductType.SharedHosting);
        Assert.Equal(5, (int)ProductType.Other);
        Assert.Equal(8, (int)ProductType.Backup);
        Assert.Equal(9, (int)ProductType.Digital);
    }

    /// <summary>IsDigital is true only for a product of the Digital type.</summary>
    /// <param name="type">The product type.</param>
    /// <param name="expected">Whether the product counts as digital.</param>
    [Theory]
    [InlineData(ProductType.Digital, true)]
    [InlineData(ProductType.SharedHosting, false)]
    [InlineData(ProductType.Addon, false)]
    [InlineData(ProductType.Other, false)]
    public void IsDigital_IsTrueOnlyForTheDigitalType(ProductType type, bool expected)
    {
        var product = NewProduct(type);

        Assert.Equal(expected, product.IsDigital);
    }

    /// <summary>A new product has no SKU, no metadata and the default grace period of seven days.</summary>
    [Fact]
    public void Create_StartsWithoutSkuOrMetadata_AndWithTheDefaultGracePeriod()
    {
        var product = NewProduct(ProductType.Digital);

        Assert.Null(product.Sku);
        Assert.Null(product.Metadata);
        Assert.Equal(7, Product.DefaultGracePeriodDays);
        Assert.Equal(Product.DefaultGracePeriodDays, product.GracePeriodDays);
    }

    /// <summary>SetSku trims and upper-cases the value before it stores it.</summary>
    [Fact]
    public void SetSku_StoresTheNormalisedValue()
    {
        var product = NewProduct(ProductType.Digital);

        product.SetSku("  lic-pro_1y.v2 ");

        Assert.Equal("LIC-PRO_1Y.V2", product.Sku);
    }

    /// <summary>SetSku with null or white space clears a SKU set earlier.</summary>
    /// <param name="sku">The clearing value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetSku_WithNothing_ClearsTheSku(string? sku)
    {
        var product = NewProduct(ProductType.Digital);
        product.SetSku("CREDITS-100");

        product.SetSku(sku);

        Assert.Null(product.Sku);
    }

    /// <summary>SetSku refuses a value that breaks the format and keeps the SKU it had.</summary>
    /// <param name="sku">A malformed SKU.</param>
    [Theory]
    [InlineData("LIC PRO")]
    [InlineData("LIC--PRO")]
    [InlineData("-LIC")]
    [InlineData("LIC-")]
    [InlineData("LIC/PRO")]
    [InlineData("ЛИЦ-1")]
    public void SetSku_WithAMalformedValue_ThrowsAndKeepsTheOldSku(string sku)
    {
        var product = NewProduct(ProductType.Digital);
        product.SetSku("CREDITS-100");

        var ex = Assert.Throws<DomainRuleException>(() => product.SetSku(sku));

        Assert.Equal(ProductsErrors.ProductSkuInvalid, ex.Code);
        Assert.Equal("CREDITS-100", product.Sku);
    }

    /// <summary>SetSku accepts a SKU of exactly the maximum length and refuses one character more.</summary>
    [Fact]
    public void SetSku_EnforcesTheMaximumLength()
    {
        var product = NewProduct(ProductType.Digital);

        product.SetSku(new string('a', ProductSku.MaxLength));
        var ex = Assert.Throws<DomainRuleException>(() => product.SetSku(new string('A', ProductSku.MaxLength + 1)));

        Assert.Equal(new string('A', ProductSku.MaxLength), product.Sku);
        Assert.Equal(ProductsErrors.ProductSkuInvalid, ex.Code);
    }

    /// <summary>SetMetadata stores a JSON object in compact form.</summary>
    [Fact]
    public void SetMetadata_WithAnObject_StoresItCompact()
    {
        var product = NewProduct(ProductType.Digital);

        product.SetMetadata("{ \"tier\": \"pro\", \"credits\": 100, \"features\": [\"a\", \"b\"] }");

        Assert.Equal("{\"tier\":\"pro\",\"credits\":100,\"features\":[\"a\",\"b\"]}", product.Metadata);
    }

    /// <summary>SetMetadata accepts the empty object.</summary>
    [Fact]
    public void SetMetadata_WithTheEmptyObject_StoresIt()
    {
        var product = NewProduct(ProductType.Digital);

        product.SetMetadata("{}");

        Assert.Equal("{}", product.Metadata);
    }

    /// <summary>SetMetadata with null or white space clears the bag.</summary>
    /// <param name="metadata">The clearing value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void SetMetadata_WithNothing_ClearsTheBag(string? metadata)
    {
        var product = NewProduct(ProductType.Digital);
        product.SetMetadata("{\"tier\":\"pro\"}");

        product.SetMetadata(metadata);

        Assert.Null(product.Metadata);
    }

    /// <summary>SetMetadata refuses anything that is not a JSON object and keeps the bag it had.</summary>
    /// <param name="metadata">Text that is not a JSON object.</param>
    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{\"tier\": }")]
    [InlineData("not json")]
    public void SetMetadata_WithANonObject_ThrowsAndKeepsTheOldBag(string metadata)
    {
        var product = NewProduct(ProductType.Digital);
        product.SetMetadata("{\"tier\":\"pro\"}");

        var ex = Assert.Throws<DomainRuleException>(() => product.SetMetadata(metadata));

        Assert.Equal(ProductsErrors.ProductMetadataInvalid, ex.Code);
        Assert.Equal("{\"tier\":\"pro\"}", product.Metadata);
    }

    /// <summary>IsValidMetadata answers true only for a JSON object.</summary>
    /// <param name="metadata">The candidate text.</param>
    /// <param name="expected">Whether it is a JSON object.</param>
    [Theory]
    [InlineData("{\"a\":1}", true)]
    [InlineData("{}", true)]
    [InlineData("[]", false)]
    [InlineData("1", false)]
    [InlineData("{", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidMetadata_AcceptsOnlyAJsonObject(string? metadata, bool expected)
    {
        Assert.Equal(expected, Product.IsValidMetadata(metadata));
    }

    /// <summary>SetGracePeriodDays accepts every value from 0 to 365 inclusive.</summary>
    /// <param name="days">An in-range grace period.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    public void SetGracePeriodDays_InRange_StoresIt(int days)
    {
        var product = NewProduct(ProductType.SharedHosting);

        product.SetGracePeriodDays(days);

        Assert.Equal(days, product.GracePeriodDays);
    }

    /// <summary>SetGracePeriodDays refuses a value outside 0 to 365 and keeps the period it had.</summary>
    /// <param name="days">An out-of-range grace period.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void SetGracePeriodDays_OutOfRange_ThrowsAndKeepsTheOldPeriod(int days)
    {
        var product = NewProduct(ProductType.SharedHosting);

        var ex = Assert.Throws<DomainRuleException>(() => product.SetGracePeriodDays(days));

        Assert.Equal(ProductsErrors.ProductGracePeriodOutOfRange, ex.Code);
        Assert.Equal(Product.DefaultGracePeriodDays, product.GracePeriodDays);
    }

    /// <summary>IsValidGracePeriod draws the boundary at 0 and 365 inclusive.</summary>
    /// <param name="days">The candidate grace period.</param>
    /// <param name="expected">Whether it is in range.</param>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(365, true)]
    [InlineData(366, false)]
    public void IsValidGracePeriod_DrawsTheBoundaryInclusive(int days, bool expected)
    {
        Assert.Equal(expected, Product.IsValidGracePeriod(days));
    }

    /// <summary>Creates a product of the given type with fixed, irrelevant values.</summary>
    /// <param name="type">The product type.</param>
    /// <returns>A new active product.</returns>
    private static Product NewProduct(ProductType type)
    {
        return Product.Create(1, "P", null, null, null, null, type, 5m, 50m, Now);
    }
}
