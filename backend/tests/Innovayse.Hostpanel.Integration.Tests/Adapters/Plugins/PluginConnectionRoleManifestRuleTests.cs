using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The manifest rules for a registrar plugin's <c>connectionRole</c> settings. The checks are the plugin packaging package's
/// (the host reuses them, it does not copy them); these tests prove the host runs them, reports each as its own rule and gives
/// each rule its own error code. No container or database.
/// </summary>
public sealed class PluginConnectionRoleManifestRuleTests
{
    /// <summary>The account name setting, a plain text one.</summary>
    private const string Account = """{"key":"account","label":"Account","type":"text","connectionRole":"accountName"}""";

    /// <summary>The API key setting, a secret one.</summary>
    private const string Key = """{"key":"key","label":"Key","type":"secret","connectionRole":"apiKey"}""";

    /// <summary>Reads a version 2 manifest of a type with the given settings.</summary>
    /// <param name="type">The plugin type, as the manifest spells it.</param>
    /// <param name="settings">The settings, each a JSON object, comma separated.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string type, string settings)
    {
        var json = "{\"id\":\"roles-plugin\",\"name\":\"P\",\"version\":\"1.0.0\",\"author\":\"A\",\"description\":\"D\","
            + $"\"type\":\"{type}\",\"category\":\"C\",\"entryPoint\":\"P.Plugin\",\"sdkVersion\":\"2.1\",\"manifestVersion\":2,"
            + $"\"settings\":[{settings}]}}";
        return PluginManifestReader.Deserialize(json)!;
    }

    /// <summary>A registrar plugin with both required roles, and optional ones on settings of the right type, passes.</summary>
    [Fact]
    public void Validate_RegistrarWithTheRequiredRoles_Passes()
    {
        PluginManifestValidator.Validate(Manifest("Registrar", Account + "," + Key)).Should().BeNull();
        PluginManifestValidator.Validate(Manifest(
            "Registrar",
            Account + "," + Key + ","
            + """{"key":"url","label":"Url","type":"url","connectionRole":"apiUrl"},"""
            + """{"key":"test","label":"Test","type":"boolean","connectionRole":"testMode"},"""
            + """{"key":"ip","label":"Ip","type":"text","connectionRole":"clientIp"}"""))
            .Should().BeNull();
    }

    /// <summary>A registrar plugin that declares no role at all is untouched, so a plugin built before roles existed still loads.</summary>
    [Fact]
    public void Validate_RegistrarWithoutRoles_Passes()
    {
        PluginManifestValidator.Validate(Manifest("Registrar", """{"key":"account","label":"Account","type":"text"}""")).Should().BeNull();
    }

    /// <summary>Each way the roles are inconsistent is reported as the host rule of the same name.</summary>
    /// <param name="type">The plugin type.</param>
    /// <param name="settings">The settings, comma separated.</param>
    /// <param name="expected">The rule that is broken.</param>
    [Theory]
    [InlineData("Payment", """{"key":"account","label":"Account","type":"text","connectionRole":"accountName"}""", PluginManifestRule.ConnectionRoleNotRegistrar)]
    [InlineData("Provisioning", """{"key":"key","label":"Key","type":"secret","connectionRole":"apiKey"}""", PluginManifestRule.ConnectionRoleNotRegistrar)]
    [InlineData("Registrar", """{"key":"a","label":"A","type":"text","connectionRole":"accountName"},{"key":"b","label":"B","type":"text","connectionRole":"accountName"}""", PluginManifestRule.ConnectionRoleDuplicate)]
    [InlineData("Registrar", """{"key":"a","label":"A","type":"text","connectionRole":"accountName"},{"key":"k","label":"K","type":"text","connectionRole":"apiKey"}""", PluginManifestRule.ConnectionRoleTypeMismatch)]
    [InlineData("Registrar", """{"key":"a","label":"A","type":"text","connectionRole":"accountName"},{"key":"k","label":"K","type":"secret","connectionRole":"apiKey"},{"key":"u","label":"U","type":"text","connectionRole":"apiUrl"}""", PluginManifestRule.ConnectionRoleTypeMismatch)]
    [InlineData("Registrar", """{"key":"a","label":"A","type":"text","connectionRole":"accountName"}""", PluginManifestRule.ConnectionRolesIncomplete)]
    [InlineData("Registrar", """{"key":"k","label":"K","type":"secret","connectionRole":"apiKey"}""", PluginManifestRule.ConnectionRolesIncomplete)]
    public void Validate_InconsistentRoles_AreReportedAsTheHostRule(string type, string settings, PluginManifestRule expected)
    {
        PluginManifestValidator.Validate(Manifest(type, settings))!.Rule.Should().Be(expected);
    }

    /// <summary>Each of the four rules maps to an error code of its own, so the install answer names the cause.</summary>
    [Fact]
    public void EveryRoleRule_HasItsOwnErrorCode()
    {
        PluginPackageErrors.CodeFor(PluginManifestRule.ConnectionRoleNotRegistrar).Should().Be(HostpanelErrorCodes.PLUGIN_MANIFEST_CONNECTION_ROLE_NOT_REGISTRAR);
        PluginPackageErrors.CodeFor(PluginManifestRule.ConnectionRoleDuplicate).Should().Be(HostpanelErrorCodes.PLUGIN_MANIFEST_CONNECTION_ROLE_DUPLICATE);
        PluginPackageErrors.CodeFor(PluginManifestRule.ConnectionRoleTypeMismatch).Should().Be(HostpanelErrorCodes.PLUGIN_MANIFEST_CONNECTION_ROLE_TYPE_MISMATCH);
        PluginPackageErrors.CodeFor(PluginManifestRule.ConnectionRolesIncomplete).Should().Be(HostpanelErrorCodes.PLUGIN_MANIFEST_CONNECTION_ROLES_INCOMPLETE);
    }
}
