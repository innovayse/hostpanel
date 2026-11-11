using Innovayse.Hostpanel.Application.Campaigns.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>Unit tests for <see cref="CampaignTrackingLinks"/>: the addresses a campaign mail carries and the body built from them. No host is needed.</summary>
public sealed class CampaignTrackingLinksTests
{
    /// <summary>The portal address the tests build links against, written with a trailing slash on purpose.</summary>
    private const string Portal = "https://portal.example/";

    /// <summary>A token of the real shape.</summary>
    private static readonly string Token = new('a', 64);

    /// <summary>The pixel and click addresses sit under the portal's origin and the shared route prefix, whatever the trailing slash.</summary>
    [Fact]
    public void Urls_SitUnderThePortalAndTheRoutePrefix()
    {
        CampaignTrackingLinks.OpenUrl(Portal, Token).Should().Be($"https://portal.example/api/public/campaign-mail/open/{Token}");
        CampaignTrackingLinks.ClickUrl(Portal, Token, 3).Should().Be($"https://portal.example/api/public/campaign-mail/click/{Token}/3");
    }

    /// <summary>Every trackable link is pointed at the click endpoint of its own index, and the pixel ends the body.</summary>
    [Fact]
    public void Track_RewritesTheLinks_AndAppendsThePixel()
    {
        var body = CampaignTrackingLinks.Track(
            "<p><a href=\"https://shop.example/a\">A</a> <a href=\"mailto:x@example.com\">mail</a> <a href=\"https://shop.example/b\">B</a></p>",
            Portal,
            Token);

        body.Should().StartWith(
            $"<p><a href=\"https://portal.example/api/public/campaign-mail/click/{Token}/0\">A</a> <a href=\"mailto:x@example.com\">mail</a> "
            + $"<a href=\"https://portal.example/api/public/campaign-mail/click/{Token}/1\">B</a></p>");
        body.Should().EndWith(">").And.Contain($"<img src=\"https://portal.example/api/public/campaign-mail/open/{Token}\" width=\"1\" height=\"1\" alt=\"\"");
        body.Should().NotContain("shop.example");
    }

    /// <summary>A body with no links still gets the pixel, so a plain mail can be counted as opened.</summary>
    [Fact]
    public void Track_BodyWithoutLinks_StillGetsThePixel()
    {
        var body = CampaignTrackingLinks.Track("<p>Hello</p>", Portal, Token);

        body.Should().StartWith("<p>Hello</p><img src=\"").And.Contain("/api/public/campaign-mail/open/");
    }

    /// <summary>A blank portal address or token is refused instead of producing a broken link.</summary>
    [Fact]
    public void Track_BlankPortalOrToken_Throws()
    {
        var blankPortal = () => CampaignTrackingLinks.Track("<p>x</p>", " ", Token);
        var blankToken = () => CampaignTrackingLinks.Track("<p>x</p>", Portal, " ");

        blankPortal.Should().Throw<ArgumentException>();
        blankToken.Should().Throw<ArgumentException>();
    }
}
