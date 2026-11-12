using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for <see cref="CountryMismatchSignal"/>.</summary>
public sealed class CountryMismatchSignalTests
{
    /// <summary>The signal fires only when both countries are known and differ, ignoring case and spaces.</summary>
    /// <param name="billing">The billing country.</param>
    /// <param name="ipCountry">The country of the IP address.</param>
    /// <param name="fires">Whether the signal fires.</param>
    [Theory]
    [InlineData("AM", "US", true)]
    [InlineData("AM", "am", false)]
    [InlineData(" AM ", "AM", false)]
    [InlineData("AM", null, false)]
    [InlineData(null, "US", false)]
    [InlineData("", "US", false)]
    public void Evaluate_FiresWhenKnownCountriesDiffer(string? billing, string? ipCountry, bool fires)
    {
        var hit = new CountryMismatchSignal().Evaluate(FraudContexts.Request(billingCountry: billing, ipCountry: ipCountry));

        (hit is not null).Should().Be(fires);
        hit?.Reason.Should().Be(FraudReasonCode.CountryMismatch);
    }
}
