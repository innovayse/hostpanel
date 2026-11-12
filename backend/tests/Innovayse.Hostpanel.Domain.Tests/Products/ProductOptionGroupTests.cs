using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>Unit tests for the <see cref="ProductOptionGroup"/> aggregate and its <see cref="ProductOption"/> children.</summary>
public class ProductOptionGroupTests
{
    /// <summary>A list group with no range, the most common shape the tests start from.</summary>
    /// <returns>A new dropdown group.</returns>
    private static ProductOptionGroup Dropdown() =>
        ProductOptionGroup.Create(7, "Operating system", ProductOptionType.Dropdown, true, 2, null, null);

    /// <summary>A quantity group from 1 to 5.</summary>
    /// <returns>A new quantity group.</returns>
    private static ProductOptionGroup Quantity() =>
        ProductOptionGroup.Create(7, "Extra IPs", ProductOptionType.Quantity, false, 0, 1, 5);

    /// <summary>Create keeps every field and starts with no options.</summary>
    [Fact]
    public void Create_SetsAllFields()
    {
        var group = Dropdown();

        Assert.Equal(7, group.ProductId);
        Assert.Equal("Operating system", group.Name);
        Assert.Equal(ProductOptionType.Dropdown, group.Type);
        Assert.True(group.IsRequired);
        Assert.Equal(2, group.DisplayOrder);
        Assert.Null(group.MinQuantity);
        Assert.Null(group.MaxQuantity);
        Assert.Empty(group.Options);
    }

    /// <summary>Surrounding whitespace is stripped from the name.</summary>
    [Fact]
    public void Create_TrimsName()
    {
        var group = ProductOptionGroup.Create(1, "  OS  ", ProductOptionType.Radio, false, 0, null, null);

        Assert.Equal("OS", group.Name);
    }

    /// <summary>A blank name is rejected rather than stored.</summary>
    /// <param name="name">Name under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankName(string name) =>
        Assert.Throws<ArgumentException>(() => ProductOptionGroup.Create(1, name, ProductOptionType.Radio, false, 0, null, null));

    /// <summary>A name longer than the column is rejected.</summary>
    [Fact]
    public void Create_RejectsOverlongName() =>
        Assert.Throws<ArgumentException>(() => ProductOptionGroup.Create(
            1, new string('a', ProductOptionGroup.NameMaxLength + 1), ProductOptionType.Radio, false, 0, null, null));

    /// <summary>A name exactly as long as the column is accepted.</summary>
    [Fact]
    public void Create_AcceptsNameAtTheLimit()
    {
        var name = new string('a', ProductOptionGroup.NameMaxLength);

        Assert.Equal(name, ProductOptionGroup.Create(1, name, ProductOptionType.Radio, false, 0, null, null).Name);
    }

