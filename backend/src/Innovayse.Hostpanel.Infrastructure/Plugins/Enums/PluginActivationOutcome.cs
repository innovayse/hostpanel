namespace Innovayse.Hostpanel.Infrastructure.Plugins.Enums;

/// <summary>How an attempt to activate a loaded plugin ended; only <see cref="Activated"/> yields an instance.</summary>
public enum PluginActivationOutcome
{
    /// <summary>The plugin is enabled, has every required setting and was instantiated.</summary>
    Activated = 0,

    /// <summary>
    /// The loaded implementation type does not implement the contract asked for: the manifest's type is only
    /// a claim the author made, and casting on it would throw out of the caller's path.
    /// </summary>
    ContractNotImplemented = 1,

    /// <summary>The admin has not switched the plugin on; its <c>is_enabled</c> setting is absent or not <c>true</c>.</summary>
    Disabled = 2,

    /// <summary>The plugin is switched on but a field its manifest marks required holds no value.</summary>
    RequiredSettingMissing = 3,
}
