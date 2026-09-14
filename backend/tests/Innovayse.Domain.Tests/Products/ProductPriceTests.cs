namespace Innovayse.Domain.Tests.Products;

using Innovayse.Domain.Products;
using Xunit;

/// <summary>Tests for per-currency prices on <see cref="Product"/>.</summary>
public sealed class ProductPriceTests
{
    private static Product NewProduct() =>
        Product.Create(groupId: 1, name: "Starter", description: null, website: null, slug: null,
            packageName: null, type: ProductType.SharedHosting, monthlyPrice: 2.99m, annualPrice: 29.99m);

    /// <summary>A price set for a currency and cycle is read back for that pair only.</summary>
    [Fact]
    public void SetPrice_ThenPriceFor_ReturnsIt()
    {
        var p = NewProduct();
        p.SetPrice("AMD", BillingCycle.Monthly, 1200m);

        Assert.Equal(1200m, p.PriceFor("AMD", BillingCycle.Monthly));
        Assert.Null(p.PriceFor("AMD", BillingCycle.Annual));
        Assert.Null(p.PriceFor("EUR", BillingCycle.Monthly));
    }

    /// <summary>Setting the same currency and cycle again replaces rather than duplicates.</summary>
    [Fact]
    public void SetPrice_Twice_Replaces()
    {
        var p = NewProduct();
        p.SetPrice("AMD", BillingCycle.Monthly, 1200m);
        p.SetPrice("amd", BillingCycle.Monthly, 1250m);

        Assert.Single(p.Prices);
        Assert.Equal(1250m, p.PriceFor("AMD", BillingCycle.Monthly));
    }

    /// <summary>A negative price is a data error, not a discount.</summary>
    [Fact]
    public void SetPrice_RejectsNegative()
    {
        var p = NewProduct();
        Assert.Throws<ArgumentOutOfRangeException>(() => p.SetPrice("USD", BillingCycle.Monthly, -1m));
    }

    /// <summary>A product sells in a currency only when it has at least one price in it.</summary>
    [Fact]
    public void SellsIn_IsTrueOnlyWithAPrice()
    {
        var p = NewProduct();
        Assert.False(p.SellsIn("AMD"));
        p.SetPrice("AMD", BillingCycle.Annual, 12000m);
        Assert.True(p.SellsIn("AMD"));
    }

    /// <summary>Removing a currency drops every cycle for it and nothing else.</summary>
    [Fact]
    public void RemovePrices_DropsOnlyThatCurrency()
    {
        var p = NewProduct();
        p.SetPrice("AMD", BillingCycle.Monthly, 1200m);
        p.SetPrice("AMD", BillingCycle.Annual, 12000m);
        p.SetPrice("USD", BillingCycle.Monthly, 2.99m);

        p.RemovePrices("AMD");

        Assert.False(p.SellsIn("AMD"));
        Assert.True(p.SellsIn("USD"));
    }

    /// <summary>The three spellings the order flow has always accepted all parse.</summary>
    [Theory]
    [InlineData("monthly", BillingCycle.Monthly)]
    [InlineData("annual", BillingCycle.Annual)]
    [InlineData("annually", BillingCycle.Annual)]
    [InlineData("Monthly", BillingCycle.Monthly)]
    public void BillingCycleParser_AcceptsTheKnownSpellings(string raw, BillingCycle expected)
        => Assert.Equal(expected, BillingCycleParser.Parse(raw));

    /// <summary>An unknown cycle is refused rather than silently priced as something.</summary>
    [Fact]
    public void BillingCycleParser_RejectsUnknown()
        => Assert.Throws<ArgumentException>(() => BillingCycleParser.Parse("weekly"));
}
