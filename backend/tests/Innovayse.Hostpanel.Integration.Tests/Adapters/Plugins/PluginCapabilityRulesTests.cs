using System.Reflection;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Payment;
using Hostpanel.Sdk.Plugins.Provisioning;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Hostpanel.Sdk.Plugins.VirtualServers;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginCapabilityRules"/>, the one table of the capabilities a manifest may declare: every name the SDK defines
/// is known, each is accepted on the plugin type that may carry it and refused on another, a capability that needs a
/// companion is refused without it, and the interface each name promises is the one the activator and the loader check.
/// No container or database.
/// </summary>
public sealed class PluginCapabilityRulesTests
{
    /// <summary>Reads a manifest of a type with the given capabilities.</summary>
    /// <param name="type">The plugin type, as the manifest spells it.</param>
    /// <param name="capabilities">The capability names, in the order they are listed.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string type, params string[] capabilities)
    {
        var list = string.Join(",", capabilities.Select(name => $"\"{name}\""));
        var json = "{\"id\":\"caps-plugin\",\"name\":\"P\",\"version\":\"1.0.0\",\"author\":\"A\",\"description\":\"D\","
            + $"\"type\":\"{type}\",\"category\":\"C\",\"entryPoint\":\"P.Plugin\",\"sdkVersion\":\"2.1\",\"manifestVersion\":2,"
            + $"\"capabilities\":[{list}]}}";
        return PluginManifestReader.Deserialize(json)!;
    }

    /// <summary>Every capability name the SDK defines is in the table, so a name the SDK adds later fails here until the host decides what it means.</summary>
    [Fact]
    public void EveryCapabilityOfTheSdk_IsKnownToTheTable()
    {
        var names = typeof(PluginCapabilities)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        names.Should().HaveCountGreaterThan(20);
        foreach (var name in names)
        {
            // A name the table does not know is refused with "does not define"; any other answer means it is known.
            var violation = PluginCapabilityRules.Validate(Manifest("Provisioning", name));
            (violation?.Detail ?? string.Empty).Should().NotContain("does not define", $"'{name}' must be in the table");
        }
    }

    /// <summary>A capability is accepted on the plugin type that may carry it, with the companion it needs.</summary>
    /// <param name="type">The plugin type.</param>
    /// <param name="capabilities">The capabilities, comma separated.</param>
    [Theory]
    [InlineData("Payment", "storedCredentials,paymentReturn")]
    [InlineData("Fulfilment", "endpoints,tax")]
    [InlineData("Payment", "tax")]
    [InlineData("Provisioning", "provisioningPanel")]
    [InlineData("Provisioning", "provisioningPanel,provisioningPanelLogin,provisioningPanelPackages")]
    [InlineData("Provisioning", "provisioningPanel,provisioningPanelAccount")]
    [InlineData("Provisioning", "provisioningPanelLogin,provisioningPanel")]
    [InlineData("Provisioning", "virtualServer,virtualServerList,virtualServerSnapshots,virtualServerBackups,virtualServerFirewall,virtualServerConsole")]
    [InlineData("Provisioning", "virtualServerList")]
    [InlineData("Registrar", "registrarOperations,registrarEmailForwarding,registrarDnsManagement,registrarWhois")]
    [InlineData("Registrar", "registrarChildNameservers,registrarDnssec,registrarRestore,registrarDomainList,registrarSuggestions,registrarBalance")]
    public void Validate_CapabilityOnItsOwnType_IsAccepted(string type, string capabilities)
    {
        PluginCapabilityRules.Validate(Manifest(type, capabilities.Split(','))).Should().BeNull();
    }

    /// <summary>A capability is refused on a plugin type that may not carry it.</summary>
    /// <param name="type">The plugin type.</param>
    /// <param name="capability">The capability.</param>
    [Theory]
    [InlineData("Payment", "provisioningPanel")]
    [InlineData("Payment", "provisioningPanelAccount")]
    [InlineData("Registrar", "provisioningPanelAccount")]
    [InlineData("Payment", "virtualServer")]
    [InlineData("Registrar", "provisioningPanel")]
    [InlineData("Registrar", "virtualServerList")]
    [InlineData("Provisioning", "registrarOperations")]
    [InlineData("Provisioning", "registrarWhois")]
    [InlineData("Payment", "registrarBalance")]
    [InlineData("Registrar", "storedCredentials")]
    [InlineData("Provisioning", "paymentReturn")]
    public void Validate_CapabilityOnAnotherType_IsRefused(string type, string capability)
    {
        var violation = PluginCapabilityRules.Validate(Manifest(type, capability));

        violation.Should().NotBeNull();
        violation!.Rule.Should().Be(PluginManifestRule.CapabilityNotAllowed);
        violation.Detail.Should().Contain(capability).And.Contain("may carry");
    }

