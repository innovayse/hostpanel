using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins.Extensions;

/// <summary><see cref="PluginHostingMappingExtensions"/>: a hosting record reaches a plugin with every value unchanged.</summary>
public sealed class PluginHostingMappingExtensionsTests
{
    /// <summary>Every value of a website is copied, the PHP CLI path included.</summary>
    [Fact]
    public void ToPlugin_Website_CopiesEveryValue()
    {
        var id = Guid.NewGuid();
        var website = new HostedWebsite(id, "app.example.test", "sites/app", "/home/web/sites/app", "8.3", "/usr/bin/php8.3");

        var result = website.ToPlugin();

        result.Should().Be(
            new PluginWebsite(id, "app.example.test", "sites/app", "/home/web/sites/app", "8.3") { PhpCliPath = "/usr/bin/php8.3" });
    }

    /// <summary>A website without PHP, or whose path is not known, reaches the plugin with both as null.</summary>
    [Fact]
    public void ToPlugin_Website_KeepsAnUnknownPhpCliPathNull()
    {
        var website = new HostedWebsite(Guid.NewGuid(), "app.example.test", "public_html", "/home/web/public_html", null, null);

        var result = website.ToPlugin();

        result.PhpVersion.Should().BeNull();
        result.PhpCliPath.Should().BeNull();
    }

    /// <summary>A website that runs PHP but has no known path reaches the plugin with the version and no path.</summary>
    [Fact]
    public void ToPlugin_Website_CarriesTheVersionWithoutAPath()
    {
        var website = new HostedWebsite(Guid.NewGuid(), "app.example.test", "public_html", "/home/web/public_html", "8.2", null);

        var result = website.ToPlugin();

        result.PhpVersion.Should().Be("8.2");
        result.PhpCliPath.Should().BeNull();
    }
}
