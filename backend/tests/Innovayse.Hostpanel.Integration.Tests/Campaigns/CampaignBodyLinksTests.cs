using Innovayse.Hostpanel.Application.Campaigns.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Unit tests for <see cref="CampaignBodyLinks"/>, the one reader of the links in a campaign body: which links are tracked, in
/// which order, and that nothing unsafe or unknown is ever rewritten or offered as a redirect target. No host is needed.
/// </summary>
public sealed class CampaignBodyLinksTests
{
    /// <summary>The replacement every rewritten link gets in these tests, numbered by the link's index.</summary>
    /// <param name="index">The link's index.</param>
    /// <returns>A recognisable replacement address.</returns>
    private static string Tracked(int index) => $"https://t.example/{index}";

    /// <summary>Absolute http and https links are listed in the order they appear.</summary>
    [Fact]
    public void Extract_ListsHttpAndHttpsLinksInOrder()
    {
        var links = CampaignBodyLinks.Extract("<p><a href=\"https://shop.example/a\">A</a> and <a href='http://shop.example/b?x=1'>B</a></p>");

        links.Should().Equal("https://shop.example/a", "http://shop.example/b?x=1");
    }

    /// <summary>An ampersand written as an entity in the attribute is the plain character in the address.</summary>
    [Fact]
    public void Extract_DecodesHtmlEntitiesInTheAddress()
    {
        var links = CampaignBodyLinks.Extract("<a href=\"https://shop.example/?a=1&amp;b=2\">x</a>");

        links.Should().Equal("https://shop.example/?a=1&b=2");
    }

    /// <summary>The tag may carry other attributes, in any order and in any letter case.</summary>
    [Fact]
    public void Extract_ReadsTheHrefAmongOtherAttributes()
    {
        var links = CampaignBodyLinks.Extract("<A class=\"btn\" HREF = \"https://shop.example/a\" target=\"_blank\">go</A>");

        links.Should().Equal("https://shop.example/a");
    }

    /// <summary>
    /// A link that is not an absolute http or https address is never listed, so it can never be a redirect target: scripts,
    /// data documents, mail and phone links, in-page anchors, relative and scheme-relative paths, and blank values.
    /// </summary>
    /// <param name="href">The attribute value.</param>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://files.example/a")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+37400000000")]
    [InlineData("#section")]
    [InlineData("/relative/path")]
    [InlineData("//evil.example/path")]
    [InlineData("relative.html")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("&#106;avascript:alert(1)")]
    [InlineData("https://shop.example/a&#10;b")]
    public void Extract_NeverListsAnythingButAbsoluteHttpLinks(string href)
    {
        CampaignBodyLinks.Extract($"<a href=\"{href}\">x</a>").Should().BeEmpty();
    }

    /// <summary>A link longer than the limit is left alone.</summary>
    [Fact]
    public void Extract_SkipsALinkLongerThanTheLimit()
    {
        var tooLong = "https://shop.example/" + new string('a', CampaignBodyLinks.MaxUrlLength);

        CampaignBodyLinks.Extract($"<a href=\"{tooLong}\">x</a>").Should().BeEmpty();
    }

    /// <summary>A body without anchors, or with an anchor that has no href, lists nothing.</summary>
    [Fact]
    public void Extract_BodyWithoutLinks_IsEmpty()
    {
        CampaignBodyLinks.Extract("<p>Hello <b>world</b></p><a name=\"top\">anchor</a><span data-href=\"https://x.example\"></span>")
            .Should().BeEmpty();
    }

    /// <summary>Only trackable links are replaced, each by the replacement of its own index, and the markup around them is untouched.</summary>
    [Fact]
    public void Rewrite_ReplacesOnlyTrackableLinks_AndKeepsTheRest()
    {
        const string body =
            "<p><a class=\"c\" href=\"https://shop.example/a\" title=\"t\">A</a> <a href=\"mailto:x@example.com\">mail</a> <a href='http://shop.example/b'>B</a></p>";

        var rewritten = CampaignBodyLinks.Rewrite(body, Tracked);

        rewritten.Should().Be(
            "<p><a class=\"c\" href=\"https://t.example/0\" title=\"t\">A</a> <a href=\"mailto:x@example.com\">mail</a> <a href='https://t.example/1'>B</a></p>");
    }

    /// <summary>Unsafe links stay exactly as written, since a rewritten one would turn a harmless dead link into a tracked redirect.</summary>
    [Fact]
    public void Rewrite_LeavesUnsafeLinksAsWritten()
    {
        const string body = "<a href=\"javascript:alert(1)\">x</a><a href=\"//evil.example\">y</a><a href=\"data:text/html,x\">z</a>";

        CampaignBodyLinks.Rewrite(body, Tracked).Should().Be(body);
    }

    /// <summary>The index a rewritten link carries names, in the extracted list, the address the link had.</summary>
    [Fact]
    public void Rewrite_AndExtract_AgreeOnEveryIndex()
    {
        const string body =
            "<a href=\"#top\">top</a><a href=\"https://one.example\">1</a><a href=\"tel:1\">t</a><a href=\"http://two.example/p\">2</a><a href=\"https://one.example\">again</a>";
        var seen = new List<int>();

        CampaignBodyLinks.Rewrite(body, index =>
        {
            seen.Add(index);
            return Tracked(index);
        });
        var links = CampaignBodyLinks.Extract(body);

        seen.Should().Equal(0, 1, 2);
        links.Should().Equal("https://one.example/", "http://two.example/p", "https://one.example/");
    }

    /// <summary>A body without links is returned unchanged.</summary>
    [Fact]
    public void Rewrite_BodyWithoutLinks_IsUnchanged()
    {
        const string body = "<p>Hello</p>";

        CampaignBodyLinks.Rewrite(body, Tracked).Should().Be(body);
    }

    /// <summary>A body made of unclosed tags is read in time linear in its size, so it cannot stall the sender.</summary>
    [Fact]
    public void Extract_ManyUnclosedTags_FinishesQuickly()
    {
        var body = string.Concat(Enumerable.Repeat("<a href=\"https://shop.example/x", 20_000));

        var started = DateTime.UtcNow;
        CampaignBodyLinks.Extract(body);

        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
}
