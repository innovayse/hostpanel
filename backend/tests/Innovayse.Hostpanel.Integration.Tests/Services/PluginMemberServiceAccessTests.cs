using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <c>/api/me/services</c> for a member an identity plugin vouches for: they act for the client linked to the
/// plugin account and never for the client record they registered with, a member who may purchase can cancel
/// the account's service, a view-only member cannot, and a member whose account has no client yet is told so.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginMemberServiceAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The member lists and reads the linked client's service, and the member's own client's service stays out of reach.</summary>
    [Fact]
    public async Task MemberActsForThePluginAccountNotForTheirOwnClientRecordAsync()
    {
        var (member, serviceId) = await MemberWithServiceAsync("plugin-service-scope", canPurchase: false);
        using var memberLifetime = member;
        var ownServiceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, member.OwnClientId);

        var list = await member.Client.GetAsync("/api/me/services");
        var status = await member.Client.GetAsync($"/api/me/services/{serviceId}/cancellation-status");
        var own = await member.Client.GetAsync($"/api/me/services/{ownServiceId}/cancellation-status");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await ClientsTestSeeds.IdsOfAsync(list);
        ids.Should().Contain(serviceId);
        ids.Should().NotContain(ownServiceId);
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        own.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(own, HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>A member who may purchase cancels the account's service.</summary>
    [Fact]
    public async Task MemberWhoMayPurchase_CancelsTheLinkedClientsServiceAsync()
    {
        var (member, serviceId) = await MemberWithServiceAsync("plugin-service-cancel", canPurchase: true);
        using var memberLifetime = member;

        var response = await member.Client.PostAsJsonAsync(
            $"/api/me/services/{serviceId}/cancel", new { type = nameof(CancellationType.Immediate), reason = "Plugin member" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A view-only member is refused the cancellation with the permission code.</summary>
    [Fact]
    public async Task MemberWhoMayOnlyView_IsRefusedTheCancellationAsync()
    {
        var (member, serviceId) = await MemberWithServiceAsync("plugin-service-viewer", canPurchase: false);
        using var memberLifetime = member;

        var response = await member.Client.PostAsJsonAsync(
            $"/api/me/services/{serviceId}/cancel", new { type = nameof(CancellationType.Immediate), reason = "Plugin member" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>A member of an account no client is linked to yet gets no profile rather than another client's services.</summary>
    [Fact]
    public async Task MemberOfAnAccountWithNoClient_GetsNoProfileAsync()
    {
        using var member = await PluginMemberSeeds.SignInMemberAsync(factory, "plugin-service-unlinked", $"acct-{Guid.NewGuid():N}");

        var list = await member.Client.GetAsync("/api/me/services");

        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(list, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }

    /// <summary>Seeds a client with a service, linked to a plugin account, and a member of that account.</summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <param name="canPurchase">Whether the plugin lets the member purchase.</param>
    /// <returns>The member and the service id on the linked client.</returns>
    private async Task<(PluginMember Member, int ServiceId)> MemberWithServiceAsync(string label, bool canPurchase)
    {
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, seeded.ClientId);
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, seeded.ClientId);
        var member = await PluginMemberSeeds.SignInMemberAsync(factory, label, accountId, canPurchase);

        return (member, serviceId);
    }
}
