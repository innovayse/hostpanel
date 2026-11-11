using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>POST /api/clients/{id}/contacts</c>: adding an additional contact to a client account.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddContactTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Adding a contact writes it onto the client's record and answers 201 with the new contact's id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddWritesTheContact()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Billing"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var contactId = await response.Content.ReadFromJsonAsync<int>();
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        var contact = fetched.GetProperty("contacts").EnumerateArray().Single();
        contact.GetProperty("firstName").GetString().Should().Be("Billing");
        contact.GetProperty("id").GetInt32().Should().Be(contactId);
    }

    /// <summary>A second contact with an address the client already has answers 409 <c>CONTACT_EMAIL_IN_USE</c>, whatever the case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddRefusesAnAddressAnotherContactUses()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var address = $"dupe-{Guid.NewGuid():N}@example.test";
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("First", address, true)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Second", address.ToUpperInvariant(), true));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CONTACT_EMAIL_IN_USE);
        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("contacts").GetArrayLength().Should().Be(1);
    }

    /// <summary>Another client may hold the same address: the rule is per client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddAllowsTheAddressAnotherClientUses()
    {
        var (firstClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (secondClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var address = $"shared-{Guid.NewGuid():N}@example.test";

        (await admin.PostAsJsonAsync($"/api/clients/{firstClientId}/contacts", ClientsTestSeeds.ContactBody("First", address, true)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.PostAsJsonAsync($"/api/clients/{secondClientId}/contacts", ClientsTestSeeds.ContactBody("Second", address, true)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A closed account's contacts are frozen: adding one answers 409 <c>CLIENT_RECORD_CLOSED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddRefusesAClosedClient()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClosedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Billing"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_RECORD_CLOSED);
    }

    /// <summary>Adding a contact stores its country upper case and leaves one activity line without the contact's data.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddNormalisesTheCountryAndIsAuditedWithTheActingAdmin()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var body = new
        {
            firstName = "Billing",
            lastName = "Probe",
            companyName = (string?)null,
            email = $"country-{Guid.NewGuid():N}@example.test",
            phone = (string?)null,
            type = "General",
            address = new
            {
                street = (string?)null,
                address2 = (string?)null,
                city = (string?)null,
                state = (string?)null,
                postCode = (string?)null,
                country = "am",
            },
            notifications = ClientsTestSeeds.AllNotificationsOn,
        };

        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        fetched.GetProperty("contacts").EnumerateArray().Single().GetProperty("address").GetProperty("country").GetString().Should().Be("AM");
        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Added Contact");
    }

    /// <summary>Adding a contact to an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/contacts", ClientsTestSeeds.ContactBody("Probe"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Adding a contact is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/contacts", ClientsTestSeeds.ContactBody("Probe"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403 on the admin route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "add-contact-forbidden");

        var response = await client.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Probe"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A contact added, updated and removed through the client portal's own
    /// <c>POST /api/clients/me/contacts</c> round-trips the same way the admin route does.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MyContactsAddUpdateRemoveRoundTrips()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-contacts-happy");

        var added = await client.PostAsJsonAsync("/api/clients/me/contacts", ClientsTestSeeds.ContactBody("Billing"));
        added.StatusCode.Should().Be(HttpStatusCode.Created);

        var listed = await client.GetFromJsonAsync<JsonElement>("/api/clients/me/contacts");
        var contactId = listed.EnumerateArray().Single().GetProperty("id").GetInt32();

        var updated = await client.PutAsJsonAsync($"/api/clients/me/contacts/{contactId}", ClientsTestSeeds.ContactBody("Renamed"));
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var removed = await client.DeleteAsync($"/api/clients/me/contacts/{contactId}");
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterRemoval = await client.GetFromJsonAsync<JsonElement>("/api/clients/me/contacts");
        afterRemoval.GetArrayLength().Should().Be(0);
    }
}
