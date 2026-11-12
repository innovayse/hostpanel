using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>
/// Unit tests for <c>ProductOptionRules</c>, the name and price checks a group and an option share.
/// The class is internal to the domain, so the cases reach it through <see cref="ProductOptionGroup.AddOption"/>
/// and <see cref="ProductOptionGroup.Create"/>, which are its two callers.
/// </summary>
public class ProductOptionRulesTests
{
    /// <summary>A dropdown group, which takes any number of options and any price.</summary>
    /// <returns>A new dropdown group.</returns>
    private static ProductOptionGroup Group() =>
        ProductOptionGroup.Create(1, "Operating system", ProductOptionType.Dropdown, false, 0, null, null);

    /// <summary>Surrounding whitespace is stripped from an option name and from a group name alike.</summary>
    [Fact]
    public void NormaliseName_TrimsForOptionsAndGroups()
    {
        var option = Group().AddOption("\t Linux \n", 0m, ProductOptionPriceType.Fixed, 0m, 0);
        var group = ProductOptionGroup.Create(1, "  OS  ", ProductOptionType.Radio, false, 0, null, null);

        Assert.Equal("Linux", option.Name);
        Assert.Equal("OS", group.Name);
    }

    /// <summary>A name that is null, empty or only whitespace is refused.</summary>
    /// <param name="name">Name under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormaliseName_RefusesBlank(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Group().AddOption(name!, 0m, ProductOptionPriceType.Fixed, 0m, 0));
    }

    /// <summary>The length limit is checked after trimming, so padding does not count against the name.</summary>
    [Fact]
    public void NormaliseName_MeasuresTheTrimmedName()
    {
        var padded = "  " + new string('a', ProductOption.NameMaxLength) + "  ";

        var option = Group().AddOption(padded, 0m, ProductOptionPriceType.Fixed, 0m, 0);

        Assert.Equal(ProductOption.NameMaxLength, option.Name.Length);
    }

    /// <summary>The group and the option each use their own column width, and neither accepts one more character.</summary>
    [Fact]
    public void NormaliseName_UsesTheWidthOfEachColumn()
    {
        Assert.Throws<ArgumentException>(() => ProductOptionGroup.Create(
            1, new string('a', ProductOptionGroup.NameMaxLength + 1), ProductOptionType.Radio, false, 0, null, null));
        Assert.Throws<ArgumentException>(() =>
            Group().AddOption(new string('a', ProductOption.NameMaxLength + 1), 0m, ProductOptionPriceType.Fixed, 0m, 0));
    }

    /// <summary>A negative recurring or setup amount is refused for both price types.</summary>
    /// <param name="price">Recurring modifier under test.</param>
    /// <param name="setup">Setup fee modifier under test.</param>
    /// <param name="priceType">How the amounts are read.</param>
    [Theory]
    [InlineData(-0.0001, 0, ProductOptionPriceType.Fixed)]
    [InlineData(0, -0.0001, ProductOptionPriceType.Fixed)]
    [InlineData(-1, 0, ProductOptionPriceType.Percentage)]
    [InlineData(0, -1, ProductOptionPriceType.Percentage)]
    public void EnsurePrice_RefusesNegativeAmounts(double price, double setup, ProductOptionPriceType priceType)
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            Group().AddOption("Linux", (decimal)price, priceType, (decimal)setup, 0));

        Assert.Equal(ProductsErrors.ProductOptionPriceOutOfRange, error.Code);
    }

    /// <summary>A percentage may reach one hundred exactly and not a hair over.</summary>
    [Fact]
    public void EnsurePrice_PercentageStopsAtOneHundred()
    {
        var group = Group();

        group.AddOption("Exactly", 100m, ProductOptionPriceType.Percentage, 100m, 0);

        Assert.Throws<DomainRuleException>(() =>
            group.AddOption("Over", 100.0001m, ProductOptionPriceType.Percentage, 0m, 1));
        Assert.Single(group.Options);
    }

    /// <summary>A fixed amount has no ceiling: only a percentage is capped.</summary>
    [Fact]
    public void EnsurePrice_FixedAmountHasNoCeiling()
    {
        var option = Group().AddOption("Dedicated IP", 5000m, ProductOptionPriceType.Fixed, 10000m, 0);

        Assert.Equal(5000m, option.PriceModifier);
        Assert.Equal(10000m, option.SetupFeeModifier);
    }
}
