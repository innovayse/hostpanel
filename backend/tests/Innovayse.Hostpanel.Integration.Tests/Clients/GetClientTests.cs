using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Clients.Commands.SetClientExternalAccount;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>GET /api/clients/{id}</c>: an admin reads a single client's full detail.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reading an existing client answers 200 with its profile.</summary>
    [Fact]
    public async Task GetExistingClientReturnsTheProfile()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Ada", lastName: "Lovelace");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("firstName").GetString().Should().Be("Ada");
    }

    /// <summary>A client with no external account link answers a null <c>externalAccount</c>.</summary>
    [Fact]
    public async Task GetClientWithoutALinkReturnsNullExternalAccount()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("externalAccount").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A linked client answers its provider and account id.</summary>
    [Fact]
    public async Task GetLinkedClientReturnsTheExternalAccount()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        (await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/external-account", new SetClientExternalAccountCommand(0, "crm", "acct-get-1")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.GetAsync($"/api/clients/{clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var link = body.RootElement.GetProperty("externalAccount");
        link.GetProperty("provider").GetString().Should().Be("crm");
        link.GetProperty("id").GetString().Should().Be("acct-get-1");
    }

    /// <summary>Reading an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task GetUnknownClientRefusesNotFound()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Reading a client is not for anonymous callers.</summary>
    [Fact]
    public async Task GetRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    [Fact]
    public async Task GetRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "get-forbidden");

        var response = await client.GetAsync($"/api/clients/{clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
