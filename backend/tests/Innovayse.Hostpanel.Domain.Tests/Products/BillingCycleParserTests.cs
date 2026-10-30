using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Products;

/// <summary>Tests for <see cref="BillingCycleParser"/>: every spelling stored or imported so far still reads back, and the newer cycles read by name.</summary>
public sealed class BillingCycleParserTests
{
    /// <summary>The spellings older rows and importers carry still read as the cycle they always did.</summary>
    /// <param name="raw">The stored or imported text.</param>
    /// <param name="expected">The cycle it must read as.</param>
    [Theory]
    [InlineData("monthly", BillingCycle.Monthly)]
    [InlineData("Monthly", BillingCycle.Monthly)]
    [InlineData("annual", BillingCycle.Annual)]
    [InlineData("annually", BillingCycle.Annual)]
    [InlineData("  ANNUAL ", BillingCycle.Annual)]
    public void TryParse_ExistingSpellings_ReadAsBefore(string raw, BillingCycle expected)
    {
        BillingCycleParser.TryParse(raw, out var cycle).Should().BeTrue();
        cycle.Should().Be(expected);
    }

    /// <summary>The cycles added later read by their member name, in any casing, and the hyphenated semi-annual spelling.</summary>
    /// <param name="raw">The stored or imported text.</param>
    /// <param name="expected">The cycle it must read as.</param>
    [Theory]
    [InlineData("Quarterly", BillingCycle.Quarterly)]
    [InlineData("quarterly", BillingCycle.Quarterly)]
    [InlineData("SemiAnnual", BillingCycle.SemiAnnual)]
    [InlineData("semiannual", BillingCycle.SemiAnnual)]
    [InlineData("semi-annual", BillingCycle.SemiAnnual)]
    [InlineData("Biennial", BillingCycle.Biennial)]
    public void TryParse_NewCycles_ReadByName(string raw, BillingCycle expected)
    {
        BillingCycleParser.TryParse(raw, out var cycle).Should().BeTrue();
        cycle.Should().Be(expected);
    }

    /// <summary>Every member's own name reads back as that member, so a stored row always round-trips.</summary>
    [Fact]
    public void TryParse_EveryMemberName_RoundTrips()
    {
        foreach (var member in Enum.GetValues<BillingCycle>())
        {
            BillingCycleParser.TryParse(member.ToString(), out var cycle).Should().BeTrue();
            cycle.Should().Be(member);
        }
    }

    /// <summary>Text that names no cycle, or nothing at all, is refused rather than guessed.</summary>
    /// <param name="raw">The text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("weekly")]
    [InlineData("biannual")]
    public void TryParse_Unknown_ReturnsFalse(string? raw)
    {
        BillingCycleParser.TryParse(raw, out _).Should().BeFalse();
    }
}