    /// <summary>A capability that needs another is refused when the plugin does not declare the other, whatever else it declares.</summary>
    /// <param name="capability">The capability that needs a companion.</param>
    /// <param name="companion">The companion it needs.</param>
    [Theory]
    [InlineData("provisioningPanelLogin", "provisioningPanel")]
    [InlineData("provisioningPanelPackages", "provisioningPanel")]
    [InlineData("provisioningPanelAccount", "provisioningPanel")]
    [InlineData("virtualServerSnapshots", "virtualServer")]
    [InlineData("virtualServerBackups", "virtualServer")]
    [InlineData("virtualServerFirewall", "virtualServer")]
    [InlineData("virtualServerConsole", "virtualServer")]
    public void Validate_CapabilityWithoutItsCompanion_IsRefused(string capability, string companion)
    {
        var violation = PluginCapabilityRules.Validate(Manifest("Provisioning", capability));

        violation.Should().NotBeNull();
        violation!.Rule.Should().Be(PluginManifestRule.CapabilityNotAllowed);
        violation.Detail.Should().Contain(capability).And.Contain($"needs '{companion}'");
    }

    /// <summary>The panel companion does not stand in for the virtual server one: each capability names the one it needs.</summary>
    [Fact]
    public void Validate_CompanionOfAnotherFamily_DoesNotCount()
    {
        PluginCapabilityRules.Validate(Manifest("Provisioning", "provisioningPanel", "virtualServerSnapshots"))
            .Should().NotBeNull();
    }

    /// <summary>A repeated and an unknown capability are refused as before.</summary>
    [Fact]
    public void Validate_RepeatedAndUnknownCapabilities_AreRefused()
    {
        PluginCapabilityRules.Validate(Manifest("Registrar", "registrarWhois", "registrarWhois"))!.Detail.Should().Contain("twice");
        PluginCapabilityRules.Validate(Manifest("Registrar", "registrarTeleport"))!.Detail.Should().Contain("does not define");
        PluginCapabilityRules.Validate(Manifest("Registrar", "RegistrarWhois"))!.Detail.Should().Contain("does not define");
    }

    /// <summary>A class keeps the promise of a capability when it implements the interface the table names, or one derived from it.</summary>
    /// <param name="capability">The capability.</param>
    /// <param name="implementation">The type the plugin loaded as.</param>
    /// <param name="expected">Whether the promise is kept.</param>
    [Theory]
    [InlineData("storedCredentials", typeof(IStoredCredentialPlugin), true)]
    [InlineData("storedCredentials", typeof(IPaymentPlugin), false)]
    [InlineData("paymentReturn", typeof(IPaymentReturnPlugin), true)]
    [InlineData("registrarOperations", typeof(IRegistrarWhoisPlugin), true)]
    [InlineData("registrarWhois", typeof(IRegistrarOperationsPlugin), false)]
    [InlineData("provisioningPanelAccount", typeof(IProvisioningPanelAccountPlugin), true)]
    [InlineData("provisioningPanelAccount", typeof(IProvisioningPanelPlugin), false)]
    [InlineData("virtualServerConsole", typeof(IVirtualServerPlugin), true)]
    [InlineData("virtualServerSnapshots", typeof(IVirtualServerPlugin), false)]
    [InlineData("virtualServerSnapshots", typeof(IVirtualServerSnapshotPlugin), true)]
    [InlineData("teleport", typeof(IVirtualServerPlugin), false)]
    public void IsImplementedBy_FollowsTheInterfaceTheTableNames(string capability, Type implementation, bool expected)
    {
        PluginCapabilityRules.IsImplementedBy(capability, implementation).Should().Be(expected);
    }

    /// <summary>The first declared capability the class does not implement is the one reported, and a fully implemented list reports none.</summary>
    [Fact]
    public void FirstUnimplemented_NamesTheFirstBrokenPromise()
    {
        var manifest = Manifest("Registrar", "registrarOperations", "registrarWhois", "registrarDnssec");

        PluginCapabilityRules.FirstUnimplemented(manifest, typeof(IRegistrarWhoisPlugin)).Should().Be("registrarDnssec");
        PluginCapabilityRules.FirstUnimplemented(manifest, typeof(IRegistrarPlugin)).Should().Be("registrarOperations");
        PluginCapabilityRules.FirstUnimplemented(Manifest("Registrar", "registrarOperations"), typeof(IRegistrarWhoisPlugin)).Should().BeNull();
    }

    /// <summary>A plugin is a provisioning one when any declared capability is an operation on a provisioned server.</summary>
    /// <param name="type">The plugin type.</param>
    /// <param name="capabilities">The capabilities, comma separated.</param>
    /// <param name="expected">Whether the plugin declares a provisioning capability.</param>
    [Theory]
    [InlineData("Provisioning", "provisioningPanel", true)]
    [InlineData("Provisioning", "provisioningPanel,provisioningPanelAccount", true)]
    [InlineData("Provisioning", "virtualServer", true)]
    [InlineData("Provisioning", "virtualServerList", true)]
    [InlineData("Provisioning", "endpoints", false)]
    [InlineData("Registrar", "registrarOperations", false)]
    [InlineData("Payment", "storedCredentials,tax", false)]
    public void DeclaresProvisioning_IsTrueForAProvisioningCapabilityOnly(string type, string capabilities, bool expected)
    {
        PluginCapabilityRules.DeclaresProvisioning(Manifest(type, capabilities.Split(','))).Should().Be(expected);
    }
}
