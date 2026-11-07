using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// <see cref="WhmInputRules"/>: the usernames and domains WHM may be sent, and the single sign-on
/// addresses a server may hand out.
/// </summary>
public sealed class WhmInputRulesTests
{
    /// <summary>The server's own WHM address, as the provider holds it.</summary>
    private const string WhmAddress = "https://whm.example.test:2087";

    /// <summary>The cPanel address account owners sign in at, as the provider holds it.</summary>
    private const string PanelUrl = "https://whm.example.test:2083";

    /// <summary>The server's IP address, as the provider holds it.</summary>
    private const string ServerIp = "192.0.2.10";

    /// <summary>A username cPanel can create is accepted.</summary>
    /// <param name="value">The username.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("testuser")]
    [InlineData("user123")]
    [InlineData("abcdefghijklmnop")]
    public void IsNewUsername_ValidName_IsAccepted(string value)
    {
        WhmInputRules.IsNewUsername(value).Should().BeTrue();
    }

    /// <summary>A blank, too long, capitalised, digit first or punctuated username is refused.</summary>
    /// <param name="value">The username.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abcdefghijklmnopq")]
    [InlineData("User")]
    [InlineData("1user")]
    [InlineData("_user")]
    [InlineData("user_name")]
    [InlineData("user-name")]
    [InlineData("user name")]
    [InlineData("user&x=1")]
    [InlineData("usér")]
    public void IsNewUsername_InvalidName_IsRefused(string? value)
    {
        WhmInputRules.IsNewUsername(value).Should().BeFalse();
    }

    /// <summary>An existing account's name may carry the characters older accounts have.</summary>
    /// <param name="value">The username.</param>
    [Theory]
    [InlineData("testuser")]
    [InlineData("Legacy_User")]
    [InlineData("old-account")]
    [InlineData("a.b")]
    public void IsAccountReference_StoredName_IsAccepted(string value)
    {
        WhmInputRules.IsAccountReference(value).Should().BeTrue();
    }

    /// <summary>A blank value, a separator, whitespace or an overlong name is never sent as a username.</summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("_user")]
    [InlineData("user name")]
    [InlineData("user\nname")]
    [InlineData("user&x=1")]
    [InlineData("user=1")]
    [InlineData("a/b")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    public void IsAccountReference_UnsafeValue_IsRefused(string? value)
    {
        WhmInputRules.IsAccountReference(value).Should().BeFalse();
    }

    /// <summary>A well formed domain, including a punycode one and a mixed case one, is accepted.</summary>
    /// <param name="value">The domain.</param>
    [Theory]
    [InlineData("example.com")]
    [InlineData("Example.COM")]
    [InlineData("sub.example.co.uk")]
    [InlineData("xn--80ak6aa92e.com")]
    [InlineData("my-site.example")]
    public void IsDomain_ValidName_IsAccepted(string value)
    {
        WhmInputRules.IsDomain(value).Should().BeTrue();
    }

    /// <summary>A blank value, a single label, an IP address, a bad label or a smuggled argument is refused.</summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("localhost")]
    [InlineData("192.0.2.10")]
    [InlineData("example.com.")]
    [InlineData(".example.com")]
    [InlineData("exa..mple.com")]
    [InlineData("-example.com")]
    [InlineData("example-.com")]
    [InlineData("exa mple.com")]
    [InlineData("example.com&plan=root")]
    [InlineData("example.com/path")]
    [InlineData("exämple.com")]
    public void IsDomain_InvalidName_IsRefused(string? value)
    {
        WhmInputRules.IsDomain(value).Should().BeFalse();
    }

    /// <summary>A label of 64 characters, and a name past 253 characters, are refused; 63 and 253 are not.</summary>
    [Fact]
    public void IsDomain_LengthLimits_AreEnforced()
    {
        WhmInputRules.IsDomain(new string('a', WhmInputRules.MaxLabelLength) + ".com").Should().BeTrue();
        WhmInputRules.IsDomain(new string('a', WhmInputRules.MaxLabelLength + 1) + ".com").Should().BeFalse();
        var label = new string('a', WhmInputRules.MaxLabelLength);
        WhmInputRules.IsDomain($"{label}.{label}.{label}.{new string('a', 61)}").Should().BeTrue("253 characters is the limit");
        WhmInputRules.IsDomain($"{label}.{label}.{label}.{new string('a', 62)}").Should().BeFalse("254 characters is past it");
    }

    /// <summary>An https address on the server's host or IP address and the cPanel port is accepted, whatever its case.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("https://whm.example.test:2083/cpsess1/login/?session=u:abc")]
    [InlineData("https://WHM.Example.TEST:2083/cpsess1/login/")]
    [InlineData("https://192.0.2.10:2083/cpsess1/login/")]
    public void ParseSessionUrl_OwnHttpsAddress_IsAccepted(string url)
    {
        WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp).Should().NotBeNull();
    }

