using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Unit tests for <see cref="PriceDiscount"/>: the bound a discount stays within and the price it leaves.
/// </summary>
public sealed class PriceDiscountTests
{
    /// <summary>The ends of the range and the values between them are valid.</summary>
    /// <param name="percent">A discount within the range.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(12.5)]
    [InlineData(100)]
    public void IsValidPercent_AcceptsTheRangeIncludingItsEnds(double percent)
    {
        PriceDiscount.IsValidPercent((decimal)percent).Should().BeTrue();
    }

    /// <summary>A negative discount and one above 100 percent are not valid.</summary>
    /// <param name="percent">A discount outside the range.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void IsValidPercent_RefusesAValueOutsideTheRange(double percent)
    {
        PriceDiscount.IsValidPercent((decimal)percent).Should().BeFalse();
    }

    /// <summary>The discount comes off the list price and the result is rounded to the currency's minor unit.</summary>
    /// <param name="listPrice">The price before the discount.</param>
    /// <param name="percent">The discount in percent.</param>
    /// <param name="currency">The currency of the price.</param>
    /// <param name="expected">The price after the discount.</param>
    [Theory]
    [InlineData(40, 25, "USD", 30)]
    [InlineData(19.99, 15, "USD", 16.99)]
    [InlineData(100, 0, "USD", 100)]
    [InlineData(100, 100, "USD", 0)]
    [InlineData(999, 10, "JPY", 899)]
    public void Apply_TakesThePercentOffAndRoundsToTheMinorUnit(
        double listPrice, double percent, string currency, double expected)
    {
        var price = PriceDiscount.Apply((decimal)listPrice, (decimal)percent, currency);

        price.Should().Be((decimal)expected);
    }

    /// <summary>A discount outside 0 to 100 percent is refused, so a price can never grow or turn negative.</summary>
    /// <param name="percent">A discount outside the range.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Apply_RefusesADiscountOutsideTheRange(double percent)
    {
        var act = () => PriceDiscount.Apply(10m, (decimal)percent, "USD");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
