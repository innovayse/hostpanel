using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A host whose registry holds three switched-on plugins of one type that declare the same routes, pages, menus and slots,
/// so a test states each assertion once for every plugin type (#1357): one that declares the <c>endpoints</c> capability and
/// implements it, one that does not declare it, and one that declares it on a class that does not implement it. The host
/// must serve the first and answer for the other two exactly as it answers for a plugin that was never loaded.
/// </summary>
public sealed class ServingPluginHost : IDisposable
{
    /// <summary>Initializes the holder.</summary>
    /// <param name="host">The derived host.</param>
    /// <param name="servingId">The id of the plugin that declares <c>endpoints</c> and implements it.</param>
    /// <param name="plainId">The id of the plugin that does not declare <c>endpoints</c>.</param>
    /// <param name="brokenId">The id of the plugin that declares <c>endpoints</c> without implementing it.</param>
    private ServingPluginHost(WebApplicationFactory<Program> host, string servingId, string plainId, string brokenId)
    {
        Host = host;
        ServingId = servingId;
        PlainId = plainId;
        BrokenId = brokenId;
    }

    /// <summary>Gets the host, derived from the fixture's.</summary>
    public WebApplicationFactory<Program> Host { get; }

    /// <summary>Gets the id of the plugin that declares <c>endpoints</c> and implements it.</summary>
    public string ServingId { get; }

    /// <summary>Gets the id of the plugin that does not declare <c>endpoints</c>.</summary>
    public string PlainId { get; }

    /// <summary>Gets the id of the plugin that declares <c>endpoints</c> without implementing it.</summary>
    public string BrokenId { get; }

    /// <summary>Gets the ids of the two plugins the host must not serve.</summary>
    public IReadOnlyList<string> RefusedIds => [PlainId, BrokenId];

    /// <summary>Builds the host with the three plugins of a type registered and switched on.</summary>
    /// <param name="fixture">The host and the database the derived host starts from.</param>
    /// <param name="type">The plugin type all three plugins declare.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    public static async Task<ServingPluginHost> CreateAsync(AppPluginFixture fixture, PluginType type)
    {
        var prefix = type.ToString().ToLowerInvariant();
        var serving = PluginActivationTestSeeds.NewId($"{prefix}-serving");
        var plain = PluginActivationTestSeeds.NewId($"{prefix}-plain");
        var broken = PluginActivationTestSeeds.NewId($"{prefix}-broken");
        var implementation = PluginCapabilitySeeds.ImplementationOf(type);

        var host = await PluginSlotSeeds.HostAsync(
            fixture,
            [serving, plain, broken],
            PluginCapabilitySeeds.Serving(serving, type, implementation, PluginCapabilities.Endpoints),
            PluginCapabilitySeeds.Serving(plain, type, implementation),
            PluginCapabilitySeeds.Serving(broken, type, PluginCapabilitySeeds.NotImplementing, PluginCapabilities.Endpoints));

        return new ServingPluginHost(host, serving, plain, broken);
    }

    /// <summary>Releases the derived host.</summary>
    public void Dispose()
    {
        Host.Dispose();
    }
}
