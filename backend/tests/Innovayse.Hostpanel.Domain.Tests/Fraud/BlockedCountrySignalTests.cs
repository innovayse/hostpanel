using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for <see cref="BlockedCountrySignal"/>.</summary>
public sealed class BlockedCountrySignalTests
{
    /// <summary>The billing country and the country of the IP address are each checked against the list, in any case.</summary>
    /// <param name="billing">The billing country.</param>
    /// <param name="ipCountry">The country of the IP address.</param>
    /// <param name="fires">Whether the signal fires.</param>
    [Theory]
    [InlineData("KP", null, true)]
    [InlineData("kp", null, true)]
    [InlineData(null, "KP", true)]
    [InlineData("AM", "KP", true)]
    [InlineData("AM", "US", false)]
    [InlineData(null, null, false)]
    public void Evaluate_ChecksBothCountries(string? billing, string? ipCountry, bool fires)
    {
        var context = FraudContexts.Request(
            billingCountry: billing, ipCountry: ipCountry, blockList: FraudContexts.Listing((BlockListType.Country, "KP")));

        var hit = new BlockedCountrySignal().Evaluate(context);

        (hit is not null).Should().Be(fires);
        hit?.Reason.Should().Be(FraudReasonCode.BlockedCountry);
    }
}
