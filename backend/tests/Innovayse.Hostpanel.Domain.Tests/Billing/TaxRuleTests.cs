using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for <see cref="TaxRule"/>: its fields, their limits and the normalising of the place.</summary>
public class TaxRuleTests
{
    /// <summary>Creating a rule stores the fields, writes the country upper case and trims the region.</summary>
    [Fact]
    public void Create_StoresTheFieldsAndNormalisesThePlace()
    {
        var rule = TaxRule.Create(" Germany VAT ", " de ", "  Bayern ", 19m, isEnabled: true);

        rule.Name.Should().Be("Germany VAT");
        rule.Country.Should().Be("DE");
        rule.Region.Should().Be("Bayern");
        rule.RatePercent.Should().Be(19m);
        rule.IsEnabled.Should().BeTrue();
    }

    /// <summary>A blank or missing region makes the rule country-wide.</summary>
    /// <param name="region">A region that names no region.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutARegion_IsCountryWide(string? region)
    {
        var rule = TaxRule.Create("Germany VAT", "DE", region, 19m, isEnabled: true);

        rule.Region.Should().BeNull();
    }

    /// <summary>The ends of the rate range, 0 and 100 percent, are accepted.</summary>
    /// <param name="ratePercent">A rate on the edge of the range.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_AcceptsTheEndsOfTheRateRange(int ratePercent)
    {
        var rule = TaxRule.Create("Edge", "DE", null, ratePercent, isEnabled: true);

        rule.RatePercent.Should().Be(ratePercent);
    }

    /// <summary>A rate outside 0 to 100 is refused with its code.</summary>
    /// <param name="ratePercent">A rate outside the range.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Create_RefusesARateOutsideTheRange(double ratePercent)
    {
        var act = () => TaxRule.Create("Bad", "DE", null, (decimal)ratePercent, isEnabled: true);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.TaxRuleRateInvalid);
    }

    /// <summary>A blank name, a country that is not two letters and a text over its column are refused.</summary>
    [Fact]
    public void Create_RefusesAnInvalidNameCountryOrRegion()
    {
        var blankName = () => TaxRule.Create(" ", "DE", null, 1m, isEnabled: true);
        var longName = () => TaxRule.Create(new string('a', TaxRule.NameMaxLength + 1), "DE", null, 1m, isEnabled: true);
        var shortCountry = () => TaxRule.Create("Rule", "D", null, 1m, isEnabled: true);
        var longCountry = () => TaxRule.Create("Rule", "DEU", null, 1m, isEnabled: true);
        var digitCountry = () => TaxRule.Create("Rule", "D1", null, 1m, isEnabled: true);
        var longRegion = () => TaxRule.Create("Rule", "DE", new string('a', TaxRule.RegionMaxLength + 1), 1m, isEnabled: true);

        blankName.Should().Throw<ArgumentException>();
        longName.Should().Throw<ArgumentException>();
        shortCountry.Should().Throw<ArgumentException>();
        longCountry.Should().Throw<ArgumentException>();
        digitCountry.Should().Throw<ArgumentException>();
        longRegion.Should().Throw<ArgumentException>();
    }

    /// <summary>Updating replaces every field, including the place and the switch.</summary>
    [Fact]
    public void Update_ReplacesEveryField()
    {
        var rule = TaxRule.Create("Germany VAT", "DE", null, 19m, isEnabled: true);

        rule.Update("Madeira VAT", "pt", "Madeira", 22m, isEnabled: false);

        rule.Name.Should().Be("Madeira VAT");
        rule.Country.Should().Be("PT");
        rule.Region.Should().Be("Madeira");
        rule.RatePercent.Should().Be(22m);
        rule.IsEnabled.Should().BeFalse();
    }

    /// <summary>A refused update leaves the rule as it was.</summary>
    [Fact]
    public void Update_WithARateOutsideTheRange_LeavesTheRuleUnchanged()
    {
        var rule = TaxRule.Create("Germany VAT", "DE", null, 19m, isEnabled: true);

        var act = () => rule.Update("Other", "FR", null, 101m, isEnabled: false);

        act.Should().Throw<DomainRuleException>();
        rule.Name.Should().Be("Germany VAT");
        rule.Country.Should().Be("DE");
        rule.RatePercent.Should().Be(19m);
        rule.IsEnabled.Should().BeTrue();
    }
}
