using System.Globalization;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for the <see cref="CurrencyCode"/> rule shared by invoices, orders, quotes and transactions.</summary>
public sealed class CurrencyCodeTests
{
    /// <summary>A three-letter code is accepted and stored in upper case.</summary>
    [Fact]
    public void Normalise_UpperCasesAThreeLetterCode()
    {
        CurrencyCode.Normalise("eur").Should().Be("EUR");
    }

    /// <summary>Anything but a three-letter code is refused.</summary>
    /// <param name="currency">A missing or malformed code.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("123")]
    [InlineData("12!")]
    [InlineData("U$D")]
    [InlineData("US1")]
    [InlineData("E R")]
    [InlineData("€UR")]
    [InlineData("ЕUR")]
    [InlineData(" eu")]
    [InlineData("eu ")]
    public void Normalise_RefusesAnythingButThreeLetters(string currency)
    {
        var act = () => CurrencyCode.Normalise(currency);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A missing code is refused as an argument error rather than a null reference.</summary>
    [Fact]
    public void Normalise_RefusesNull()
    {
        var act = () => CurrencyCode.Normalise(null!);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>Padding around a three-letter code is trimmed first, so the code is stored clean and upper case.</summary>
    /// <param name="currency">A padded code.</param>
    [Theory]
    [InlineData(" eur")]
    [InlineData("eur ")]
    [InlineData("  EUR  ")]
    [InlineData("\teur\n")]
    public void Normalise_TrimsPaddingBeforeChecking(string currency)
    {
        CurrencyCode.Normalise(currency).Should().Be("EUR");
    }

    /// <summary>The shape check accepts only three ASCII letters of either case, with no padding.</summary>
    /// <param name="currency">The value to inspect.</param>
    /// <param name="expected">Whether it is a valid shape.</param>
    [Theory]
    [InlineData("USD", true)]
    [InlineData("usd", true)]
    [InlineData("uSd", true)]
    [InlineData(" USD", false)]
    [InlineData("USD ", false)]
    [InlineData("12!", false)]
    [InlineData("123", false)]
    [InlineData("US", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_AcceptsOnlyThreeAsciiLetters(string? currency, bool expected)
    {
        CurrencyCode.IsValid(currency).Should().Be(expected);
    }

    /// <summary>The non-throwing form trims and upper-cases a good code and reports a bad one.</summary>
    [Fact]
    public void TryNormalise_TrimsAndUpperCasesAGoodCodeAndRefusesABadOne()
    {
        CurrencyCode.TryNormalise(" eur ", out var good).Should().BeTrue();
        good.Should().Be("EUR");

        CurrencyCode.TryNormalise("12!", out var bad).Should().BeFalse();
        bad.Should().BeEmpty();

        CurrencyCode.TryNormalise(null, out _).Should().BeFalse();
    }

    /// <summary>The exponent is the ISO 4217 one, whatever the code's case.</summary>
    /// <param name="code">The currency.</param>
    /// <param name="expected">The exponent.</param>
    [Theory]
    [InlineData("USD", 2)]
    [InlineData("jpy", 0)]
    [InlineData("OMR", 3)]
    public void MinorUnitExponent_FollowsIso4217(string code, int expected)
    {
        CurrencyCode.MinorUnitExponent(code).Should().Be(expected);
    }

    /// <summary>An amount is rounded to the currency's minor unit, half away from zero.</summary>
    /// <param name="amount">The amount, invariant text so it reaches the test as an exact decimal.</param>
    /// <param name="code">The currency.</param>
    /// <param name="expected">The rounded amount, invariant text.</param>
    [Theory]
    [InlineData("10.005", "USD", "10.01")]
    [InlineData("33.3333", "EUR", "33.33")]
    [InlineData("1500.5", "JPY", "1501")]
    [InlineData("1.0005", "KWD", "1.001")]
    public void RoundToMinorUnits_RoundsToTheCurrencyMinorUnit(string amount, string code, string expected)
    {
        CurrencyCode.RoundToMinorUnits(decimal.Parse(amount, CultureInfo.InvariantCulture), code)
            .Should().Be(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// An amount becomes whole minor units in a zero, two or three decimal currency, rounding half
    /// away from zero and never truncating, including amounts that do not divide evenly.
    /// </summary>
    /// <param name="amount">The amount, invariant text so it reaches the test as an exact decimal.</param>
    /// <param name="code">The currency.</param>
    /// <param name="expected">The minor-unit integer.</param>
    [Theory]
    [InlineData("10.00", "USD", 1000)]
    [InlineData("10.005", "USD", 1001)]
    [InlineData("0.999", "USD", 100)]
    [InlineData("19.994", "EUR", 1999)]
    [InlineData("1500", "JPY", 1500)]
    [InlineData("1500.5", "JPY", 1501)]
    [InlineData("1.0005", "KWD", 1001)]
    [InlineData("0.0004", "KWD", 0)]
    public void ToMinorUnits_RoundsAndNeverTruncates(string amount, string code, long expected)
    {
        CurrencyCode.ToMinorUnits(decimal.Parse(amount, CultureInfo.InvariantCulture), code).Should().Be(expected);
    }

    /// <summary>Minor units convert back to the exact major-unit amount in zero, two and three decimal currencies.</summary>
    /// <param name="minor">The minor-unit integer.</param>
    /// <param name="code">The currency.</param>
    /// <param name="expected">The major-unit amount, invariant text.</param>
    [Theory]
    [InlineData(1001, "USD", "10.01")]
    [InlineData(1501, "JPY", "1501")]
    [InlineData(1001, "KWD", "1.001")]
    public void FromMinorUnits_RestoresTheMajorUnitAmount(long minor, string code, string expected)
    {
        CurrencyCode.FromMinorUnits(minor, code).Should().Be(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }
}
