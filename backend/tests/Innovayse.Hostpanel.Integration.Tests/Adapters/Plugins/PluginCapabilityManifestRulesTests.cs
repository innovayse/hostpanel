using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The manifest rules of the <c>endpoints</c> capability (#818): which types may carry which capability, and that a
/// non-app plugin's <c>api</c> and <c>ui</c> blocks need the capability. No container or database.
/// </summary>
public sealed class PluginCapabilityManifestRulesTests
{
    /// <summary>An admin route, the smallest <c>api</c> block.</summary>
    private const string AdminRoute = ""","api":{"admin":[{"method":"get","path":"/stats"}]}""";

    /// <summary>A client route, which needs a permission in the client area.</summary>
    private const string ClientRoute = ""","api":{"client":[{"method":"get","path":"/ping","permission":"Any"}]}""";

    /// <summary>A menu entry in the client area.</summary>
    private const string ClientMenu = ""","ui":{"client":{"menus":[{"id":"m","label":"M","page":"p"}]}}""";

    /// <summary>A page in the client area.</summary>
    private const string ClientPage = ""","ui":{"client":{"pages":[{"id":"p","path":"/p","title":"P","view":"v.json","permission":"Any"}]}}""";

    /// <summary>A menu entry in the admin area.</summary>
    private const string AdminMenu = ""","ui":{"admin":{"menus":[{"id":"m","label":"M","page":"p"}]}}""";

    /// <summary>A page in the admin area, which takes no permission.</summary>
    private const string AdminPage = ""","ui":{"admin":{"pages":[{"id":"p","path":"/p","title":"P","view":"v.json"}]}}""";

    /// <summary>A widget in a client slot.</summary>
    private const string ClientSlot = ""","ui":{"slots":[{"id":"s","slot":"client.service.tabs","view":"v.json","permission":"Any"}]}""";

    /// <summary>A widget in an admin slot, which takes no permission.</summary>
    private const string AdminSlot = ""","ui":{"slots":[{"id":"s","slot":"admin.service.panels","view":"v.json"}]}""";

    /// <summary>The plugin types that serve only when their manifest declares the capability.</summary>
    private static readonly string[] NonAppTypes = ["Provisioning", "Payment", "Registrar", "Identity", "Fulfilment", "Hosted"];

    /// <summary>One block of each kind the capability opens.</summary>
    private static readonly string[] Blocks = [ClientRoute, AdminRoute, ClientMenu, ClientPage, AdminMenu, AdminPage, ClientSlot, AdminSlot];

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

    /// <summary>
    /// One row for every non-app plugin type crossed with every kind of block the capability opens: a client route, an admin
    /// route, a client menu and page, an admin menu and page, and a slot widget of each area.
    /// </summary>
    /// <returns>The plugin type and the api or ui block it declares, one row per pair.</returns>
    public static TheoryData<string, string> EveryTypeAndBlock()
    {
        var rows = new TheoryData<string, string>();
        foreach (var type in NonAppTypes)
        {
            foreach (var block in Blocks)
            {
                rows.Add(type, block);
            }
        }

        return rows;
    }

    /// <summary>Every non-app type that declares an api route, a page, a menu or a slot without the capability is refused.</summary>
    /// <param name="type">The plugin type.</param>
    /// <param name="block">The api or ui block it declares.</param>
    [Theory]
    [MemberData(nameof(EveryTypeAndBlock))]
    public void Validate_NonAppBlocksWithoutCapability_BreaksEndpointsNotDeclared(string type, string block)
    {
        RuleOf(Manifest(type, block)).Should().Be(PluginManifestRule.EndpointsNotDeclared);
    }

    /// <summary>Every non-app type that declares the endpoints capability may declare any of those blocks.</summary>
    /// <param name="type">The plugin type.</param>
    /// <param name="block">The api or ui block it declares.</param>
    [Theory]
    [MemberData(nameof(EveryTypeAndBlock))]
    public void Validate_NonAppBlocksWithCapability_Passes(string type, string block)
    {
        RuleOf(Manifest(type, ""","capabilities":["endpoints"]""" + block)).Should().BeNull();
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

    /// <summary>The paymentReturn capability fits a payment plugin only.</summary>
    [Fact]
    public void Validate_PaymentReturn_IsAllowedOnAPaymentPluginOnly()
    {
        RuleOf(Manifest("Payment", ""","capabilities":["paymentReturn"]""")).Should().BeNull();
        RuleOf(Manifest("Payment", ""","capabilities":["storedCredentials","paymentReturn"]""")).Should().BeNull();
        RuleOf(Manifest("Fulfilment", ""","capabilities":["paymentReturn"]""")).Should().Be(PluginManifestRule.CapabilityNotAllowed);
        RuleOf(Manifest("App", ""","capabilities":["paymentReturn"]""")).Should().Be(PluginManifestRule.CapabilityNotAllowed);
        RuleOf(Manifest("Payment", ""","capabilities":["paymentReturn","paymentReturn"]""")).Should().Be(PluginManifestRule.CapabilityNotAllowed);
    }
}
