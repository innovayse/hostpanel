using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>
/// The one place the SDK's hosting records and the host's own are translated into each other, so the host API adapter
/// never builds either by hand.
/// </summary>
public static class PluginHostingMappingExtensions
{
    /// <summary>Maps the website a plugin asks for to the host's request shape.</summary>
    /// <param name="spec">The SDK's specification.</param>
    /// <returns>The host's specification carrying the same values.</returns>
    public static HostedWebsiteSpec ToHosted(this PluginWebsiteSpec spec)
    {
        return new HostedWebsiteSpec(spec.Hostname, spec.DocumentRoot, spec.PhpVersion, spec.DocumentRootMayBeLink);
    }

    /// <summary>Maps the capabilities the host reports to the record a plugin reads.</summary>
    /// <param name="capabilities">The host's capabilities of the service.</param>
    /// <returns>The SDK's record carrying the same values.</returns>
    public static PluginHostingCapabilities ToPlugin(this HostedCapabilities capabilities)
    {
        return new PluginHostingCapabilities(
            capabilities.Ssh, capabilities.Websites, capabilities.Databases, capabilities.PhpVersions);
    }

    /// <summary>Maps a website the host holds to the record a plugin receives.</summary>
    /// <param name="website">The host's website.</param>
    /// <returns>The SDK's record carrying the same values.</returns>
    public static PluginWebsite ToPlugin(this HostedWebsite website)
    {
        return new PluginWebsite(website.Id, website.Hostname, website.DocumentRoot, website.DocumentRootPath, website.PhpVersion)
        {
            PhpCliPath = website.PhpCliPath,
        };
    }

    /// <summary>Maps a database the host holds to the record a plugin receives, carrying the password only when the host has one.</summary>
    /// <param name="database">The host's database.</param>
    /// <returns>The SDK's record carrying the same values.</returns>
    public static PluginDatabase ToPlugin(this HostedDatabase database)
    {
        return new PluginDatabase(database.Id, database.Name, database.Username, database.Host, database.Port, database.Password);
    }
}
