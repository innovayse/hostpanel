using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>
/// Unit tests for <see cref="ProductOption"/>. An option has no public constructor, so every case
/// builds it through its <see cref="ProductOptionGroup"/>, the only way the domain creates one.
/// </summary>
public class ProductOptionTests
{
    /// <summary>A dropdown group, which takes any number of options and any price.</summary>
    /// <returns>A new dropdown group.</returns>
    private static ProductOptionGroup Group() =>
        ProductOptionGroup.Create(3, "Operating system", ProductOptionType.Dropdown, false, 0, null, null);

    /// <summary>A new option has no group id until the group is saved, and keeps the fields it was given.</summary>
    [Fact]
    public void New_KeepsItsFields_AndHasNoGroupIdYet()
    {
        var option = Group().AddOption("Linux", 1.25m, ProductOptionPriceType.Fixed, 4m, 6);

        Assert.Equal(0, option.Id);
        Assert.Equal(0, option.ProductOptionGroupId);
        Assert.Equal("Linux", option.Name);
        Assert.Equal(1.25m, option.PriceModifier);
        Assert.Equal(ProductOptionPriceType.Fixed, option.PriceType);
        Assert.Equal(4m, option.SetupFeeModifier);
        Assert.Equal(6, option.DisplayOrder);
    }

    /// <summary>The name column is exactly <see cref="ProductOption.NameMaxLength"/> wide: that length is accepted, one more is not.</summary>
    [Fact]
    public void Name_IsBoundedByTheColumnWidth()
    {
        var group = Group();

        var option = group.AddOption(new string('a', ProductOption.NameMaxLength), 0m, ProductOptionPriceType.Fixed, 0m, 0);

        Assert.Equal(ProductOption.NameMaxLength, option.Name.Length);
        Assert.Throws<ArgumentException>(() =>
            group.AddOption(new string('a', ProductOption.NameMaxLength + 1), 0m, ProductOptionPriceType.Fixed, 0m, 1));
        Assert.Single(group.Options);
    }

    /// <summary>An update replaces every editable field of the option and leaves its siblings alone.</summary>
    [Fact]
    public void Update_ReplacesEveryField_AndLeavesSiblingsAlone()
    {
        var group = Group();
        var option = group.AddOption("Linux", 1m, ProductOptionPriceType.Fixed, 2m, 0);
        var sibling = group.AddOption("Windows", 9m, ProductOptionPriceType.Fixed, 0m, 1);

        group.UpdateOption(option.Id, "  Debian  ", 15m, ProductOptionPriceType.Percentage, 20m, 4);

        Assert.Equal("Debian", option.Name);
        Assert.Equal(15m, option.PriceModifier);
        Assert.Equal(ProductOptionPriceType.Percentage, option.PriceType);
        Assert.Equal(20m, option.SetupFeeModifier);
        Assert.Equal(4, option.DisplayOrder);
        Assert.Equal("Windows", sibling.Name);
        Assert.Equal(9m, sibling.PriceModifier);
    }

    /// <summary>A refused update changes none of the fields, not even the ones that were valid.</summary>
    /// <param name="name">Name offered by the update.</param>
    /// <param name="price">Recurring modifier offered by the update.</param>
    /// <param name="order">Display order offered by the update.</param>
    [Theory]
    [InlineData("", 5, 1)]
    [InlineData("Debian", -1, 1)]
    [InlineData("Debian", 5, -1)]
    [InlineData("Debian", 101, 1)]
    public void Update_Refused_ChangesNothing(string name, int price, int order)
    {
        var group = Group();
        var option = group.AddOption("Linux", 1m, ProductOptionPriceType.Percentage, 2m, 0);

        Assert.ThrowsAny<Exception>(() =>
            group.UpdateOption(option.Id, name, price, ProductOptionPriceType.Percentage, 7m, order));

        Assert.Equal("Linux", option.Name);
        Assert.Equal(1m, option.PriceModifier);
        Assert.Equal(2m, option.SetupFeeModifier);
        Assert.Equal(0, option.DisplayOrder);
    }

    /// <summary>The percentage ceiling applies to the setup fee modifier as well as the recurring one.</summary>
    [Fact]
    public void Percentage_SetupFeeAbove100_IsRefused()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            Group().AddOption("Big", 10m, ProductOptionPriceType.Percentage, 100.01m, 0));

        Assert.Equal(ProductsErrors.ProductOptionPriceOutOfRange, error.Code);
    }

    /// <summary>Switching a fixed amount over 100 to a percentage is refused and the option stays as it was.</summary>
    [Fact]
    public void Update_FixedAmountOver100_CannotBecomeAPercentage()
    {
        var group = Group();
        var option = group.AddOption("Storage", 250m, ProductOptionPriceType.Fixed, 0m, 0);

        var error = Assert.Throws<DomainRuleException>(() =>
            group.UpdateOption(option.Id, "Storage", 250m, ProductOptionPriceType.Percentage, 0m, 0));

        Assert.Equal(ProductsErrors.ProductOptionPriceOutOfRange, error.Code);
        Assert.Equal(ProductOptionPriceType.Fixed, option.PriceType);
        Assert.Equal(250m, option.PriceModifier);
    }
}
