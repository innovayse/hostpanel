using Hostpanel.Sdk.Plugins.Payment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The parameters a payment plugin receives from the return request: names compared ordinally, the first value of a name
/// wins, and a name without text or a missing value is not a parameter. The limits of <see cref="PaymentReturnLimits"/> are held
/// by the validators of the complete routes, not here.
/// </summary>
public sealed class PaymentReturnParameterExtensionsTests
{
    /// <summary>Builds the return request from name and value pairs.</summary>
    /// <param name="pairs">The pairs, in the order they arrived.</param>
    /// <returns>The parameters the plugin would receive.</returns>
    private static IReadOnlyDictionary<string, string> Pass(params (string Name, string Value)[] pairs)
    {
        return pairs.Select(p => new PaymentReturnParameterDto(p.Name, p.Value)).ToList().ToPluginParameters();
    }

    /// <summary>Builds the pairs of a set that sits exactly at the count limit.</summary>
    /// <returns>One pair per allowed parameter.</returns>
    private static List<(string Name, string Value)> FullSet()
    {
        return Enumerable.Range(0, PaymentReturnLimits.MaxParameters).Select(i => (Name: $"p{i}", Value: "v")).ToList();
    }

    /// <summary>The first value of a repeated name wins and the later ones are dropped.</summary>
    [Fact]
    public void TheFirstValueOfAName_Wins()
    {
        var parameters = Pass(("token", "first"), ("other", "o"), ("token", "second"));

        parameters.Should().BeEquivalentTo(new Dictionary<string, string> { ["token"] = "first", ["other"] = "o" });
    }

    /// <summary>Names are compared ordinally, so two spellings are two names.</summary>
    [Fact]
    public void Names_AreComparedOrdinally()
    {
        var parameters = Pass(("Token", "upper"), ("token", "lower"));

        parameters.Should().HaveCount(2);
        parameters["Token"].Should().Be("upper");
        parameters["token"].Should().Be("lower");
        parameters.ContainsKey("TOKEN").Should().BeFalse();
    }

    /// <summary>A return that carried nothing gives an empty set.</summary>
    [Fact]
    public void ANoParameterReturn_GivesAnEmptySet()
    {
        Pass().Should().BeEmpty();
    }

    /// <summary>A name without text and a missing value are not parameters.</summary>
    [Fact]
    public void ANameWithoutText_OrAMissingValue_IsLeftOut()
    {
        var returned = new List<PaymentReturnParameterDto>
        {
            new(string.Empty, "v"),
            new("missing-value", null!),
            new("kept", string.Empty),
        };

        returned.ToPluginParameters().Should().BeEquivalentTo(new Dictionary<string, string> { ["kept"] = string.Empty });
    }

    /// <summary>A set at the limits is passed on whole.</summary>
    [Fact]
    public void ASetAtTheLimits_IsPassedOnWhole()
    {
        var pairs = FullSet();
        pairs[0] = (new string('n', PaymentReturnLimits.MaxNameLength), new string('v', PaymentReturnLimits.MaxValueLength));

        Pass([.. pairs]).Should().HaveCount(PaymentReturnLimits.MaxParameters);
    }
}
