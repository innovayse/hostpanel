using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginFrameOrigin"/>: the text it answers reaches a response header and the panel's exact frame check, so
/// only a canonical origin passes and everything that could carry a delimiter or another scheme answers nothing.
/// </summary>
public sealed class PluginFrameOriginTests
{
    /// <summary>A pure https origin reads as a browser writes it: lower case, default port dropped, punycode.</summary>
    /// <param name="text">The entry.</param>
    /// <param name="expected">The canonical origin.</param>
    [Theory]
    [InlineData("https://docs.example.org", "https://docs.example.org")]
    [InlineData("https://DOCS.Example.org", "https://docs.example.org")]
    [InlineData("https://example.org:443", "https://example.org")]
    [InlineData("https://example.org:8443", "https://example.org:8443")]
    [InlineData("https://bücher.example", "https://xn--bcher-kva.example")]
    public void FromHttps_PureOrigin_IsCanonical(string text, string expected)
    {
        PluginFrameOrigin.FromHttps(text).Should().Be(expected);
    }

    /// <summary>Anything but a pure https origin on a DNS name answers nothing.</summary>
    /// <param name="text">The entry.</param>
    [Theory]
    [InlineData("")]
    [InlineData("example.org")]
    [InlineData("http://example.org")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://example.org/")]
    [InlineData("https://example.org/path")]
    [InlineData("https://example.org?a=1")]
    [InlineData("https://example.org#a")]
    [InlineData("https://user:pw@example.org")]
    [InlineData("https://localhost")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://[::1]")]
    [InlineData("https://*.example.org")]
    [InlineData("https://example.org; script-src *")]
    [InlineData("https://example.org\r\nX-Evil: 1")]
    public void FromHttps_AnythingElse_IsNull(string text)
    {
        PluginFrameOrigin.FromHttps(text).Should().BeNull();
    }

    /// <summary>A loopback http origin is accepted for development, and only that.</summary>
    /// <param name="text">The entry.</param>
    /// <param name="expected">The canonical origin, or null when it is refused.</param>
    [Theory]
    [InlineData("http://localhost:8088", "http://localhost:8088")]
    [InlineData("http://site.localhost:8088", "http://site.localhost:8088")]
    [InlineData("http://127.0.0.1:8088", "http://127.0.0.1:8088")]
    [InlineData("http://[::1]:8088", "http://[::1]:8088")]
    [InlineData("http://example.org:8088", null)]
    [InlineData("http://localhost.example.org", null)]
    [InlineData("https://localhost:8088", null)]
    [InlineData("http://localhost:8088/path", null)]
    public void FromLoopbackHttp_AnswersOnlyLoopback(string text, string? expected)
    {
        PluginFrameOrigin.FromLoopbackHttp(text).Should().Be(expected);
    }

    /// <summary>A domain gives its https origin and its www name, whichever of the two was stored.</summary>
    /// <param name="domain">The stored domain.</param>
    [Theory]
    [InlineData("example.com")]
    [InlineData("EXAMPLE.com")]
    [InlineData(" example.com ")]
    [InlineData("www.example.com")]
    public void FromDomain_PlainName_GivesBothOrigins(string domain)
    {
        PluginFrameOrigin.FromDomain(domain).Should().Equal("https://example.com", "https://www.example.com");
    }

    /// <summary>A value a client typed that is no plain DNS name gives nothing, above all nothing that could break a header.</summary>
    /// <param name="domain">The stored domain.</param>
    [Theory]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("1.2.3.4")]
    [InlineData("example.com:8080")]
    [InlineData("example.com/path")]
    [InlineData("https://example.com")]
    [InlineData("example.com; script-src *")]
    [InlineData("example.com evil.example")]
    [InlineData("*.example.com")]
    [InlineData("example.com.")]
    public void FromDomain_AnythingElse_GivesNothing(string domain)
    {
        PluginFrameOrigin.FromDomain(domain).Should().BeEmpty();
    }
}
