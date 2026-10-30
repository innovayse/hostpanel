using System.Net;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <c>Policies.Client</c> for users who hold no stored client role: an enabled identity plugin vouches for a
/// member and the portal opens with the rights the plugin gives, while without a plugin, or with a plugin that
/// refuses them, the same user is still refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientPortalPolicyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A roleless member who may purchase reads their invoices without ever having ordered, on the client linked to their plugin account.</summary>
    [Fact]
    public async Task RolelessMemberWhoMayPurchase_ReadsInvoicesWithoutAnOrderAsync()
    {
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId);
        using var member = await PluginMemberSeeds.SignInRolelessMemberAsync(factory, "portal-buyer", accountId, canPurchase: true);

        var response = await member.Client.GetAsync("/api/me/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A roleless member who may only view reads their services without ever having ordered, on the client linked to their plugin account.</summary>
    [Fact]
    public async Task RolelessViewOnlyMember_ReadsServicesWithoutAnOrderAsync()
    {
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId);
        using var member = await PluginMemberSeeds.SignInRolelessMemberAsync(factory, "portal-viewer", accountId, canPurchase: false);

        var response = await member.Client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>With no identity plugin a user without the client role is refused every portal route.</summary>
    [Fact]
    public async Task RolelessUserWithoutAPlugin_IsRefusedAsync()
    {
        using var user = await PluginMemberSeeds.SignInRolelessUserAsync(factory, "portal-noplugin", plugin: null);

        var response = await user.Client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A roleless user the enabled plugin refuses is refused the portal too.</summary>
    [Fact]
    public async Task RolelessUserThePluginRefuses_IsRefusedAsync()
    {
        using var user = await PluginMemberSeeds.SignInRolelessUserAsync(factory, "portal-refused", new StubClientResolver());

        var response = await user.Client.GetAsync("/api/me/services");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
