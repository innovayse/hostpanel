using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>Tests for per-currency prices on <see cref="Product"/>.</summary>
public sealed class ProductPriceTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a minimal shared-hosting product to price.</summary>
    /// <returns>A new product with monthly and annual base prices.</returns>
    private static Product NewProduct() =>
        Product.Create(groupId: 1, name: "Starter", description: null, website: null, slug: null,
            packageName: null, type: ProductType.SharedHosting, monthlyPrice: 2.99m, annualPrice: 29.99m, now: Now);

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

    /// <summary>Every spelling older data carries parses, whatever its casing or padding.</summary>
    /// <param name="raw">The cycle spelling as stored or imported.</param>
    /// <param name="expected">The cycle it should parse to.</param>
    [Theory]
    [InlineData("monthly", BillingCycle.Monthly)]
    [InlineData("Monthly", BillingCycle.Monthly)]
    [InlineData("annual", BillingCycle.Annual)]
    [InlineData("Annual", BillingCycle.Annual)]
    [InlineData("annually", BillingCycle.Annual)]
    [InlineData(" Annually ", BillingCycle.Annual)]
    public void BillingCycleParser_AcceptsTheKnownSpellings(string raw, BillingCycle expected)
    {
        Assert.True(BillingCycleParser.TryParse(raw, out var cycle));
        Assert.Equal(expected, cycle);
    }

    /// <summary>An unknown cycle is refused rather than silently priced as something.</summary>
    /// <param name="raw">A text that names no cycle.</param>
    [Theory]
    [InlineData("weekly")]
    [InlineData("free")]
    [InlineData("")]
    [InlineData(null)]
    public void BillingCycleParser_RejectsUnknown(string? raw)
        => Assert.False(BillingCycleParser.TryParse(raw, out _));

    /// <summary>Replacing all currencies removes one the list no longer names, and rewrites a listed one in full.</summary>
    [Fact]
    public void ReplacePrices_AllCurrencies_RemovesUnlistedAndRewritesListed()
    {
        var p = NewProduct();
        p.SetPrice("USD", BillingCycle.Monthly, 3m);
        p.SetPrice("USD", BillingCycle.Annual, 30m);
        p.SetPrice("EUR", BillingCycle.Monthly, 2m);

        p.ReplacePrices([new ProductCurrencyPrices("usd", 4m, null, null, null, null)], replaceAllCurrencies: true);

        Assert.Equal(4m, p.PriceFor("USD", BillingCycle.Monthly));
        Assert.Null(p.PriceFor("USD", BillingCycle.Annual));
        Assert.False(p.SellsIn("EUR"));
    }

    /// <summary>Replacing only the listed currencies leaves every other currency as it was.</summary>
    [Fact]
    public void ReplacePrices_ListedOnly_KeepsOtherCurrencies()
    {
        var p = NewProduct();
        p.SetPrice("USD", BillingCycle.Monthly, 3m);
        p.SetPrice("EUR", BillingCycle.Monthly, 2m);

        p.ReplacePrices([new ProductCurrencyPrices("USD", 5m, 50m, null, null, null)], replaceAllCurrencies: false);

        Assert.Equal(5m, p.PriceFor("USD", BillingCycle.Monthly));
        Assert.Equal(50m, p.PriceFor("USD", BillingCycle.Annual));
        Assert.Equal(2m, p.PriceFor("EUR", BillingCycle.Monthly));
    }

    /// <summary>A price can be set and read back for each of the five cycles, independently.</summary>
    /// <param name="cycle">The billing cycle.</param>
    [Theory]
    [InlineData(BillingCycle.Monthly)]
    [InlineData(BillingCycle.Annual)]
    [InlineData(BillingCycle.Quarterly)]
    [InlineData(BillingCycle.SemiAnnual)]
    [InlineData(BillingCycle.Biennial)]
    public void SetPrice_EachCycle_IsReadBackForThatCycleOnly(BillingCycle cycle)
    {
        var p = NewProduct();
        p.SetPrice("EUR", cycle, 7m);

        Assert.Equal(7m, p.PriceFor("EUR", cycle));
        Assert.Equal(1, p.Prices.Count(price => price.CurrencyCode == "EUR"));
    }

    /// <summary>Replacing prices writes all five cycles and drops a cycle left null.</summary>
    [Fact]
    public void ReplacePrices_AllFiveCycles_WritesEachAndDropsNull()
    {
        var p = NewProduct();
        p.SetPrice("USD", BillingCycle.Quarterly, 99m);

        p.ReplacePrices([new ProductCurrencyPrices("USD", 1m, 12m, null, 6m, 22m)], replaceAllCurrencies: false);

        Assert.Equal(1m, p.PriceFor("USD", BillingCycle.Monthly));
        Assert.Equal(12m, p.PriceFor("USD", BillingCycle.Annual));
        Assert.Null(p.PriceFor("USD", BillingCycle.Quarterly));
        Assert.Equal(6m, p.PriceFor("USD", BillingCycle.SemiAnnual));
        Assert.Equal(22m, p.PriceFor("USD", BillingCycle.Biennial));
    }

    /// <summary>A product that only has the two original cycles lists exactly those, and the new cycles stay unpriced.</summary>
    [Fact]
    public void Priced_OnlyOriginalCycles_ListsThemAndLeavesTheNewOnesOut()
    {
        var prices = new ProductCurrencyPrices("USD", 3m, 30m, null, null, null);

        Assert.Equal([(BillingCycle.Monthly, 3m), (BillingCycle.Annual, 30m)], prices.Priced());
    }

    /// <summary>The factory asks for every cycle, so none can be forgotten.</summary>
    [Fact]
    public void Of_AsksForEveryCycle()
    {
        var asked = new List<BillingCycle>();

        var prices = ProductCurrencyPrices.Of("USD", cycle =>
        {
            asked.Add(cycle);
            return (decimal)(int)cycle + 1;
        });

        Assert.Equal(Enum.GetValues<BillingCycle>().OrderBy(c => c), asked.OrderBy(c => c));
        Assert.Equal(new ProductCurrencyPrices("USD", 1m, 2m, 3m, 4m, 5m), prices);
    }

    /// <summary>A negative amount is refused for a new cycle as for the old ones.</summary>
    [Fact]
    public void SetPrice_NegativeAmountForNewCycle_Throws()
    {
        var p = NewProduct();

        Assert.Throws<ArgumentOutOfRangeException>(() => p.SetPrice("USD", BillingCycle.Biennial, -1m));
    }
}
