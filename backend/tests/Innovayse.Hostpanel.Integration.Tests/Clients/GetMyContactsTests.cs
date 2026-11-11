using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>GET /api/clients/me/contacts</c>: the authenticated client lists the contacts on their own account.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyContactsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A new account has no contacts, and a contact added to it is listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyContactsListsTheOwnContacts()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "get-my-contacts");
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/clients/me/contacts");
        empty.GetArrayLength().Should().Be(0);
        (await client.PostAsJsonAsync("/api/clients/me/contacts", ClientsTestSeeds.ContactBody("Listed")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var listed = await client.GetFromJsonAsync<JsonElement>("/api/clients/me/contacts");

        listed.GetArrayLength().Should().Be(1);
    }

    /// <summary>A caller with the Client role but no client row owns no account and is linked to none, so the permission gate refuses with 403 <c>CLIENT_PERMISSION_DENIED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyContactsWithNoClientRowIsRefusedByThePermissionGate()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "get-my-contacts-orphaned");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Clients.Where(c => c.Id == clientId).ExecuteDeleteAsync();
        }

        var response = await client.GetAsync("/api/clients/me/contacts");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyContactsRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/clients/me/contacts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin, who is not a client, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetMyContactsRefusesAnAdmin()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/clients/me/contacts");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
