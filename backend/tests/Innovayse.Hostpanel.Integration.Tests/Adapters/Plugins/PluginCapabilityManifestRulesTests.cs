using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Manifest;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The manifest rules of the <c>endpoints</c> capability (#818): which types may carry which capability, and that a
/// non-app plugin's <c>api</c> and <c>ui</c> blocks need the capability. No container or database.
/// </summary>
public sealed class PluginCapabilityManifestRulesTests
{
    /// <summary>An admin route, the smallest <c>api</c> block.</summary>
    private const string AdminRoute = ""","api":{"admin":[{"method":"get","path":"/stats"}]}""";

    /// <summary>Reads a version 2 manifest of a type with the given extra members.</summary>
    /// <param name="type">The plugin type, as the manifest spells it.</param>
    /// <param name="extra">Extra JSON members, each starting with a comma.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(string type, string extra)
    {
        var json = "{\"id\":\"caps-plugin\",\"name\":\"P\",\"version\":\"1.0.0\",\"author\":\"A\",\"description\":\"D\","
            + $"\"type\":\"{type}\",\"category\":\"C\",\"entryPoint\":\"P.Plugin\",\"sdkVersion\":\"2.1\",\"manifestVersion\":2{extra}}}";
        return PluginManifestReader.Deserialize(json)!;
    }

    /// <summary>The rule a manifest breaks, or null when it passes.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The broken rule, or <see langword="null"/>.</returns>
    private static PluginManifestRule? RuleOf(PluginManifest manifest)
    {
        return PluginManifestValidator.Validate(manifest)?.Rule;
    }

    /// <summary>A fulfilment plugin may declare routes, pages and slots once it declares the endpoints capability.</summary>
    [Fact]
    public void Validate_FulfilmentWithEndpointsCapability_Passes()
    {
        RuleOf(Manifest("Fulfilment", ""","capabilities":["endpoints"]""" + AdminRoute
            + ""","ui":{"slots":[{"id":"credits","slot":"client.dashboard.cards","view":"v.json","permission":"Any"}]}""")).Should().BeNull();
    }

    /// <summary>Every non-app type that declares an api route, a page, a menu or a slot without the capability is refused.</summary>
    /// <param name="type">The plugin type.</param>
    /// <param name="block">The api or ui block it declares.</param>
    [Theory]
    [InlineData("Fulfilment", AdminRoute)]
    [InlineData("Payment", AdminRoute)]
    [InlineData("Hosted", ""","ui":{"client":{"pages":[{"id":"p","path":"/p","title":"P","view":"v.json","permission":"Any"}]}}""")]
    [InlineData("Identity", ""","ui":{"admin":{"menus":[{"id":"m","label":"M","page":"p"}]}}""")]
    [InlineData("Fulfilment", ""","ui":{"slots":[{"id":"s","slot":"client.dashboard.cards","view":"v.json","permission":"Any"}]}""")]
    public void Validate_NonAppBlocksWithoutCapability_BreaksEndpointsNotDeclared(string type, string block)
    {
        RuleOf(Manifest(type, block)).Should().Be(PluginManifestRule.EndpointsNotDeclared);
    }

    /// <summary>An app plugin needs no declaration, and a non-app plugin with no api or ui block needs none either.</summary>
    [Fact]
    public void Validate_AppWithoutCapabilityOrPlainFulfilment_Passes()
    {
        RuleOf(Manifest("App", AdminRoute)).Should().BeNull();
        RuleOf(Manifest("Fulfilment", string.Empty)).Should().BeNull();
    }

    /// <summary>
    /// An unknown capability, a wrong-case one, one declared twice, and storedCredentials on anything but a payment plugin
    /// are refused.
    /// </summary>
    [Fact]
    public void Validate_CapabilityTheTypeMayNotCarry_BreaksCapabilityNotAllowed()
    {
        RuleOf(Manifest("Fulfilment", ""","capabilities":["teleport"]""")).Should().Be(PluginManifestRule.CapabilityNotAllowed);
        RuleOf(Manifest("App", ""","capabilities":["teleport"]""" + AdminRoute)).Should().Be(PluginManifestRule.CapabilityNotAllowed);
        RuleOf(Manifest("Fulfilment", ""","capabilities":["Endpoints"]""")).Should().Be(PluginManifestRule.CapabilityNotAllowed);
        RuleOf(Manifest("Fulfilment", ""","capabilities":["storedCredentials"]""")).Should().Be(PluginManifestRule.CapabilityNotAllowed);
        RuleOf(Manifest("Fulfilment", ""","capabilities":["endpoints","endpoints"]""" + AdminRoute))
            .Should().Be(PluginManifestRule.CapabilityNotAllowed);
        RuleOf(Manifest("Payment", ""","capabilities":["storedCredentials","endpoints"]""")).Should().BeNull();
        RuleOf(Manifest("App", ""","capabilities":["endpoints"]""")).Should().BeNull();
    }
}
