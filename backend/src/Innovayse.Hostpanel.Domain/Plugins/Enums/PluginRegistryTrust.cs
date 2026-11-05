namespace Innovayse.Hostpanel.Domain.Plugins.Enums;

/// <summary>
/// Whose plugins an operator installs from the plugin registry. Stored as the name in the setting
/// <c>plugins.registry_trust</c>. Every policy still requires a valid package signature; the policy only narrows
/// which registry entries and which signing keys are accepted on top of it.
/// </summary>
public enum PluginRegistryTrust
{
    /// <summary>Only registry entries the registry marks as verified. The default and the safest choice.</summary>
    VerifiedOnly = 1,

    /// <summary>Any publisher listed in the registry, verified or not.</summary>
    AnyRegistryPublisher = 2,

    /// <summary>Only packages signed with a key the operator listed themselves (<c>PluginSigning:OperatorKeys</c>).</summary>
    OwnKeysOnly = 3,
}
