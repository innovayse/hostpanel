using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// <c>GET /api/me/emails</c> for a member an identity plugin vouches for: they read what was sent to the
/// client linked to the plugin account and never what was sent to the client record they registered with.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginMemberEmailAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The member sees the email sent to the linked client and not the one sent to their own client.</summary>
    [Fact]
    public async Task MemberActsForThePluginAccountNotForTheirOwnClientRecordAsync()
    {
        var holder = await ClientsTestSeeds.RegisterClientAsync(factory, "plugin-email-holder");
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, holder.ClientId);
        using var member = await PluginMemberSeeds.SignInMemberAsync(factory, "plugin-email-member", accountId);
        var accountSubject = $"Plugin account {Guid.NewGuid():N}";
        var ownSubject = $"Plugin member own {Guid.NewGuid():N}";
        await NotificationsTestSeeds.SeedLogAsync(factory, holder.Email, accountSubject);
        await NotificationsTestSeeds.SeedLogAsync(factory, member.OwnEmail, ownSubject);

        var list = await member.Client.GetAsync("/api/me/emails");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await list.Content.ReadAsStringAsync();
        body.Should().Contain(accountSubject);
        body.Should().NotContain(ownSubject);
    }

    /// <summary>A member of an account no client is linked to yet gets no profile rather than another client's emails.</summary>
    [Fact]
    public async Task MemberOfAnAccountWithNoClient_GetsNoProfileAsync()
    {
        using var member = await PluginMemberSeeds.SignInMemberAsync(factory, "plugin-email-unlinked", $"acct-{Guid.NewGuid():N}");

        var list = await member.Client.GetAsync("/api/me/emails");

        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(list, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }
}
