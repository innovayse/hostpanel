using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <see cref="VatRegisters"/>: which countries have a VAT number register and how a client's number is put in lookup shape.
/// Pure logic; no host is started.
/// </summary>
public sealed class VatRegistersTests
{
    /// <summary>Every EU member state and the United Kingdom have a register, in any case and with spaces around the code.</summary>
    /// <param name="country">A country code as a client record may hold it.</param>
    [Theory]
    [InlineData("DE")]
    [InlineData("de")]
    [InlineData(" fr ")]
    [InlineData("GR")]
    [InlineData("GB")]
    public void Covers_EuMemberStatesAndTheUnitedKingdom(string country)
    {
        VatRegisters.Covers(country).Should().BeTrue();
    }

    /// <summary>No other country, and no country at all, has a register to ask.</summary>
    /// <param name="country">A country code, or null.</param>
    [Theory]
    [InlineData("US")]
    [InlineData("NO")]
    [InlineData("CH")]
    [InlineData("AM")]
    [InlineData("")]
    [InlineData(null)]
    public void Covers_NothingElse(string? country)
    {
        VatRegisters.Covers(country).Should().BeFalse();
    }

    /// <summary>The countries are exactly the EU member states the reports list, plus the United Kingdom.</summary>
    [Fact]
    public void Countries_AreTheEuMemberStatesAndTheUnitedKingdom()
    {
        VatRegisters.Countries.Should().BeEquivalentTo(EuMemberStates.Codes.Append("GB"));
    }

    /// <summary>Spaces, dots, dashes and case do not change the number, and a typed country prefix is cut.</summary>
    /// <param name="country">The country.</param>
    /// <param name="typed">The number as a client typed it.</param>
    /// <param name="expected">The number the register is asked about.</param>
    [Theory]
    [InlineData("DE", "DE123456789", "123456789")]
    [InlineData("DE", "de 123.456.789", "123456789")]
    [InlineData("DE", "123456789", "123456789")]
    [InlineData("NL", "NL123456789B01", "123456789B01")]
    [InlineData("ES", "ES-A1234567B", "A1234567B")]
    [InlineData("GB", "GB 123 4567 89", "123456789")]
    [InlineData("GR", "EL123456789", "123456789")]
    [InlineData("GR", "gr123456789", "123456789")]
    [InlineData("gr", "123456789", "123456789")]
    public void LookupFor_PutsTheNumberInLookupShape(string country, string typed, string expected)
    {
        var lookup = VatRegisters.LookupFor(country, typed);

        lookup.Country.Should().Be(country.ToUpperInvariant());
        lookup.Number.Should().Be(expected);
    }

    /// <summary>A number that is only its prefix is empty, so the check can tell it holds nothing to look up.</summary>
    [Fact]
    public void LookupFor_APrefixAlone_IsAnEmptyNumber()
    {
        VatRegisters.LookupFor("DE", " de ").Number.Should().BeEmpty();
    }

    /// <summary>Only Greece spells its prefix differently from its country code.</summary>
    [Fact]
    public void PrefixOf_IsTheCountryCode_ExceptForGreece()
    {
        VatRegisters.PrefixOf("DE").Should().Be("DE");
        VatRegisters.PrefixOf("GB").Should().Be("GB");
        VatRegisters.PrefixOf("GR").Should().Be("EL");
    }
}
