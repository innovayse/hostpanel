namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

/// <summary>Where a published plugin version stands in the registry.</summary>
public enum PluginRegistryVersionStatus
{
    /// <summary>The registry is still reviewing the version.</summary>
    InReview = 0,

    /// <summary>The version is published and can be installed.</summary>
    Published = 1,

    /// <summary>The registry rejected the version.</summary>
    Rejected = 2,

    /// <summary>The version still works but the publisher advises against it.</summary>
    Deprecated = 3,

    /// <summary>The version was withdrawn and should not be installed.</summary>
    Yanked = 4,
}
