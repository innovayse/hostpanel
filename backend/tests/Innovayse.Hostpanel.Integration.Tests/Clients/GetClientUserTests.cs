using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// Integration tests for the <c>GetClientUser</c> use case: <c>GET /api/clients/{id}/user</c> loads
/// the client and the identity account behind it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientUserTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A real client answers its linked person.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetReturnsTheLinkedPerson()
    {
        var (_, clientId, userId) = await ClientsTestSeeds.RegisterClientAsync(factory, "client-user-detail");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/user");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(userId);
        body.GetProperty("accounts").GetArrayLength().Should().Be(1);
    }

    /// <summary>An id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/user");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>A client whose linked subject resolves to nobody answers 404 with <c>IDENTITY_USER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesAClientWhoseAccountIsMissing()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/user");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/user");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
