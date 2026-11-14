using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>Unit tests for the free-trial settings of a product: <see cref="ProductTrial"/> and <see cref="Product.ConfigureTrial"/>.</summary>
public class ProductTrialTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A new product has the trial off and the default settings.</summary>
    [Fact]
    public void NewProduct_HasTheTrialOffWithTheDefaults()
    {
        var product = NewProduct();

        product.Trial.Should().BeEquivalentTo(ProductTrial.Default);
        product.TrialEnabled.Should().BeFalse();
        product.TrialDays.Should().Be(14);
        product.TrialAutoConvert.Should().BeTrue();
        product.TrialReminderDays.Should().Be("3,1");
        product.TrialGracePeriodDays.Should().Be(3);
    }

    /// <summary>Configuring the trial stores every setting and keeps the reminder days descending.</summary>
    [Fact]
    public void ConfigureTrial_StoresTheSettingsWithTheReminderDaysDescending()
    {
        var product = NewProduct();

        product.ConfigureTrial(new ProductTrial(true, 30, false, [1, 7, 3], 5));

        product.Trial.Enabled.Should().BeTrue();
        product.Trial.Days.Should().Be(30);
        product.Trial.AutoConvert.Should().BeFalse();
        product.Trial.ReminderDays.Should().Equal(7, 3, 1);
        product.Trial.GracePeriodDays.Should().Be(5);
        product.TrialReminderDays.Should().Be("7,3,1");
    }

    /// <summary>A trial with no reminder days is accepted.</summary>
    [Fact]
    public void ConfigureTrial_WithNoReminderDays_IsAccepted()
    {
        var product = NewProduct();

        product.ConfigureTrial(new ProductTrial(true, 7, true, [], 0));

        product.Trial.ReminderDays.Should().BeEmpty();
        product.TrialReminderDays.Should().BeEmpty();
    }

    /// <summary>Settings outside the accepted ranges are refused with <c>PRODUCT_TRIAL_INVALID</c> and change nothing.</summary>
    /// <param name="days">The trial length.</param>
    /// <param name="reminders">The reminder days.</param>
    /// <param name="grace">The grace period.</param>
    [Theory]
    [InlineData(0, new[] { 3, 1 }, 3)]
    [InlineData(366, new[] { 3, 1 }, 3)]
    [InlineData(14, new[] { 3, 1 }, -1)]
    [InlineData(14, new[] { 3, 1 }, 31)]
    [InlineData(14, new[] { 14 }, 3)]
    [InlineData(14, new[] { 0 }, 3)]
    [InlineData(14, new[] { 3, 3 }, 3)]
    [InlineData(30, new[] { 6, 5, 4, 3, 2, 1 }, 3)]
    public void ConfigureTrial_OutOfRange_IsRefusedAndChangesNothing(int days, int[] reminders, int grace)
    {
        var product = NewProduct();

        var act = () => product.ConfigureTrial(new ProductTrial(true, days, true, reminders, grace));

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ProductsErrors.ProductTrialInvalid);
        product.Trial.Should().BeEquivalentTo(ProductTrial.Default);
    }

    /// <summary>The bounds of the length, the grace period and the reminder count are accepted.</summary>
    /// <param name="days">The trial length.</param>
    /// <param name="reminders">The reminder days.</param>
    /// <param name="grace">The grace period.</param>
    [Theory]
    [InlineData(1, new int[0], 0)]
    [InlineData(365, new[] { 364, 100, 50, 7, 1 }, 30)]
    [InlineData(2, new[] { 1 }, 3)]
    public void IsValid_OnTheBounds_Passes(int days, int[] reminders, int grace)
    {
        ProductTrial.IsValid(days, reminders, grace).Should().BeTrue();
    }

    /// <summary>Reminder days are written descending and read back the same.</summary>
    [Fact]
    public void ReminderDays_RoundTripThroughTheirStoredText()
    {
        var text = ProductTrial.FormatReminderDays([2, 14, 7]);

        text.Should().Be("14,7,2");
        ProductTrial.ParseReminderDays(text).Should().Equal(14, 7, 2);
        ProductTrial.ParseReminderDays(string.Empty).Should().BeEmpty();
        ProductTrial.ParseReminderDays(null).Should().BeEmpty();
    }

    /// <summary>Builds a product with no trial settings of its own.</summary>
    /// <returns>The product.</returns>
    private static Product NewProduct() =>
        Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 5m, 50m, Now);
}
