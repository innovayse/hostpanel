using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Payment;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Hostpanel.Sdk.Plugins.Tax;
using Hostpanel.Sdk.Plugins.VirtualServers;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one table of the capabilities a manifest may declare: for each name from <see cref="PluginCapabilities"/> the SDK
/// interface the plugin class must implement, the plugin type that may carry it and the capability it needs beside it.
/// The manifest validator (what a manifest may say), the loader (whether an endpoints plugin can answer) and
/// <see cref="PluginActivator"/> (whether the loaded class keeps the promise) all ask it, so the three can never disagree
/// about what a name means.
/// </summary>
/// <remarks>
/// A name in the table only means the plugin loads and is shown with the capability. Nothing here calls the contract: the
/// host starts to use one when the use case that needs it is written, and a capability the host does not use yet is still
/// checked against its interface. A name missing from the table is refused by the validator and fails closed as not
/// implemented in the activator.
/// </remarks>
internal static class PluginCapabilityRules
{
    /// <summary>The rule of each capability, keyed by its manifest name and compared exactly.</summary>
    private static readonly IReadOnlyDictionary<string, PluginCapabilityRule> Rules =
        new Dictionary<string, PluginCapabilityRule>(StringComparer.Ordinal)
        {
            [PluginCapabilities.StoredCredentials] = new(typeof(IStoredCredentialPlugin), PluginType.Payment, null),
            [PluginCapabilities.PaymentReturn] = new(typeof(IPaymentReturnPlugin), PluginType.Payment, null),
            [PluginCapabilities.Endpoints] = new(typeof(IPluginEndpoints), null, null),
            [PluginCapabilities.Tax] = new(typeof(ITaxPlugin), null, null),
            [PluginCapabilities.ProvisioningPanel] = new(typeof(IProvisioningPanelPlugin), PluginType.Provisioning, null),
            [PluginCapabilities.ProvisioningPanelLogin] = new(
                typeof(IProvisioningPanelLoginPlugin), PluginType.Provisioning, PluginCapabilities.ProvisioningPanel),
            [PluginCapabilities.ProvisioningPanelPackages] = new(
                typeof(IProvisioningPanelPackagesPlugin), PluginType.Provisioning, PluginCapabilities.ProvisioningPanel),
            [PluginCapabilities.ProvisioningPanelAccount] = new(
                typeof(IProvisioningPanelAccountPlugin), PluginType.Provisioning, PluginCapabilities.ProvisioningPanel),
            [PluginCapabilities.VirtualServer] = new(typeof(IVirtualServerPlugin), PluginType.Provisioning, null),
            [PluginCapabilities.VirtualServerList] = new(typeof(IVirtualServerListPlugin), PluginType.Provisioning, null),
            [PluginCapabilities.VirtualServerSnapshots] = new(
                typeof(IVirtualServerSnapshotPlugin), PluginType.Provisioning, PluginCapabilities.VirtualServer),
            [PluginCapabilities.VirtualServerBackups] = new(
                typeof(IVirtualServerBackupPlugin), PluginType.Provisioning, PluginCapabilities.VirtualServer),
            [PluginCapabilities.VirtualServerFirewall] = new(
                typeof(IVirtualServerFirewallPlugin), PluginType.Provisioning, PluginCapabilities.VirtualServer),

            // A promise about one member of IVirtualServerPlugin, so the class is held to that interface and nothing more.
            [PluginCapabilities.VirtualServerConsole] = new(
                typeof(IVirtualServerPlugin), PluginType.Provisioning, PluginCapabilities.VirtualServer),
            [PluginCapabilities.RegistrarOperations] = new(typeof(IRegistrarOperationsPlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarEmailForwarding] = new(typeof(IRegistrarEmailForwardingPlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarDnsManagement] = new(typeof(IRegistrarDnsManagementPlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarWhois] = new(typeof(IRegistrarWhoisPlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarChildNameservers] = new(typeof(IRegistrarChildNameserversPlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarDnssec] = new(typeof(IRegistrarDnssecPlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarRestore] = new(typeof(IRegistrarRestorePlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarDomainList] = new(typeof(IRegistrarDomainListPlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarSuggestions] = new(typeof(IRegistrarSuggestionsPlugin), PluginType.Registrar, null),
            [PluginCapabilities.RegistrarBalance] = new(typeof(IRegistrarBalancePlugin), PluginType.Registrar, null),
        };

    /// <summary>
    /// Checks that every capability a manifest declares is one the host knows, declared once, carried by a plugin type that
    /// may carry it, and declared together with the capability it needs.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The <see cref="PluginManifestRule.CapabilityNotAllowed"/> violation, or <see langword="null"/>.</returns>
    internal static PluginManifestViolation? Validate(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var capability in manifest.Capabilities)
        {
            // A repeat is refused rather than ignored: it is a manifest mistake, and the list is shown to the operator.
            if (!seen.Add(capability))
            {
                return NotAllowed($"capabilities names '{capability}' twice");
            }

            if (!Rules.TryGetValue(capability, out var rule))
            {
                return NotAllowed($"capabilities names '{capability}', which this host does not define");
            }

            if (rule.OnlyType is { } type && manifest.Type != type)
            {
                return NotAllowed($"capabilities names '{capability}', which only a {type} plugin may carry");
            }
        }

        // After the loop, so the order of the list never decides whether a companion counts as declared.
        foreach (var capability in manifest.Capabilities)
        {
            if (Rules[capability].Requires is { } required && !seen.Contains(required))
            {
                return NotAllowed($"capabilities names '{capability}', which needs '{required}' as well");
            }
        }

        return null;
    }

    /// <summary>Finds the first capability the manifest declares that the implementation type does not implement.</summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <param name="implementation">The type the plugin loaded as.</param>
    /// <returns>The capability name, or <see langword="null"/> when every declared capability is implemented.</returns>
    internal static string? FirstUnimplemented(PluginManifest manifest, Type implementation)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return manifest.Capabilities.FirstOrDefault(capability => !IsImplementedBy(capability, implementation));
    }

    /// <summary>
    /// Tells whether the manifest declares a capability that is an operation on a provisioned server, a hosting panel or a
    /// virtual server platform: one whose interface derives from <see cref="IProvisioningPlugin"/>. Such a plugin is sent the
    /// servers of the modules it lists, so it is the only kind that may list any.
    /// </summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <returns><see langword="true"/> when at least one declared capability is a provisioning one.</returns>
    internal static bool DeclaresProvisioning(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return manifest.Capabilities.Any(capability =>
            Rules.TryGetValue(capability, out var rule) && typeof(IProvisioningPlugin).IsAssignableFrom(rule.Contract));
    }

    /// <summary>Tells whether a type implements the interface a capability promises.</summary>
    /// <param name="capability">A capability name.</param>
    /// <param name="implementation">The type the plugin loaded as.</param>
    /// <returns><see langword="false"/> for a name the table does not know, so an unchecked promise fails closed.</returns>
    internal static bool IsImplementedBy(string capability, Type implementation)
    {
        ArgumentNullException.ThrowIfNull(implementation);

        return Rules.TryGetValue(capability, out var rule) && rule.Contract.IsAssignableFrom(implementation);
    }

    /// <summary>Builds the violation every capability rule reports.</summary>
    /// <param name="detail">Which capability breaks which rule.</param>
    /// <returns>The <see cref="PluginManifestRule.CapabilityNotAllowed"/> violation.</returns>
    private static PluginManifestViolation NotAllowed(string detail)
    {
        return new PluginManifestViolation(PluginManifestRule.CapabilityNotAllowed, detail);
    }
}
