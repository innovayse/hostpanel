using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Tests for the <see cref="Currency"/> aggregate's invariants and formatting.</summary>
public sealed class CurrencyTests
{
    /// <summary>Codes are normalised to upper case so lookups by "usd" and "USD" agree.</summary>
    [Fact]
    public void Create_UpperCasesTheAlphaCode()
    {
        var c = Currency.Create("usd", "840", "$", "", 2, 1m, isBase: true);
        Assert.Equal("USD", c.Code);
    }

    /// <summary>A numeric code keeps its leading zero: "051" is AMD, "51" is nothing.</summary>
    [Fact]
    public void Create_KeepsTheLeadingZeroOnTheNumericCode()
    {
        var c = Currency.Create("AMD", "051", "", " ֏", 0, 1m / 390m, isBase: false);
        Assert.Equal("051", c.Numeric);
    }

    /// <summary>The base currency's rate is 1 by definition; anything else is a data error.</summary>
    [Fact]
    public void Create_BaseCurrencyMustHaveRateOne()
    {
        Assert.Throws<ArgumentException>(() => Currency.Create("USD", "840", "$", "", 2, 2m, isBase: true));
    }

    /// <summary>A rate must be positive; zero would make every converted price zero.</summary>
    [Fact]
    public void UpdateRate_RejectsNonPositive()
    {
        var c = Currency.Create("AMD", "051", "", " ֏", 0, 0.0025m, isBase: false);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.UpdateRate(0m));
    }

    /// <summary>Formatting uses this currency's own decimals, prefix and suffix, never the locale.</summary>
    /// <param name="code">Currency code.</param>
    /// <param name="prefix">Text printed before the number.</param>
    /// <param name="suffix">Text printed after the number.</param>
    /// <param name="decimals">Decimal places shown.</param>
    /// <param name="amount">Amount to format.</param>
    /// <param name="expected">The exact expected output.</param>
    [Theory]
    [InlineData("USD", "$", "", 2, 2.99, "$2.99")]
    [InlineData("AMD", "", " ֏", 0, 1200, "1200 ֏")]
    [InlineData("AMD", "", " ֏", 0, 1166.1, "1166 ֏")]
    public void Format_UsesTheCurrencysOwnShape(string code, string prefix, string suffix, int decimals, decimal amount, string expected)
    {
        var c = Currency.Create(code, "000", prefix, suffix, decimals, 1m, isBase: true);
        Assert.Equal(expected, c.Format(amount));
    }

    /// <summary>Rounding follows the currency's decimals, half away from zero, so 2.995 → 3.00 not 2.99.</summary>
    [Fact]
    public void Round_IsHalfAwayFromZeroAtTheCurrencysDecimals()
    {
        var usd = Currency.Create("USD", "840", "$", "", 2, 1m, isBase: true);
        Assert.Equal(3.00m, usd.Round(2.995m));
    }

    /// <summary>
    /// Disabling the base currency throws <see cref="DomainRuleException"/> with
    /// <c>CURRENCY_BASE_DISABLE_NOT_ALLOWED</c>: a client's default currency must always resolve.
    /// </summary>
    [Fact]
    public void Disable_OnBaseCurrency_ThrowsWithCurrencyBaseDisableNotAllowed()
    {
        var usd = Currency.Create("USD", "840", "$", "", 2, 1m, isBase: true);

        var ex = Assert.Throws<DomainRuleException>(usd.Disable);

        Assert.Equal(BillingErrors.CurrencyBaseDisableNotAllowed, ex.Code);
    }

    /// <summary>Conversion goes through the base: 10 USD at 1 AMD = 0.0025 USD is 4000 AMD, rounded to AMD's decimals.</summary>
    [Fact]
    public void ConvertTo_UsesTheStoredRatesAndTheTargetsDecimals()
    {
        var usd = Currency.Create("USD", "840", "$", "", 2, 1m, isBase: true);
        var amd = Currency.Create("AMD", "051", "", " AMD", 0, 0.0025m, isBase: false);

        Assert.Equal(4000m, usd.ConvertTo(10m, amd));
        Assert.Equal(10m, amd.ConvertTo(4000m, usd));
        Assert.Equal(10m, usd.ConvertTo(10m, usd));
    }
}