    /// <summary>An address on another host, a lookalike host or a parent domain is refused.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("https://evil.example:2083/cpsess1/login/")]
    [InlineData("https://whm.example.test.evil.example/cpsess1/login/")]
    [InlineData("https://evilwhm.example.test/cpsess1/login/")]
    [InlineData("https://example.test/cpsess1/login/")]
    [InlineData("https://sub.whm.example.test/cpsess1/login/")]
    [InlineData("https://192.0.2.11:2083/cpsess1/login/")]
    public void ParseSessionUrl_WrongHost_IsRefused(string url)
    {
        WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp).Should().BeNull();
    }

    /// <summary>
    /// A host written with a trailing dot, or with a percent encoded character, is not the server's
    /// own name: whatever the parser makes of it, a result is never a host other than the server's.
    /// </summary>
    /// <param name="url">The URL WHM returned.</param>
    /// <param name="expectedHost">The server host the address resolves to, or <see langword="null"/> when it must be refused.</param>
    [Theory]
    [InlineData("https://whm.example.test.:2083/cpsess1/login/", null)]
    [InlineData("https://192.0.2.10.:2083/cpsess1/login/", null)]
    [InlineData("https://%77hm.example.test:2083/cpsess1/login/", null)]
    [InlineData("https://whm%2eexample.test:2083/cpsess1/login/", null)]
    [InlineData("https://whm.example.test%2f.evil.example:2083/cpsess1/login/", null)]
    [InlineData("https://whm.example.test%00.evil.example:2083/cpsess1/login/", null)]
    public void ParseSessionUrl_TrailingDotOrPercentEncodedHost_IsNeverTakenForAnotherHost(string url, string? expectedHost)
    {
        var parsed = WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp);

        if (expectedHost is null)
        {
            parsed.Should().BeNull();
        }
        else
        {
            parsed.Should().NotBeNull();
            parsed!.IdnHost.Should().Be(expectedHost);
        }
    }

    /// <summary>A host with a trailing dot is a different name and is refused.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("https://whm.example.test.:2083/cpsess1/login/")]
    [InlineData("https://192.0.2.10.:2083/cpsess1/login/")]
    public void ParseSessionUrl_TrailingDot_IsRefused(string url)
    {
        WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp).Should().BeNull();
    }

    /// <summary>Only the cPanel port passes: another port on the server's own host, the WHM port and an absent port included, is refused.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("https://whm.example.test:2087/cpsess1/login/")]
    [InlineData("https://whm.example.test:8443/cpsess1/login/")]
    [InlineData("https://whm.example.test:2084/cpsess1/login/")]
    [InlineData("https://whm.example.test/cpsess1/login/")]
    [InlineData("https://whm.example.test:443/cpsess1/login/")]
    [InlineData("https://192.0.2.10:2087/cpsess1/login/")]
    [InlineData("https://192.0.2.10/cpsess1/login/")]
    public void ParseSessionUrl_PortOtherThanTheCPanelPort_IsRefused(string url)
    {
        WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp).Should().BeNull();
    }

    /// <summary>An address with no port is on the cPanel port only when the server is configured with the https default.</summary>
    [Fact]
    public void ParseSessionUrl_PanelOnThePortHttpsDefaults_AcceptsAnAddressWithoutAPort()
    {
        WhmInputRules.ParseSessionUrl("https://whm.example.test/cpsess1/", "https://whm.example.test", WhmAddress).Should().NotBeNull();
        WhmInputRules.ParseSessionUrl("https://whm.example.test:443/cpsess1/", "https://whm.example.test:443", WhmAddress).Should().NotBeNull();
        WhmInputRules.ParseSessionUrl("https://whm.example.test:2083/cpsess1/", "https://whm.example.test", WhmAddress).Should().BeNull();
    }

    /// <summary>With no usable cPanel address nothing passes, so an unknown port never lets a URL through.</summary>
    /// <param name="panelUrl">The cPanel address the provider holds.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("whm.example.test:2083")]
    [InlineData("http://whm.example.test:2083")]
    public void ParseSessionUrl_NoUsablePanelUrl_IsRefused(string? panelUrl)
    {
        WhmInputRules.ParseSessionUrl("https://whm.example.test:2083/cpsess1/", panelUrl, WhmAddress, ServerIp).Should().BeNull();
    }

    /// <summary>The result is the parsed absolute address that was checked, normalised, not the original text.</summary>
    [Fact]
    public void ParseSessionUrl_ReturnsTheParsedAddress()
    {
        var parsed = WhmInputRules.ParseSessionUrl("HTTPS://WHM.Example.TEST:2083/cpsess1/login/?session=u:abc", PanelUrl, WhmAddress, ServerIp);

        parsed.Should().NotBeNull();
        parsed!.IsAbsoluteUri.Should().BeTrue();
        parsed.AbsoluteUri.Should().Be("https://whm.example.test:2083/cpsess1/login/?session=u:abc");
    }

    /// <summary>A plain http address on the right host is refused: the session token must not travel in clear text.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("http://whm.example.test:2083/cpsess1/login/")]
    [InlineData("ftp://whm.example.test/cpsess1/login/")]
    [InlineData("javascript:alert(1)")]
    public void ParseSessionUrl_NotHttps_IsRefused(string url)
    {
        WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp).Should().BeNull();
    }

    /// <summary>A relative, scheme relative, blank or malformed value is not an address on the server.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/cpsess1/login/")]
    [InlineData("cpsess1/login/")]
    [InlineData("//whm.example.test/cpsess1/login/")]
    [InlineData("https:///cpsess1/login/")]
    [InlineData("https://")]
    public void ParseSessionUrl_RelativeOrMalformed_IsRefused(string? url)
    {
        WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp).Should().BeNull();
    }

    /// <summary>User information in front of the host, with either host in it, never passes.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("https://whm.example.test@evil.example/cpsess1/login/")]
    [InlineData("https://evil.example@whm.example.test/cpsess1/login/")]
    [InlineData("https://whm.example.test:2083@evil.example/")]
    [InlineData("https://user:pass@whm.example.test/cpsess1/login/")]
    [InlineData("https://evil.example\\@whm.example.test/cpsess1/login/")]
    [InlineData("https://evil.example#@whm.example.test/cpsess1/login/")]
    [InlineData("https://evil.example?@whm.example.test/cpsess1/login/")]
    public void ParseSessionUrl_UserInfoTricks_AreRefused(string url)
    {
        WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp).Should().BeNull();
    }

    /// <summary>Whitespace or a control character anywhere in the value is refused, so nothing is smuggled into a header or a redirect.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("https://whm.example.test/cpsess1/\r\nSet-Cookie: x=1")]
    [InlineData("https://whm.example.test/cpsess1/ login")]
    [InlineData("https://whm.example.test/\u0000")]
    public void ParseSessionUrl_WhitespaceOrControlCharacter_IsRefused(string url)
    {
        WhmInputRules.ParseSessionUrl(url, PanelUrl, WhmAddress, ServerIp).Should().BeNull();
    }

    /// <summary>A server configured by an internationalised name matches its punycode address, in any case.</summary>
    [Fact]
    public void ParseSessionUrl_InternationalisedHost_ComparesInPunycode()
    {
        WhmInputRules.ParseSessionUrl("https://XN--80AK6AA92E.com:2083/cpsess1/", PanelUrl, "https://буква.com:2087").Should().BeNull("a different name");
        WhmInputRules.ParseSessionUrl("https://xn--80ak6aa92e.com:2083/cpsess1/", PanelUrl, "https://xn--80ak6aa92e.com:2087").Should().NotBeNull();
        WhmInputRules.ParseSessionUrl("https://XN--80AK6AA92E.com:2083/cpsess1/", PanelUrl, "https://xn--80ak6aa92e.com:2087").Should().NotBeNull();
    }

    /// <summary>Blank own hosts match nothing, so an unset address never lets a URL through.</summary>
    [Fact]
    public void ParseSessionUrl_NoOwnHost_IsRefused()
    {
        WhmInputRules.ParseSessionUrl("https://whm.example.test:2083/cpsess1/", PanelUrl, null, " ", string.Empty).Should().BeNull();
    }

    /// <summary>An IPv6 address of the server matches its bracketed URL form.</summary>
    [Fact]
    public void ParseSessionUrl_Ipv6Address_Matches()
    {
        WhmInputRules.ParseSessionUrl("https://[2001:db8::10]:2083/cpsess1/", PanelUrl, "2001:db8::10").Should().NotBeNull();
        WhmInputRules.ParseSessionUrl("https://[2001:db8::11]:2083/cpsess1/", PanelUrl, "2001:db8::10").Should().BeNull();
    }
}
