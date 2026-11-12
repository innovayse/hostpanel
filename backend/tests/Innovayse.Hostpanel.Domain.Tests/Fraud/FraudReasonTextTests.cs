using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for the stored spelling of a list of reasons in <see cref="FraudReasonText"/>.</summary>
public sealed class FraudReasonTextTests
{
    /// <summary>Reasons are written between commas and read back in order, each once.</summary>
    [Fact]
    public void FormatAndParse_RoundTripInOrder_WithoutRepeats()
    {
        var text = FraudReasonText.Format([FraudReasonCode.DisposableEmail, FraudReasonCode.BlockedIp, FraudReasonCode.DisposableEmail]);

        text.Should().Be(",DisposableEmail,BlockedIp,");
        FraudReasonText.Parse(text).Should().Equal(FraudReasonCode.DisposableEmail, FraudReasonCode.BlockedIp);
    }

    /// <summary>No reason is the empty text, and the empty text is no reason.</summary>
    [Fact]
    public void Format_NoReasons_IsEmpty_AndParseEmptyIsNone()
    {
        FraudReasonText.Format([]).Should().BeEmpty();
        FraudReasonText.Parse(string.Empty).Should().BeEmpty();
        FraudReasonText.Parse(null).Should().BeEmpty();
    }

    /// <summary>The marker of one reason is found in a text that holds it, and never in a text that only holds a longer name.</summary>
    [Fact]
    public void Marker_MatchesOnlyTheWholeName()
    {
        var text = FraudReasonText.Format([FraudReasonCode.BlockedEmailDomain]);

        text.Contains(FraudReasonText.Marker(FraudReasonCode.BlockedEmailDomain)).Should().BeTrue();
        text.Contains(FraudReasonText.Marker(FraudReasonCode.BlockedIp)).Should().BeFalse();
    }
}
