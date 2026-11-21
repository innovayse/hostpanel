using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The manifest rule for <c>serverModules</c>: a plugin that declares a provisioning capability lists at least one well
/// formed, unique module id, and a plugin that declares none lists none. No container or database.
/// </summary>
public sealed class PluginServerModulesManifestRuleTests
{
    /// <summary>Reads a version 2 manifest of a type with the given extra members.</summary>
    /// <param name="type">The plugin type, as the manifest spells it.</param>
    /// <param name="extra">Extra JSON members, each starting with a comma.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string type, string extra)
    {
        var json = "{\"id\":\"modules-plugin\",\"name\":\"P\",\"version\":\"1.0.0\",\"author\":\"A\",\"description\":\"D\","
            + $"\"type\":\"{type}\",\"category\":\"C\",\"entryPoint\":\"P.Plugin\",\"sdkVersion\":\"2.1\",\"manifestVersion\":2{extra}}}";
        return PluginManifestReader.Deserialize(json)!;
    }

    /// <summary>The violation a manifest breaks, or null when it passes.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The violation, or <see langword="null"/>.</returns>
    private static PluginManifestViolation? ViolationOf(PluginManifest manifest)
    {
        return PluginManifestValidator.Validate(manifest);
    }

    /// <summary>A panel or virtual server plugin that lists well formed module ids passes.</summary>
    /// <param name="capabilities">The capabilities, as the manifest lists them.</param>
    [Theory]
    [InlineData("\"provisioningPanel\"")]
    [InlineData("\"virtualServer\"")]
    [InlineData("\"virtualServerList\"")]
    [InlineData("\"provisioningPanel\",\"provisioningPanelLogin\"")]
    public void Validate_ProvisioningCapabilityWithModules_Passes(string capabilities)
    {
        ViolationOf(Manifest("Provisioning", $",\"capabilities\":[{capabilities}],\"serverModules\":[\"example-panel\",\"Panel_2.x\"]"))
            .Should().BeNull();
    }

    /// <summary>A provisioning capability with no module list, an empty one or a null one is refused, because no server would ever be sent to it.</summary>
    /// <param name="members">The manifest members after the capabilities.</param>
    [Theory]
    [InlineData("")]
    [InlineData(",\"serverModules\":[]")]
    [InlineData(",\"serverModules\":null")]
    public void Validate_ProvisioningCapabilityWithoutModules_IsRefused(string members)
    {
        var violation = ViolationOf(Manifest("Provisioning", ",\"capabilities\":[\"provisioningPanel\"]" + members));

        violation.Should().NotBeNull();
        violation!.Rule.Should().Be(PluginManifestRule.ServerModulesInvalid);
        violation.Detail.Should().Contain("ListEmpty");
    }

    /// <summary>An entry the SDK refuses (empty, too long, a bad character or a repeat that differs only by case) is refused with its position.</summary>
    /// <param name="modules">The JSON list.</param>
    /// <param name="rule">The SDK rule named in the detail.</param>
    [Theory]
    [InlineData("[\"ok\",\"\"]", "IdEmpty")]
    [InlineData("[\"ok\",\"   \"]", "IdEmpty")]
    [InlineData("[\"has space\"]", "IdBadCharacters")]
    [InlineData("[\"slash/panel\"]", "IdBadCharacters")]
    [InlineData("[\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxy\"]", "IdTooLong")]
    [InlineData("[\"Panel\",\"panel\"]", "IdDuplicate")]
    public void Validate_BadModuleEntry_IsRefusedWithTheSdkRule(string modules, string rule)
    {
        var violation = ViolationOf(Manifest("Provisioning", $",\"capabilities\":[\"virtualServer\"],\"serverModules\":{modules}"));

        violation.Should().NotBeNull();
        violation!.Rule.Should().Be(PluginManifestRule.ServerModulesInvalid);
        violation.Detail.Should().Contain(rule).And.Contain("entry ");
    }

    /// <summary>A plugin that declares no provisioning capability may not list module ids, whatever its type.</summary>
    /// <param name="type">The plugin type.</param>
    /// <param name="capabilities">The capabilities, as the manifest lists them.</param>
    [Theory]
    [InlineData("Provisioning", "")]
    [InlineData("Provisioning", "\"endpoints\"")]
    [InlineData("Registrar", "\"registrarOperations\"")]
    [InlineData("Payment", "\"storedCredentials\"")]
    public void Validate_ModulesWithoutAProvisioningCapability_AreRefused(string type, string capabilities)
    {
        var violation = ViolationOf(Manifest(type, $",\"capabilities\":[{capabilities}],\"serverModules\":[\"example-panel\"]"));

        violation.Should().NotBeNull();
        violation!.Rule.Should().Be(PluginManifestRule.ServerModulesInvalid);
    }

    /// <summary>A plugin with no provisioning capability and no module list is untouched by the rule.</summary>
    [Fact]
    public void Validate_NoCapabilityAndNoModules_Passes()
    {
        ViolationOf(Manifest("Provisioning", string.Empty)).Should().BeNull();
        ViolationOf(Manifest("Registrar", ",\"capabilities\":[\"registrarOperations\"],\"serverModules\":[]")).Should().BeNull();
    }

    /// <summary>The rule has its own error code, so an install is refused with a sentence that names the cause.</summary>
    [Fact]
    public void TheRule_HasItsOwnErrorCode()
    {
        PluginPackageErrors.CodeFor(PluginManifestRule.ServerModulesInvalid)
            .Should().Be(HostpanelErrorCodes.PLUGIN_MANIFEST_SERVER_MODULES_INVALID);
    }
}
