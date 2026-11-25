using Innovayse.Hostpanel.Infrastructure.Provisioning;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="PanelSsoLinkRules"/>: the one rule a panel sign in link meets before it reaches a browser, for every panel plugin.
/// </summary>
public sealed class PanelSsoLinkRulesTests
{
    /// <summary>A secure address on the server's host passes, whatever the letter case of the host or the port.</summary>
    [Fact]
    public void Accept_HttpsOnTheServersHost_Passes()
    {
        var link = PanelSsoLinkRules.Accept("https://Panel.Example.Test:8443/login?t=abc", "panel.example.test");

        link.Should().NotBeNull();
        link!.Port.Should().Be(8443);
    }

    /// <summary>The host the API is dialled at counts as the server's own too.</summary>
    [Fact]
    public void Accept_TheApiHost_Passes()
    {
        PanelSsoLinkRules.Accept("https://api.example.test/login", "panel.example.test", "api.example.test").Should().NotBeNull();
    }

    /// <summary>A link on another host, over plain http, with user info or not an address at all is refused, and an empty host list refuses everything.</summary>
    /// <param name="link">The link the plugin answered.</param>
    [Theory]
    [InlineData("http://panel.example.test/login")]
    [InlineData("https://user:pass@panel.example.test/login")]
    [InlineData("https://user@panel.example.test/login")]
    [InlineData("https://elsewhere.example.test/login")]
    [InlineData("https://panel.example.test.evil.example/login")]
    [InlineData("//panel.example.test/login")]
    [InlineData("not a link")]
    [InlineData("")]
    [InlineData(null)]
    public void Accept_AnythingElse_IsRefused(string? link)
    {
        PanelSsoLinkRules.Accept(link, "panel.example.test", null).Should().BeNull();
        PanelSsoLinkRules.Accept("https://panel.example.test/login").Should().BeNull();
    }
}
