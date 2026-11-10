using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>PUT /api/clients/me/contacts/{contactId}</c>: the authenticated client updates a contact on their own account.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateMyContactTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route's contact id selects the contact and the change is written.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyContactWritesTheContact()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "update-my-contact");
        (await client.PostAsJsonAsync("/api/clients/me/contacts", ClientsTestSeeds.ContactBody("Before")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/clients/me/contacts");
        var contactId = listed.EnumerateArray().Single().GetProperty("id").GetInt32();

        var response = await client.PutAsJsonAsync(
            $"/api/clients/me/contacts/{contactId}", ClientsTestSeeds.ContactBody("After"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/clients/me/contacts");
        after.EnumerateArray().Single().GetProperty("firstName").GetString().Should().Be("After");
    }

    /// <summary>A contact id that is not on the caller's account answers 404 with <c>CLIENT_CONTACT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyContactRefusesAnUnknownContact()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "update-my-contact-unknown");

        var response = await client.PutAsJsonAsync("/api/clients/me/contacts/999999", ClientsTestSeeds.ContactBody("After"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_CONTACT_NOT_FOUND);
    }

    /// <summary>A caller with the Client role but no client row owns no account and is linked to none, so the permission gate refuses with 403 <c>CLIENT_PERMISSION_DENIED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyContactWithNoClientRowIsRefusedByThePermissionGate()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "update-my-contact-orphaned");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Clients.Where(c => c.Id == clientId).ExecuteDeleteAsync();
        }

        var response = await client.PutAsJsonAsync("/api/clients/me/contacts/1", ClientsTestSeeds.ContactBody("After"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyContactRefusesAnonymous()
    {
        var response = await factory.CreateClient()
            .PutAsJsonAsync("/api/clients/me/contacts/1", ClientsTestSeeds.ContactBody("After"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin, who is not a client, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateMyContactRefusesAnAdmin()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/api/clients/me/contacts/1", ClientsTestSeeds.ContactBody("After"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
