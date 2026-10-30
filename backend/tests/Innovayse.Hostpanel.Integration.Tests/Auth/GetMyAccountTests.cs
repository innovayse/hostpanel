using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary><c>GET /api/auth/me</c>: the caller's email, local roles, client id and external account link.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyAccountTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A signed-in caller reads their own email, roles and verification state.</summary>
    [Fact]
    public async Task GetMyAccountReturnsTheCallersAccount()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("roles").ValueKind.Should().Be(JsonValueKind.Array);
        body.TryGetProperty("emailVerified", out _).Should().BeTrue();
    }

    /// <summary>Reads <c>/api/auth/me</c> as the given caller.</summary>
    /// <param name="client">The signed-in caller.</param>
    /// <returns>The answered body.</returns>
    private static async Task<JsonElement> ReadMeAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>An owner whose client is not linked to another system gets the client id and a null link.</summary>
    [Fact]
    public async Task OwnerWithoutALink_GetsTheClientIdAndNoExternalAccount()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, "me-nolink");

        var body = await ReadMeAsync(owner.Client);

        body.GetProperty("clientId").GetInt32().Should().Be(owner.ClientId);
        body.GetProperty("externalAccount").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("clientPermissions").GetInt32().Should().Be((int)ClientPermission.All);
    }

    /// <summary>An owner whose client is linked to another system gets the link as provider and id.</summary>
    [Fact]
    public async Task OwnerWithALink_GetsTheClientIdAndTheExternalAccount()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(factory, "me-link");
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, owner.ClientId);

        var body = await ReadMeAsync(owner.Client);

        body.GetProperty("clientId").GetInt32().Should().Be(owner.ClientId);
        var link = body.GetProperty("externalAccount");
        link.GetProperty("provider").GetString().Should().Be(PluginMemberSeeds.Provider);
        link.GetProperty("id").GetString().Should().Be(accountId);
    }

    /// <summary>A sub-user gets the id and the link of the client they are linked to, with the permissions of the link.</summary>
    [Fact]
    public async Task SubUser_GetsTheLinkedClientIdAndItsExternalAccount()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, clientId);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "me-sub", clientId, ClientPermission.ViewEmails);

        var body = await ReadMeAsync(user);

        body.GetProperty("clientId").GetInt32().Should().Be(clientId);
        body.GetProperty("externalAccount").GetProperty("id").GetString().Should().Be(accountId);
        body.GetProperty("clientPermissions").GetInt32().Should().Be((int)ClientPermission.ViewEmails);
    }

    /// <summary>A plugin member whose account has no client yet gets the account the plugin named and no client id.</summary>
    [Fact]
    public async Task PluginMemberWithoutAClient_GetsTheNamedAccountAndNoClientId()
    {
        var accountId = $"acct-{Guid.NewGuid():N}";
        using var member = await PluginMemberSeeds.SignInMemberAsync(factory, "me-plugin", accountId);

        var body = await ReadMeAsync(member.Client);

        body.GetProperty("clientId").ValueKind.Should().Be(JsonValueKind.Null);
        var link = body.GetProperty("externalAccount");
        link.GetProperty("provider").GetString().Should().Be(PluginMemberSeeds.Provider);
        link.GetProperty("id").GetString().Should().Be(accountId);
    }

    /// <summary>A plugin member whose account already has a client gets that client id and the link.</summary>
    [Fact]
    public async Task PluginMemberWithAClient_GetsTheClientIdAndTheLink()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, clientId);
        using var member = await PluginMemberSeeds.SignInMemberAsync(factory, "me-plugin-client", accountId);

        var body = await ReadMeAsync(member.Client);

        body.GetProperty("clientId").GetInt32().Should().Be(clientId);
        body.GetProperty("externalAccount").GetProperty("id").GetString().Should().Be(accountId);
    }

    /// <summary>A caller who acts for no client gets a null client id and a null link.</summary>
    [Fact]
    public async Task CallerWithNoClient_GetsNullClientIdAndNullLink()
    {
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(factory, "me-none");

        var body = await ReadMeAsync(user.Client);

        body.GetProperty("clientId").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("externalAccount").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task GetMyAccountRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
