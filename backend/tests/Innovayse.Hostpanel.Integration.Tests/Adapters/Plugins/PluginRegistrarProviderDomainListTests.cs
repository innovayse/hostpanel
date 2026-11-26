using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRegistrarProvider.ListDomainsAsync"/>: the registrar plugin's domain list reaches the plugin with the
/// connection and the cursor, comes back as the host's page, and is reached only when the plugin declares the capability.
/// </summary>
public sealed class PluginRegistrarProviderDomainListTests
{
    /// <summary>The connection every test hands the provider.</summary>
    private static readonly RegistrarConnection Connection = new("account", "key", "https://api.example.test", "10.0.0.1", true);

    /// <summary>The expiry the first listed domain reports.</summary>
    private static readonly DateTimeOffset Expiry = new(2031, 5, 6, 7, 8, 9, TimeSpan.Zero);

    /// <summary>Builds the provider over a scripted plugin, declaring the given capabilities in its manifest.</summary>
    /// <param name="plugin">The scripted plugin.</param>
    /// <param name="capabilities">The capabilities the manifest declares.</param>
    /// <returns>The provider under test.</returns>
    private static PluginRegistrarProvider Provider(ScriptedRegistrarPlugin plugin, params string[] capabilities)
    {
        var manifest = PluginCapabilitySeeds.Plugin("scripted", PluginType.Registrar, plugin.GetType(), capabilities).Manifest;

        return new PluginRegistrarProvider(manifest, plugin, Connection, NullLogger<PluginRegistrarProvider>.Instance);
    }

    /// <summary>A page reaches the plugin with the connection and the cursor and comes back with each domain's name, reference and expiry and the next cursor.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListDomains_PassesTheConnectionAndCursorAndMapsThePageAsync()
    {
        var plugin = new FullRegistrarPlugin(new ConfigurationBuilder().Build())
        {
            DomainPages = _ => ValueResult<RegistrarDomainPage, RegistrarRefusalReason>.Done(new RegistrarDomainPage(
                [
                    new RegistrarListedDomain(new RegistrarDomain("one.example", "REF-1"), Expiry),
                    new RegistrarListedDomain(new RegistrarDomain("two.example", "REF-2"), null),
                ],
                "next-page")),
        };
        var provider = Provider(plugin, PluginCapabilities.RegistrarOperations, PluginCapabilities.RegistrarDomainList);

        var result = await provider.ListDomainsAsync("this-page", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Domains.Should().Equal(
            new RegistrarHeldDomain("one.example", "REF-1", Expiry),
            new RegistrarHeldDomain("two.example", "REF-2", null));
        result.Value.NextCursor.Should().Be("next-page");
        var context = plugin.Received.OfType<RegistrarDomainListContext>().Single();
        context.Connection.Should().Be(Connection);
        context.Cursor.Should().Be("this-page");
    }

    /// <summary>A plugin that does not declare the domain list answers <c>REGISTRAR_OPERATION_UNSUPPORTED</c> and is never asked, even when its class implements the interface.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListDomains_NotDeclared_AnswersUnsupportedWithoutCallingThePluginAsync()
    {
        var plugin = new FullRegistrarPlugin(new ConfigurationBuilder().Build());
        var provider = Provider(plugin, PluginCapabilities.RegistrarOperations);

        var result = await provider.ListDomainsAsync(null, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);
        plugin.Received.Should().BeEmpty();
    }

    /// <summary>A refusal of the plugin is the mapped <c>REGISTRAR_*</c> code, never the plugin's own text.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListDomains_ARefusal_AnswersTheMappedCodeAsync()
    {
        var plugin = new FullRegistrarPlugin(new ConfigurationBuilder().Build())
        {
            DomainPages = _ => ValueResult<RegistrarDomainPage, RegistrarRefusalReason>.Refused(RegistrarRefusalReason.AccessDenied),
        };
        var provider = Provider(plugin, PluginCapabilities.RegistrarOperations, PluginCapabilities.RegistrarDomainList);

        var result = await provider.ListDomainsAsync(null, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ACCESS_DENIED);
    }
}
