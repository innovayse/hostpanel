using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// The shapes a plugin's website or database request must have. They mirror what the hosting agent accepts, so a request
/// the agent would refuse is refused first, and the document root path is built the one way the agent resolves it.
/// </summary>
public sealed class HostingSpecRulesTests
{
    /// <summary>Lower case domain names are host names; anything else the agent refuses is not.</summary>
    /// <param name="hostname">The text.</param>
    /// <param name="expected">Whether it is a host name.</param>
    [Theory]
    [InlineData("example.com", true)]
    [InlineData("app.example.com", true)]
    [InlineData("a-b.c-d.example.co.uk", true)]
    [InlineData("Example.com", false)]
    [InlineData("example", false)]
    [InlineData("-bad.example.com", false)]
    [InlineData("bad_name.example.com", false)]
    [InlineData("exa mple.com", false)]
    [InlineData("example.com\n", false)]
    [InlineData("", false)]
    public void IsHostname_AcceptsOnlyLowerCaseDomainNames(string hostname, bool expected)
    {
        HostingSpecRules.IsHostname(hostname).Should().Be(expected);
    }

    /// <summary>Only the service's own domain and its subdomains are host names of the service.</summary>
    /// <param name="hostname">The host name.</param>
    /// <param name="domain">The service's domain.</param>
    /// <param name="expected">Whether the host name belongs to it.</param>
    [Theory]
    [InlineData("example.com", "example.com", true)]
    [InlineData("app.example.com", "example.com", true)]
    [InlineData("a.b.example.com", " Example.COM ", true)]
    [InlineData("notexample.com", "example.com", false)]
    [InlineData("example.com.evil.net", "example.com", false)]
    [InlineData("app.example.com", null, false)]
    [InlineData("app.example.com", "  ", false)]
    public void IsHostnameOfDomain_AcceptsTheDomainAndItsSubdomainsOnly(string hostname, string? domain, bool expected)
    {
        HostingSpecRules.IsHostnameOfDomain(hostname, domain).Should().Be(expected);
    }

    /// <summary>A document root is one to six plain relative segments, none starting with a dot, at most 200 characters.</summary>
    /// <param name="documentRoot">The text.</param>
    /// <param name="expected">Whether it is accepted.</param>
    [Theory]
    [InlineData("public_html", true)]
    [InlineData("sites/app/current/public", true)]
    [InlineData("a.b/c-d_e", true)]
    [InlineData("a/b/c/d/e/f", true)]
    [InlineData("a/b/c/d/e/f/g", false)]
    [InlineData("/etc", false)]
    [InlineData("../bob", false)]
    [InlineData("a/../b", false)]
    [InlineData(".ssh", false)]
    [InlineData("a/.hidden", false)]
    [InlineData("a//b", false)]
    [InlineData("a b", false)]
    [InlineData("a/", false)]
    [InlineData("", false)]
    public void IsDocumentRoot_AcceptsOnlyPlainRelativePaths(string documentRoot, bool expected)
    {
        HostingSpecRules.IsDocumentRoot(documentRoot).Should().Be(expected);
    }

    /// <summary>A document root longer than the agent accepts is refused even when every segment is plain.</summary>
    [Fact]
    public void IsDocumentRoot_RefusesMoreThanTwoHundredCharacters()
    {
        var segment = new string('a', 60);

        HostingSpecRules.IsDocumentRoot($"{segment}/{segment}/{segment}").Should().BeTrue();
        HostingSpecRules.IsDocumentRoot($"{segment}/{segment}/{segment}/{segment}").Should().BeFalse();
    }

    /// <summary>A PHP version is a digit, a dot and one or two digits.</summary>
    /// <param name="phpVersion">The text.</param>
    /// <param name="expected">Whether it is accepted.</param>
    [Theory]
    [InlineData("8.3", true)]
    [InlineData("7.4", true)]
    [InlineData("8.10", true)]
    [InlineData("8", false)]
    [InlineData("8.", false)]
    [InlineData("8.3.1", false)]
    [InlineData("php8.3", false)]
    [InlineData("", false)]
    public void IsPhpVersion_AcceptsOnlyMajorDotMinor(string phpVersion, bool expected)
    {
        HostingSpecRules.IsPhpVersion(phpVersion).Should().Be(expected);
    }

    /// <summary>A database label is a lower case letter followed by up to fourteen lower case letters or digits.</summary>
    /// <param name="label">The text.</param>
    /// <param name="expected">Whether it is accepted.</param>
    [Theory]
    [InlineData("shop", true)]
    [InlineData("a", true)]
    [InlineData("abcdefghijklmno", true)]
    [InlineData("abcdefghijklmnop", false)]
    [InlineData("1shop", false)]
    [InlineData("Shop", false)]
    [InlineData("my_shop", false)]
    [InlineData("", false)]
    public void IsDatabaseLabel_AcceptsOnlyTheAgentsShape(string label, bool expected)
    {
        HostingSpecRules.IsDatabaseLabel(label).Should().Be(expected);
    }

    /// <summary>The document root path is the account's home plus the relative root.</summary>
    [Fact]
    public void DocumentRootPath_IsTheAccountsHomePlusTheRoot()
    {
        HostingSpecRules.DocumentRootPath("alice", "sites/app").Should().Be("/home/alice/sites/app");
    }
}
