using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <see cref="PluginIconPaths"/>: the icon endpoint is cached by address, so the address a plugin row carries must
/// change with the plugin's version, or an updated plugin keeps showing its old icon until the cache entry expires.
/// </summary>
public sealed class PluginIconPathsTests
{
    /// <summary>The address names the plugin and carries its version as the <c>v</c> parameter.</summary>
    [Fact]
    public void For_NamesThePluginAndCarriesItsVersion()
    {
        PluginIconPaths.For("acme-panel", "1.2.0").Should().Be("/api/admin/plugins/acme-panel/icon?v=1.2.0");
    }

    /// <summary>A new version is a new address, so the browser does not reuse the cached icon of the old one.</summary>
    [Fact]
    public void For_GivesANewAddressForANewVersion()
    {
        PluginIconPaths.For("acme-panel", "1.2.0").Should().NotBe(PluginIconPaths.For("acme-panel", "1.3.0"));
    }

    /// <summary>A version with a character that is special in a query string (a build suffix) is escaped, never splitting the query.</summary>
    [Fact]
    public void For_EscapesAVersionThatIsSpecialInAQuery()
    {
        PluginIconPaths.For("acme-panel", "1.0.0+build&x=1").Should().Be("/api/admin/plugins/acme-panel/icon?v=1.0.0%2Bbuild%26x%3D1");
    }
}
