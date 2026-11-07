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

    /// <summary>An https address on the server's host or IP address is accepted, whatever its case or port.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("https://whm.example.test:2083/cpsess1/login/?session=u:abc")]
    [InlineData("https://WHM.Example.TEST:2083/cpsess1/login/")]
    [InlineData("https://whm.example.test/cpsess1/login/")]
    [InlineData("https://192.0.2.10:2083/cpsess1/login/")]
    public void IsSessionUrlOf_OwnHttpsAddress_IsAccepted(string url)
    {
        WhmInputRules.IsSessionUrlOf(url, WhmAddress, ServerIp).Should().BeTrue();
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
    public void IsSessionUrlOf_WrongHost_IsRefused(string url)
    {
        WhmInputRules.IsSessionUrlOf(url, WhmAddress, ServerIp).Should().BeFalse();
    }

    /// <summary>A plain http address on the right host is refused: the session token must not travel in clear text.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("http://whm.example.test:2083/cpsess1/login/")]
    [InlineData("ftp://whm.example.test/cpsess1/login/")]
    [InlineData("javascript:alert(1)")]
    public void IsSessionUrlOf_NotHttps_IsRefused(string url)
    {
        WhmInputRules.IsSessionUrlOf(url, WhmAddress, ServerIp).Should().BeFalse();
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
    public void IsSessionUrlOf_RelativeOrMalformed_IsRefused(string? url)
    {
        WhmInputRules.IsSessionUrlOf(url, WhmAddress, ServerIp).Should().BeFalse();
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
    public void IsSessionUrlOf_UserInfoTricks_AreRefused(string url)
    {
        WhmInputRules.IsSessionUrlOf(url, WhmAddress, ServerIp).Should().BeFalse();
    }

    /// <summary>Whitespace or a control character anywhere in the value is refused, so nothing is smuggled into a header or a redirect.</summary>
    /// <param name="url">The URL WHM returned.</param>
    [Theory]
    [InlineData("https://whm.example.test/cpsess1/\r\nSet-Cookie: x=1")]
    [InlineData("https://whm.example.test/cpsess1/ login")]
    [InlineData("https://whm.example.test/\u0000")]
    public void IsSessionUrlOf_WhitespaceOrControlCharacter_IsRefused(string url)
    {
        WhmInputRules.IsSessionUrlOf(url, WhmAddress, ServerIp).Should().BeFalse();
    }

    /// <summary>A server configured by an internationalised name matches its punycode address, in any case.</summary>
    [Fact]
    public void IsSessionUrlOf_InternationalisedHost_ComparesInPunycode()
    {
        WhmInputRules.IsSessionUrlOf("https://XN--80AK6AA92E.com:2083/cpsess1/", "https://буква.com:2087").Should().BeFalse("a different name");
        WhmInputRules.IsSessionUrlOf("https://xn--80ak6aa92e.com:2083/cpsess1/", "https://xn--80ak6aa92e.com:2087").Should().BeTrue();
        WhmInputRules.IsSessionUrlOf("https://XN--80AK6AA92E.com:2083/cpsess1/", "https://xn--80ak6aa92e.com:2087").Should().BeTrue();
    }

    /// <summary>Blank own hosts match nothing, so an unset address never lets a URL through.</summary>
    [Fact]
    public void IsSessionUrlOf_NoOwnHost_IsRefused()
    {
        WhmInputRules.IsSessionUrlOf("https://whm.example.test/cpsess1/", null, " ", string.Empty).Should().BeFalse();
    }

    /// <summary>An IPv6 address of the server matches its bracketed URL form.</summary>
    [Fact]
    public void IsSessionUrlOf_Ipv6Address_Matches()
    {
        WhmInputRules.IsSessionUrlOf("https://[2001:db8::10]:2083/cpsess1/", "2001:db8::10").Should().BeTrue();
        WhmInputRules.IsSessionUrlOf("https://[2001:db8::11]:2083/cpsess1/", "2001:db8::10").Should().BeFalse();
    }
}
