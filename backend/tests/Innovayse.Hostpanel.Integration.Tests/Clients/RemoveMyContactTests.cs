using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>DELETE /api/clients/me/contacts/{contactId}</c>: the authenticated client removes a contact from their own account.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemoveMyContactTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The contact is removed from the caller's own account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveMyContactDropsTheContact()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "remove-my-contact");
        (await client.PostAsJsonAsync("/api/clients/me/contacts", ClientsTestSeeds.ContactBody("Gone")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/clients/me/contacts");
        var contactId = listed.EnumerateArray().Single().GetProperty("id").GetInt32();

        var response = await client.DeleteAsync($"/api/clients/me/contacts/{contactId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/clients/me/contacts");
        after.GetArrayLength().Should().Be(0);
    }

    /// <summary>A contact id that is not on the caller's account answers 404 with <c>CLIENT_CONTACT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveMyContactRefusesAnUnknownContact()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "remove-my-contact-unknown");

        var response = await client.DeleteAsync("/api/clients/me/contacts/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_CONTACT_NOT_FOUND);
    }

    /// <summary>A caller with the Client role but no client row owns no account and is linked to none, so the permission gate refuses with 403 <c>CLIENT_PERMISSION_DENIED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveMyContactWithNoClientRowIsRefusedByThePermissionGate()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "remove-my-contact-orphaned");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Clients.Where(c => c.Id == clientId).ExecuteDeleteAsync();
        }

        var response = await client.DeleteAsync("/api/clients/me/contacts/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveMyContactRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/clients/me/contacts/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin, who is not a client, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveMyContactRefusesAnAdmin()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync("/api/clients/me/contacts/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
