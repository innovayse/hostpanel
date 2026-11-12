using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// How <see cref="BundleDiscountAllocator"/> spreads a bundle's discount over an order's lines, without a
/// database: a percentage off every price, a fixed amount in proportion to the prices with cumulative
/// rounding so the shares add up exactly, never below zero and never more than the subtotal.
/// </summary>
public sealed class BundleDiscountAllocatorTests
{
    /// <summary>A discount type of none leaves every price as it was.</summary>
    [Fact]
    public void None_LeavesThePricesUnchanged()
    {
        var result = BundleDiscountAllocator.Allocate([19.99m, 5m], BundleDiscountType.None, 0m, "USD");

        result.Should().Equal(19.99m, 5m);
    }

    /// <summary>A percentage comes off every price and is rounded to the currency's minor unit.</summary>
    [Fact]
    public void Percentage_TakesThePercentOffEveryPrice()
    {
        var result = BundleDiscountAllocator.Allocate([100m, 50m, 19.99m], BundleDiscountType.Percentage, 10m, "USD");

        result.Should().Equal(90m, 45m, 17.99m);
    }

    /// <summary>A hundred percent makes every line free, never negative.</summary>
    [Fact]
    public void Percentage_OfAHundred_MakesEveryLineFree()
    {
        var result = BundleDiscountAllocator.Allocate([12.5m, 7m], BundleDiscountType.Percentage, 100m, "USD");

        result.Should().Equal(0m, 0m);
    }

    /// <summary>A fixed amount is split in proportion to the prices.</summary>
    [Fact]
    public void Fixed_IsSplitInProportionToThePrices()
    {
        var result = BundleDiscountAllocator.Allocate([30m, 20m, 50m], BundleDiscountType.Fixed, 10m, "USD");

        result.Should().Equal(27m, 18m, 45m);
    }

    /// <summary>Cumulative rounding spreads the remainder, so the shares add up to the amount exactly.</summary>
    [Fact]
    public void Fixed_SpreadsTheRoundingRemainderWithCumulativeRounding()
    {
        var result = BundleDiscountAllocator.Allocate([10m, 10m, 10m], BundleDiscountType.Fixed, 10m, "USD");

        result.Should().Equal(6.67m, 6.66m, 6.67m);
        (30m - result.Sum()).Should().Be(10m);
    }

    /// <summary>
    /// Over four or more lines the shares sum exactly to the amount and no line goes below zero, however the cents
    /// fall, including lines too cheap to carry a whole cent of the discount.
    /// </summary>
    [Fact]
    public void Fixed_OverManyLines_SumsExactlyAndLeavesNoLineBelowZero()
    {
        decimal[] prices = [0.01m, 3.33m, 0.02m, 7.77m, 0.01m, 19.99m];
        var subtotal = prices.Sum();

        var result = BundleDiscountAllocator.Allocate(prices, BundleDiscountType.Fixed, 7.77m, "USD");

        result.Should().HaveCount(prices.Length);
        result.Should().OnlyContain(price => price >= 0m);
        result.Zip(prices, (after, before) => before - after).Should().OnlyContain(share => share >= 0m);
        (subtotal - result.Sum()).Should().Be(7.77m);
    }

    /// <summary>A percentage over four lines rounds each price to the minor unit and leaves none below zero.</summary>
    [Fact]
    public void Percentage_OverManyLines_RoundsEachPriceAndLeavesNoneBelowZero()
    {
        var result = BundleDiscountAllocator.Allocate(
            [0.01m, 3.33m, 7.77m, 19.99m, 100m], BundleDiscountType.Percentage, 33m, "USD");

        result.Should().Equal(0.01m, 2.23m, 5.21m, 13.39m, 67m);
        result.Should().OnlyContain(price => price >= 0m);
    }

    /// <summary>A currency with no minor unit is rounded to whole units, the remainder still on the last line.</summary>
    [Fact]
    public void Fixed_InACurrencyWithoutMinorUnits_RoundsToWholeUnits()
    {
        var result = BundleDiscountAllocator.Allocate([100m, 100m, 100m], BundleDiscountType.Fixed, 100m, "JPY");

        result.Should().Equal(67m, 66m, 67m);
    }

    /// <summary>A fixed amount larger than the subtotal is capped at it: every line is free, none negative.</summary>
    [Fact]
    public void Fixed_LargerThanTheSubtotal_IsCappedAtTheSubtotal()
    {
        var result = BundleDiscountAllocator.Allocate([10m, 5m], BundleDiscountType.Fixed, 100m, "USD");

        result.Should().Equal(0m, 0m);
    }

    /// <summary>A fixed amount of zero, and an order with nothing to discount, change nothing.</summary>
    [Fact]
    public void Fixed_OfZero_OrOverNoCharge_ChangesNothing()
    {
        BundleDiscountAllocator.Allocate([10m, 5m], BundleDiscountType.Fixed, 0m, "USD").Should().Equal(10m, 5m);
        BundleDiscountAllocator.Allocate([0m, 0m], BundleDiscountType.Fixed, 10m, "USD").Should().Equal(0m, 0m);
        BundleDiscountAllocator.Allocate([], BundleDiscountType.Fixed, 10m, "USD").Should().BeEmpty();
    }

    /// <summary>
    /// Whatever the prices and the amount, no line goes below zero and the discount taken is the amount, or the
    /// subtotal when the amount is larger, exactly.
    /// </summary>
    /// <param name="amount">The fixed discount.</param>
    /// <param name="prices">The line prices.</param>
    [Theory]
    [InlineData(1000, new[] { 1.0, 1000.0 })]
    [InlineData(0.01, new[] { 3.33, 3.33, 3.34 })]
    [InlineData(7.77, new[] { 0.01, 0.01, 9.99 })]
    [InlineData(5.0, new[] { 2.5, 2.5 })]
    public void Fixed_NeverDrivesALineBelowZero_AndTakesTheAmountOrTheSubtotal(double amount, double[] prices)
    {
        var decimals = prices.Select(p => (decimal)p).ToList();
        var subtotal = decimals.Sum();

        var result = BundleDiscountAllocator.Allocate(decimals, BundleDiscountType.Fixed, (decimal)amount, "USD");

        result.Should().OnlyContain(price => price >= 0m);
        var taken = subtotal - result.Sum();
        taken.Should().Be(Math.Min((decimal)amount, subtotal));
    }

    /// <summary>A discount type the allocator does not know is refused rather than ignored.</summary>
    [Fact]
    public void UndefinedType_IsRefused()
    {
        var act = () => BundleDiscountAllocator.Allocate([1m], (BundleDiscountType)99, 1m, "USD");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
