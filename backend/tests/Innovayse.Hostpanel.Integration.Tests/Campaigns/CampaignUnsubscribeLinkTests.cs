using Innovayse.Hostpanel.Application.Campaigns.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>Tests for the two addresses a campaign mail carries for one recipient.</summary>
public sealed class CampaignUnsubscribeLinkTests
{
    /// <summary>The page link keeps its shape: the portal's <c>/unsubscribe</c> with the token in the query.</summary>
    [Fact]
    public void For_BuildsThePortalPageLink()
    {
        CampaignUnsubscribeLink.For("https://portal.example.test/", "abc").Should().Be("https://portal.example.test/unsubscribe?token=abc");
    }

    /// <summary>The one-click address is the API endpoint on the same host, with the token in the query and no doubled slash.</summary>
    [Fact]
    public void OneClickFor_BuildsTheEndpointAddressOnThePublicHost()
    {
        CampaignUnsubscribeLink.OneClickFor("https://portal.example.test/", "abc")
            .Should().Be("https://portal.example.test/api/public/unsubscribe/one-click?token=abc");
    }

    /// <summary>A token with characters that are special in a query is escaped, so the address names exactly that token.</summary>
    [Fact]
    public void OneClickFor_EscapesTheToken()
    {
        CampaignUnsubscribeLink.OneClickFor("https://portal.example.test", "a b&c")
            .Should().EndWith("?token=a%20b%26c");
    }

    /// <summary>A blank token or base is a programming error, not an address.</summary>
    /// <param name="baseUrl">The base to build on.</param>
    /// <param name="token">The token to carry.</param>
    [Theory]
    [InlineData("", "abc")]
    [InlineData("https://portal.example.test", " ")]
    public void OneClickFor_BlankArgument_Throws(string baseUrl, string token)
    {
        var act = () => CampaignUnsubscribeLink.OneClickFor(baseUrl, token);

        act.Should().Throw<ArgumentException>();
    }
}
