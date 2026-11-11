using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>PUT /api/clients/{id}/contacts/{contactId}</c>: editing an existing contact.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateContactTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating a contact writes the change back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateWritesTheContact()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var added = await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Billing"));
        added.StatusCode.Should().Be(HttpStatusCode.Created);
        var contacts = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        var contactId = contacts.GetProperty("contacts").EnumerateArray().Single().GetProperty("id").GetInt32();

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/contacts/{contactId}", ClientsTestSeeds.ContactBody("Renamed"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("contacts").EnumerateArray().Single().GetProperty("firstName").GetString().Should().Be("Renamed");
    }

    /// <summary>Updating a contact on an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/contacts/1", ClientsTestSeeds.ContactBody("Probe"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Updating a contact id that is not this client's answers 404 with <c>CLIENT_CONTACT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesAWrongContactId()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        const int wrongContactId = 999_999;

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/contacts/{wrongContactId}", ClientsTestSeeds.ContactBody("Probe"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_CONTACT_NOT_FOUND);
    }

    /// <summary>Updating a contact is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/contacts/1", ClientsTestSeeds.ContactBody("Probe"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403 on the admin route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "update-contact-forbidden");

        var response = await client.PutAsJsonAsync($"/api/clients/{clientId}/contacts/1", ClientsTestSeeds.ContactBody("Probe"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Updating a contact id that is not the caller's own, through the portal's own
    /// <c>PUT /api/clients/me/contacts/{contactId}</c>, answers 404 with <c>CLIENT_CONTACT_NOT_FOUND</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyContactsWrongIdRefusesNotFound()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-update-contact-wrong-id");

        var response = await client.PutAsJsonAsync("/api/clients/me/contacts/999999", ClientsTestSeeds.ContactBody("Anyone"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_CONTACT_NOT_FOUND);
    }

    /// <summary>Moving a contact onto the address another contact of the client uses answers 409 <c>CONTACT_EMAIL_IN_USE</c>; keeping its own is fine.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesAnAddressAnotherContactUses()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var first = $"first-{Guid.NewGuid():N}@example.test";
        var second = $"second-{Guid.NewGuid():N}@example.test";
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("First", first, true)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var secondId = await (await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Second", second, true))).Content.ReadFromJsonAsync<int>();

        var clash = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/contacts/{secondId}", ClientsTestSeeds.ContactBody("Second", first, true));
        var own = await admin.PutAsJsonAsync(
            $"/api/clients/{clientId}/contacts/{secondId}", ClientsTestSeeds.ContactBody("Renamed", second, true));

        clash.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(clash, HostpanelErrorCodes.CONTACT_EMAIL_IN_USE);
        own.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating a contact leaves one activity line naming the client and the acting admin.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateIsAuditedWithTheActingAdmin()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var contactId = await (await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Billing"))).Content.ReadFromJsonAsync<int>();

        (await admin.PutAsJsonAsync($"/api/clients/{clientId}/contacts/{contactId}", ClientsTestSeeds.ContactBody("Renamed")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Modified Contact");
    }
}
