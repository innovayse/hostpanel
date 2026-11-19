namespace Innovayse.Hostpanel.Domain.Plugins.Enums;

/// <summary>Where an installed plugin package came from. Stored as the name for an uploaded or registry package.</summary>
public enum PluginSource
{
    /// <summary>The plugin ships with the build; it is never recorded as an install, because it is part of the image.</summary>
    BuiltIn = 1,

    /// <summary>An operator uploaded the package file by hand.</summary>
    Uploaded = 2,

    /// <summary>The package was fetched from the plugin registry.</summary>
    Registry = 3,
}
