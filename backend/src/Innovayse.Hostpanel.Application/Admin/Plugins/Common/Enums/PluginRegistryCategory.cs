namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>The category a plugin is listed under in the plugin registry; serialised as the name.</summary>
public enum PluginRegistryCategory
{
    /// <summary>A payment gateway plugin.</summary>
    Payment = 1,

    /// <summary>A provisioning plugin.</summary>
    Provisioning = 2,

    /// <summary>A domain registrar plugin.</summary>
    Registrar = 3,

    /// <summary>An app plugin with its own pages and routes.</summary>
    App = 4,

    /// <summary>Anything else.</summary>
    Other = 5,
}
