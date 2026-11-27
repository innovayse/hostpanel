using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRegistrarProvider"/> hands the registry's extra values to the plugin, unchanged and under the keys the manifest
/// declared, in the one context member the SDK has for them on a registration, a transfer and a contact change; a plugin that is
/// given none receives the empty set the SDK promises, never <see langword="null"/>.
/// </summary>
public sealed class PluginRegistrarProviderDomainFieldsTests
{
    /// <summary>The connection every test hands the provider.</summary>
    private static readonly RegistrarConnection Connection = new("account", "key", "https://api.example.test", "10.0.0.1", true);

    /// <summary>The domain every test addresses.</summary>
    private static readonly DomainRef Domain = new("example.test", "REF-EXAMPLE");

    /// <summary>The registrant contact every order carries: the stub registrar's own, so no address or country is written here.</summary>
    private static readonly DomainContact Contact = StubRegistrarProvider.StubContact;

    /// <summary>The extra values every test hands the provider.</summary>
    private static readonly IReadOnlyDictionary<string, string> Attributes =
        new Dictionary<string, string> { ["holder-number"] = "AB1234", ["category"] = "a" };

    /// <summary>Builds the provider over a full scripted plugin.</summary>
    /// <returns>The plugin and the provider over it.</returns>
    private static (FullRegistrarPlugin Plugin, PluginRegistrarProvider Provider) Full()
    {
        var plugin = new FullRegistrarPlugin(new ConfigurationBuilder().Build());
        var manifest = PluginCapabilitySeeds.Plugin("scripted", PluginType.Registrar, plugin.GetType(), PluginCapabilities.RegistrarOperations).Manifest;

        return (plugin, new PluginRegistrarProvider(manifest, plugin, Connection, NullLogger<PluginRegistrarProvider>.Instance));
    }

    /// <summary>A registration reaches the plugin with the values it was ordered with.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Register_PassesTheExtendedAttributesAsync()
    {
        var (plugin, provider) = Full();

        await provider.RegisterAsync(
            new DomainRegistrationOrder("example.test", 1, false, false, null, null, Contact, Attributes), CancellationToken.None);

        plugin.Received.OfType<RegistrarRegisterContext>().Single().ExtendedAttributes.Should().BeEquivalentTo(Attributes);
    }

    /// <summary>A transfer reaches the plugin with the values it was ordered with.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Transfer_PassesTheExtendedAttributesAsync()
    {
        var (plugin, provider) = Full();

        await provider.TransferAsync(
            new DomainTransferOrder("example.test", "auth-code-1", false, Contact, Attributes), CancellationToken.None);

        plugin.Received.OfType<RegistrarTransferContext>().Single().ExtendedAttributes.Should().BeEquivalentTo(Attributes);
    }

    /// <summary>A contact change reaches the plugin with the values now in force.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ModifyContact_PassesTheExtendedAttributesAsync()
    {
        var (plugin, provider) = Full();

        await provider.ModifyContactDetailsAsync(new DomainContactChange(Domain, Contact, Attributes), CancellationToken.None);

        plugin.Received.OfType<RegistrarContactContext>().Single().ExtendedAttributes.Should().BeEquivalentTo(Attributes);
    }

    /// <summary>An order with no values gives the plugin the empty set, on every operation that carries them.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task NoValues_GiveThePluginTheEmptySetAsync()
    {
        var (plugin, provider) = Full();

        await provider.RegisterAsync(new DomainRegistrationOrder("example.test", 1, false, false, null, null, Contact), CancellationToken.None);
        await provider.TransferAsync(new DomainTransferOrder("example.test", "auth-code-1", false, Contact), CancellationToken.None);
        await provider.ModifyContactDetailsAsync(new DomainContactChange(Domain, Contact), CancellationToken.None);

        plugin.Received.OfType<RegistrarRegisterContext>().Single().ExtendedAttributes.Should().BeEmpty();
        plugin.Received.OfType<RegistrarTransferContext>().Single().ExtendedAttributes.Should().BeEmpty();
        plugin.Received.OfType<RegistrarContactContext>().Single().ExtendedAttributes.Should().BeEmpty();
    }

    /// <summary>The values never reach a log line: a record that carries them prints them masked.</summary>
    [Fact]
    public void Orders_PrintTheirExtendedAttributesMasked()
    {
        var register = new DomainRegistrationOrder("example.test", 1, false, false, null, null, Contact, Attributes).ToString();
        var transfer = new DomainTransferOrder("example.test", "auth-code-1", false, Contact, Attributes).ToString();
        var change = new DomainContactChange(Domain, Contact, Attributes).ToString();

        foreach (var printed in new[] { register, transfer, change })
        {
            printed.Should().NotContain("AB1234");
        }
    }
}
