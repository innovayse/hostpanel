using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for <see cref="VatRate"/>: its fields, their limits and the normalising of the country.</summary>
public class VatRateTests
{
    /// <summary>Creating a row stores the fields and writes the country upper case.</summary>
    [Fact]
    public void Create_StoresTheFieldsAndNormalisesTheCountry()
    {
        var rate = VatRate.Create(" fi ", VatRateKind.Standard, 25.5m, new DateOnly(2024, 9, 1));

        rate.Country.Should().Be("FI");
        rate.Kind.Should().Be(VatRateKind.Standard);
        rate.RatePercent.Should().Be(25.5m);
        rate.ValidFrom.Should().Be(new DateOnly(2024, 9, 1));
    }

    /// <summary>Updating replaces every field, the way a correction of a mistyped row does.</summary>
    [Fact]
    public void Update_ReplacesEveryField()
    {
        var rate = VatRate.Create("DE", VatRateKind.Standard, 19m, new DateOnly(2024, 1, 1));

        rate.Update("at", VatRateKind.Reduced, 10m, new DateOnly(2025, 1, 1));

        rate.Country.Should().Be("AT");
        rate.Kind.Should().Be(VatRateKind.Reduced);
        rate.RatePercent.Should().Be(10m);
        rate.ValidFrom.Should().Be(new DateOnly(2025, 1, 1));
    }

    /// <summary>The ends of the rate range, 0 and 100 percent, are accepted.</summary>
    /// <param name="ratePercent">A rate on the edge of the range.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_AcceptsTheEndsOfTheRateRange(int ratePercent)
    {
        var rate = VatRate.Create("DE", VatRateKind.Standard, ratePercent, new DateOnly(2024, 1, 1));

        rate.RatePercent.Should().Be(ratePercent);
    }

    /// <summary>A rate outside 0 to 100 is refused with its code.</summary>
    /// <param name="ratePercent">A rate outside the range.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Create_RefusesARateOutsideTheRange(double ratePercent)
    {
        var act = () => VatRate.Create("DE", VatRateKind.Standard, (decimal)ratePercent, new DateOnly(2024, 1, 1));

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.VatRateInvalid);
    }

    /// <summary>A blank country and a country that is not two letters are refused.</summary>
    /// <param name="country">A text that is not a country code.</param>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("D")]
    [InlineData("DEU")]
    [InlineData("1A")]
    public void Create_RefusesAnInvalidCountry(string country)
    {
        var act = () => VatRate.Create(country, VatRateKind.Standard, 19m, new DateOnly(2024, 1, 1));

        act.Should().Throw<ArgumentException>();
    }
}
