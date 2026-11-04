namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>How a registry plugin is priced; serialised as the name.</summary>
public enum PluginRegistryPricing
{
    /// <summary>Free to install.</summary>
    Free = 1,

    /// <summary>Paid; the publisher sells it.</summary>
    Paid = 2,

    /// <summary>Private to the publisher's customers.</summary>
    Private = 3,
}