    /// <summary>A negative display order is rejected.</summary>
    [Fact]
    public void Create_RejectsNegativeDisplayOrder() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProductOptionGroup.Create(1, "OS", ProductOptionType.Radio, false, -1, null, null));

    /// <summary>A type that is not one of the defined ones is rejected.</summary>
    [Fact]
    public void Create_RejectsUndefinedType() =>
        Assert.Throws<ArgumentException>(() =>
            ProductOptionGroup.Create(1, "OS", (ProductOptionType)99, false, 0, null, null));

    /// <summary>A quantity group keeps its range.</summary>
    [Fact]
    public void Create_Quantity_KeepsTheRange()
    {
        var group = Quantity();

        Assert.Equal(1, group.MinQuantity);
        Assert.Equal(5, group.MaxQuantity);
    }

    /// <summary>A quantity group may start at zero, and may have a single value as its range.</summary>
    /// <param name="min">Minimum under test.</param>
    /// <param name="max">Maximum under test.</param>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(0, 100)]
    public void Create_Quantity_AcceptsValidRanges(int min, int max)
    {
        var group = ProductOptionGroup.Create(1, "IPs", ProductOptionType.Quantity, false, 0, min, max);

        Assert.Equal(min, group.MinQuantity);
        Assert.Equal(max, group.MaxQuantity);
    }

    /// <summary>A quantity group with a missing, negative, zero-maximum or inverted range is refused with its code.</summary>
    /// <param name="min">Minimum under test.</param>
    /// <param name="max">Maximum under test.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData(1, null)]
    [InlineData(null, 5)]
    [InlineData(-1, 5)]
    [InlineData(0, 0)]
    [InlineData(6, 5)]
    public void Create_Quantity_RejectsInvalidRanges(int? min, int? max)
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            ProductOptionGroup.Create(1, "IPs", ProductOptionType.Quantity, false, 0, min, max));

        Assert.Equal(ProductsErrors.ProductOptionQuantityRangeInvalid, error.Code);
    }

    /// <summary>Every type other than quantity refuses a range, whichever side is given.</summary>
    /// <param name="type">Type under test.</param>
    /// <param name="min">Minimum under test.</param>
    /// <param name="max">Maximum under test.</param>
    [Theory]
    [InlineData(ProductOptionType.Dropdown, 1, 5)]
    [InlineData(ProductOptionType.Radio, 1, null)]
    [InlineData(ProductOptionType.Checkbox, null, 5)]
    [InlineData(ProductOptionType.Text, 0, 1)]
    public void Create_NonQuantity_RejectsARange(ProductOptionType type, int? min, int? max)
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            ProductOptionGroup.Create(1, "Thing", type, false, 0, min, max));

        Assert.Equal(ProductsErrors.ProductOptionQuantityRangeInvalid, error.Code);
    }

    /// <summary>Update replaces the editable fields and leaves the product and type alone.</summary>
    [Fact]
    public void Update_ReplacesFields()
    {
        var group = Quantity();

        group.Update("Public IPs", true, 4, 2, 9);

        Assert.Equal("Public IPs", group.Name);
        Assert.True(group.IsRequired);
        Assert.Equal(4, group.DisplayOrder);
        Assert.Equal(2, group.MinQuantity);
        Assert.Equal(9, group.MaxQuantity);
        Assert.Equal(ProductOptionType.Quantity, group.Type);
        Assert.Equal(7, group.ProductId);
    }

    /// <summary>A refused update leaves the group as it was.</summary>
    [Fact]
    public void Update_RejectedRange_ChangesNothing()
    {
        var group = Quantity();

        Assert.Throws<DomainRuleException>(() => group.Update("Renamed", true, 4, 9, 2));

        Assert.Equal("Extra IPs", group.Name);
        Assert.False(group.IsRequired);
        Assert.Equal(0, group.DisplayOrder);
        Assert.Equal(1, group.MinQuantity);
        Assert.Equal(5, group.MaxQuantity);
    }

    /// <summary>Update applies the same name and order guards as creation.</summary>
    [Fact]
    public void Update_RejectsBlankNameAndNegativeOrder()
    {
        var group = Dropdown();

        Assert.Throws<ArgumentException>(() => group.Update(" ", true, 0, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => group.Update("OS", true, -1, null, null));
        Assert.Equal("Operating system", group.Name);
    }

    /// <summary>A range given to a non-quantity group on update is refused.</summary>
    [Fact]
    public void Update_NonQuantity_RejectsARange()
    {
        var error = Assert.Throws<DomainRuleException>(() => Dropdown().Update("OS", true, 0, 1, 5));

        Assert.Equal(ProductsErrors.ProductOptionQuantityRangeInvalid, error.Code);
    }

    /// <summary>Reorder moves the group and refuses a negative position.</summary>
    [Fact]
    public void Reorder_SetsThePosition()
    {
        var group = Dropdown();

        group.Reorder(9);

        Assert.Equal(9, group.DisplayOrder);
        Assert.Throws<ArgumentOutOfRangeException>(() => group.Reorder(-1));
        Assert.Equal(9, group.DisplayOrder);
    }

    /// <summary>A dropdown and a radio group hold any number of options.</summary>
    /// <param name="type">Type under test.</param>
    [Theory]
    [InlineData(ProductOptionType.Dropdown)]
    [InlineData(ProductOptionType.Radio)]
    public void AddOption_ListTypes_HoldManyOptions(ProductOptionType type)
    {
        var group = ProductOptionGroup.Create(1, "OS", type, true, 0, null, null);

        group.AddOption("Linux", 0m, ProductOptionPriceType.Fixed, 0m, 0);
        group.AddOption("Windows", 5m, ProductOptionPriceType.Fixed, 10m, 1);
        group.AddOption("BSD", 2m, ProductOptionPriceType.Percentage, 0m, 2);

        Assert.Equal(3, group.Options.Count);
    }

    /// <summary>AddOption keeps every field and trims the name.</summary>
    [Fact]
    public void AddOption_SetsAllFields()
    {
        var group = Dropdown();

        var option = group.AddOption("  Windows  ", 5.5m, ProductOptionPriceType.Percentage, 12m, 3);

        Assert.Same(option, Assert.Single(group.Options));
        Assert.Equal("Windows", option.Name);
        Assert.Equal(5.5m, option.PriceModifier);
        Assert.Equal(ProductOptionPriceType.Percentage, option.PriceType);
        Assert.Equal(12m, option.SetupFeeModifier);
        Assert.Equal(3, option.DisplayOrder);
    }

    /// <summary>A checkbox, quantity or text group takes one option and refuses a second.</summary>
    /// <param name="type">Type under test.</param>
    [Theory]
    [InlineData(ProductOptionType.Checkbox)]
    [InlineData(ProductOptionType.Quantity)]
    [InlineData(ProductOptionType.Text)]
    public void AddOption_SingleOptionTypes_RefuseASecond(ProductOptionType type)
    {
        var quantity = type == ProductOptionType.Quantity;
        var group = ProductOptionGroup.Create(1, "Thing", type, false, 0, quantity ? 1 : null, quantity ? 3 : null);
        group.AddOption("Only", 0m, ProductOptionPriceType.Fixed, 0m, 0);

        var error = Assert.Throws<DomainRuleException>(() =>
            group.AddOption("Second", 0m, ProductOptionPriceType.Fixed, 0m, 1));

        Assert.Equal(ProductsErrors.ProductOptionCountNotAllowed, error.Code);
        Assert.Single(group.Options);
    }

    /// <summary>Removing the only option of a single-option group lets it take another.</summary>
    [Fact]
    public void AddOption_SingleOptionType_AcceptsOneAgainAfterRemoval()
    {
        var group = ProductOptionGroup.Create(1, "Backups", ProductOptionType.Checkbox, false, 0, null, null);
        var first = group.AddOption("Daily", 3m, ProductOptionPriceType.Fixed, 0m, 0);
        SetId(first, 11);

        group.RemoveOption(11);
        group.AddOption("Weekly", 1m, ProductOptionPriceType.Fixed, 0m, 0);

        Assert.Equal("Weekly", Assert.Single(group.Options).Name);
    }

    /// <summary>A text option must carry no price on either modifier.</summary>
    /// <param name="price">Recurring modifier under test.</param>
    /// <param name="setup">Setup modifier under test.</param>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void AddOption_Text_RefusesAPrice(int price, int setup)
    {
        var group = ProductOptionGroup.Create(1, "Note", ProductOptionType.Text, false, 0, null, null);

        var error = Assert.Throws<DomainRuleException>(() =>
            group.AddOption("Your note", price, ProductOptionPriceType.Fixed, setup, 0));

        Assert.Equal(ProductsErrors.ProductOptionPriceNotAllowed, error.Code);
        Assert.Empty(group.Options);
    }

    /// <summary>A text option with both modifiers at zero is accepted.</summary>
    [Fact]
    public void AddOption_Text_AcceptsNoPrice()
    {
        var group = ProductOptionGroup.Create(1, "Note", ProductOptionType.Text, false, 0, null, null);

        group.AddOption("Your note", 0m, ProductOptionPriceType.Fixed, 0m, 0);

        Assert.Single(group.Options);
    }

    /// <summary>A negative modifier, and a percentage above 100, are refused with the range code.</summary>
    /// <param name="price">Recurring modifier under test.</param>
    /// <param name="setup">Setup modifier under test.</param>
    /// <param name="priceType">Price type under test.</param>
    [Theory]
    [InlineData(-0.01, 0, ProductOptionPriceType.Fixed)]
    [InlineData(0, -1, ProductOptionPriceType.Fixed)]
    [InlineData(-5, 0, ProductOptionPriceType.Percentage)]
    [InlineData(100.01, 0, ProductOptionPriceType.Percentage)]
    [InlineData(0, 101, ProductOptionPriceType.Percentage)]
    public void AddOption_RefusesPricesOutOfRange(double price, double setup, ProductOptionPriceType priceType)
    {
        var group = Dropdown();

        var error = Assert.Throws<DomainRuleException>(() =>
            group.AddOption("Windows", (decimal)price, priceType, (decimal)setup, 0));

        Assert.Equal(ProductsErrors.ProductOptionPriceOutOfRange, error.Code);
        Assert.Empty(group.Options);
    }

    /// <summary>The ends of the range are accepted: 100 percent, and a large fixed amount.</summary>
    [Fact]
    public void AddOption_AcceptsTheEdgesOfTheRange()
    {
        var group = Dropdown();

        group.AddOption("Full", 100m, ProductOptionPriceType.Percentage, 100m, 0);
        group.AddOption("Big", 99999m, ProductOptionPriceType.Fixed, 99999m, 1);
        group.AddOption("Free", 0m, ProductOptionPriceType.Percentage, 0m, 2);

        Assert.Equal(3, group.Options.Count);
    }

    /// <summary>A blank name, an over-long name and a negative position are refused on an option.</summary>
    [Fact]
    public void AddOption_RefusesBadNameAndOrder()
    {
        var group = Dropdown();

        Assert.Throws<ArgumentException>(() => group.AddOption(" ", 0m, ProductOptionPriceType.Fixed, 0m, 0));
        Assert.Throws<ArgumentException>(() => group.AddOption(
            new string('a', ProductOption.NameMaxLength + 1), 0m, ProductOptionPriceType.Fixed, 0m, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => group.AddOption("Linux", 0m, ProductOptionPriceType.Fixed, 0m, -1));
        Assert.Empty(group.Options);
    }

    /// <summary>UpdateOption replaces every field of the named option and no other.</summary>
    [Fact]
    public void UpdateOption_ReplacesFields()
    {
        var group = Dropdown();
        var linux = group.AddOption("Linux", 0m, ProductOptionPriceType.Fixed, 0m, 0);
        var windows = group.AddOption("Windows", 5m, ProductOptionPriceType.Fixed, 0m, 1);
        SetId(linux, 1);
        SetId(windows, 2);

        group.UpdateOption(2, "Windows Server", 7m, ProductOptionPriceType.Percentage, 15m, 4);

        Assert.Equal("Windows Server", windows.Name);
        Assert.Equal(7m, windows.PriceModifier);
        Assert.Equal(ProductOptionPriceType.Percentage, windows.PriceType);
        Assert.Equal(15m, windows.SetupFeeModifier);
        Assert.Equal(4, windows.DisplayOrder);
        Assert.Equal("Linux", linux.Name);
    }

    /// <summary>A refused option update leaves the option as it was.</summary>
    [Fact]
    public void UpdateOption_Rejected_ChangesNothing()
    {
        var group = Dropdown();
        var linux = group.AddOption("Linux", 2m, ProductOptionPriceType.Fixed, 1m, 0);
        SetId(linux, 1);

        Assert.Throws<DomainRuleException>(() =>
            group.UpdateOption(1, "Renamed", 150m, ProductOptionPriceType.Percentage, 0m, 3));

        Assert.Equal("Linux", linux.Name);
        Assert.Equal(2m, linux.PriceModifier);
        Assert.Equal(ProductOptionPriceType.Fixed, linux.PriceType);
        Assert.Equal(0, linux.DisplayOrder);
    }

    /// <summary>Updating a text option to carry a price is refused.</summary>
    [Fact]
    public void UpdateOption_Text_RefusesAPrice()
    {
        var group = ProductOptionGroup.Create(1, "Note", ProductOptionType.Text, false, 0, null, null);
        SetId(group.AddOption("Your note", 0m, ProductOptionPriceType.Fixed, 0m, 0), 1);

        var error = Assert.Throws<DomainRuleException>(() =>
            group.UpdateOption(1, "Your note", 1m, ProductOptionPriceType.Fixed, 0m, 0));

        Assert.Equal(ProductsErrors.ProductOptionPriceNotAllowed, error.Code);
    }

    /// <summary>Changing or removing an option the group does not hold is refused with the not-found code.</summary>
    [Fact]
    public void UpdateAndRemoveOption_UnknownId_AreRefused()
    {
        var group = Dropdown();

        var update = Assert.Throws<DomainRuleException>(() =>
            group.UpdateOption(99, "Linux", 0m, ProductOptionPriceType.Fixed, 0m, 0));
        var remove = Assert.Throws<DomainRuleException>(() => group.RemoveOption(99));

        Assert.Equal(ProductsErrors.ProductOptionNotFound, update.Code);
        Assert.Equal(ProductsErrors.ProductOptionNotFound, remove.Code);
    }

    /// <summary>RemoveOption drops only the named option.</summary>
    [Fact]
    public void RemoveOption_RemovesTheNamedOption()
    {
        var group = Dropdown();
        SetId(group.AddOption("Linux", 0m, ProductOptionPriceType.Fixed, 0m, 0), 1);
        SetId(group.AddOption("Windows", 0m, ProductOptionPriceType.Fixed, 0m, 1), 2);

        group.RemoveOption(1);

        Assert.Equal("Windows", Assert.Single(group.Options).Name);
    }

    /// <summary>Stamps the id the database would assign, which a unit test has no database to do.</summary>
    /// <param name="option">The option to stamp.</param>
    /// <param name="id">The id to give it.</param>
    private static void SetId(ProductOption option, int id) =>
        typeof(ProductOption).GetProperty(nameof(ProductOption.Id))!.SetValue(option, id);
}
