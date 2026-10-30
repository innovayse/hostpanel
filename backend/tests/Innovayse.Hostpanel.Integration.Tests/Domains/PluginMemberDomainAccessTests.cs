using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <c>/api/me/domains</c> for a member an identity plugin vouches for: they read the domains of the client
/// linked to the plugin account and never those of the client record they registered with, and a plugin
/// never grants the right to change domain settings, however much it lets the member purchase.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginMemberDomainAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The member lists and reads the linked client's domain, and the member's own client's domain stays out of reach.</summary>
    [Fact]
    public async Task MemberActsForThePluginAccountNotForTheirOwnClientRecordAsync()
    {
        var (member, domainId, admin) = await MemberWithDomainAsync("plugin-domain-scope", canPurchase: false);
        using var memberLifetime = member;
        var ownDomainId = await DomainsTestSeeds.RegisterDomainAsync(admin, member.OwnClientId);

        var list = await member.Client.GetAsync("/api/me/domains");
        var read = await member.Client.GetAsync($"/api/me/domains/{domainId}");
        var own = await member.Client.GetAsync($"/api/me/domains/{ownDomainId}");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await ClientsTestSeeds.IdsOfAsync(list);
        ids.Should().Contain(domainId);
        ids.Should().NotContain(ownDomainId);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        own.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(own, HostpanelErrorCodes.DOMAIN_NOT_FOUND);
    }

    /// <summary>Changing a domain setting needs a flag no plugin grants, so even a purchasing member is refused.</summary>
    [Fact]
    public async Task Member_IsRefusedDomainSettingChangesAsync()
    {
        var (member, domainId, _) = await MemberWithDomainAsync("plugin-domain-settings", canPurchase: true);
        using var memberLifetime = member;

        var response = await member.Client.PutAsJsonAsync($"/api/me/domains/{domainId}/auto-renew", new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>A member of an account no client is linked to yet gets no profile rather than another client's domains.</summary>
    [Fact]
    public async Task MemberOfAnAccountWithNoClient_GetsNoProfileAsync()
    {
        using var member = await PluginMemberSeeds.SignInMemberAsync(factory, "plugin-domain-unlinked", $"acct-{Guid.NewGuid():N}");

        var list = await member.Client.GetAsync("/api/me/domains");

        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(list, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }

    /// <summary>Seeds a client with a domain, linked to a plugin account, and a member of that account.</summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <param name="canPurchase">Whether the plugin lets the member purchase.</param>
    /// <returns>The member, the domain id on the linked client and the admin caller that seeded it.</returns>
    private async Task<(PluginMember Member, int DomainId, HttpClient Admin)> MemberWithDomainAsync(string label, bool canPurchase)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, seeded.ClientId);
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, seeded.ClientId);
        var member = await PluginMemberSeeds.SignInMemberAsync(factory, label, accountId, canPurchase);

        return (member, domainId, admin);
    }
}
