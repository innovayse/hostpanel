namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>Where a registry plugin's package can be obtained from; serialised as the name.</summary>
public enum PluginRegistryDistribution
{
    /// <summary>Offered to everyone: the version details and the package need no proof of identity.</summary>
    Download = 1,

    /// <summary>
    /// Offered only to a verified installation: the version list, the version details, the download ticket and the
    /// package are asked for with a request signed by this installation's registry identity.
    /// </summary>
    HostOnly = 2,
}
