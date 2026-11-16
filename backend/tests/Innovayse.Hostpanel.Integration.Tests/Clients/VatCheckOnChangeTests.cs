using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// A client's VAT number is checked again, in the background, when the number or the billing country is saved: by staff on the
/// client page and by the client in the portal. A save that changes neither asks nothing. The VAT number port is a fake.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class VatCheckOnChangeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The body of the staff edit route with the given number and country; the rest is left as a new client has it.</summary>
    /// <param name="taxId">The VAT number to send, or null for none.</param>
    /// <param name="country">The billing country to send, or null for none.</param>
    /// <returns>The request body.</returns>
    private static object UpdateBody(string? taxId, string? country) => new
    {
        firstName = "Ada",
        lastName = "Lovelace",
        companyName = (string?)null,
        phone = (string?)null,
        address = new { street = (string?)null, address2 = (string?)null, city = (string?)null, state = (string?)null, postCode = (string?)null, country },
        preferences = new
        {
            language = (string?)null,
            currency = (string?)null,
            paymentMethod = (string?)null,
            billingContact = (string?)null,
            adminNotes = (string?)null,
        },
        notifications = ClientsTestSeeds.AllNotificationsOn,
        settings = new
        {
            overdueNotices = true,
            taxExempt = false,
            taxId,
            separateInvoices = false,
            disableCcProcessing = false,
            marketingOptIn = false,
            statusUpdate = true,
            allowSso = true,
        },
    };

    /// <summary>Sends a request from the host and waits until the background check it caused has run.</summary>
    /// <param name="host">The host whose bus runs the check.</param>
    /// <param name="send">The request to send.</param>
    /// <returns>The response of the request.</returns>
    private static async Task<HttpResponseMessage> SendAndWaitAsync(WebApplicationFactory<Program> host, Func<Task<HttpResponseMessage>> send)
    {
        HttpResponseMessage? response = null;
        Func<IMessageContext, Task> action = async _ => response = await send();

        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(action);

        return response!;
    }

    /// <summary>Saving a new number and country checks it and records the verdict; saving the same again asks nothing; changing either asks again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StaffSavingANumberOrCountry_ChecksItOnlyWhenItChanged()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, null, null);
        var checker = new FakeVatNumberChecker();
        var host = VatTestSeeds.HostWith(factory, checker);
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);

        var created = await SendAndWaitAsync(host, () => admin.PutAsJsonAsync($"/api/clients/{clientId}", UpdateBody("DE123456789", "DE")));
        created.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await VatTestSeeds.ReadAsync(factory, clientId)).Should().Match<VatTestSeeds.StoredVat>(
            s => s.Status == VatCheckOutcome.Valid && s.Name == "Registered Name");
        checker.Lookups.Should().Equal([new VatNumberLookup("DE", "123456789")]);

        await SendAndWaitAsync(host, () => admin.PutAsJsonAsync($"/api/clients/{clientId}", UpdateBody(" DE123456789 ", "de")));
        checker.Lookups.Should().HaveCount(1, "the same number in the same country is not asked about again");
        (await VatTestSeeds.ReadAsync(factory, clientId)).Status.Should().Be(VatCheckOutcome.Valid);

        await SendAndWaitAsync(host, () => admin.PutAsJsonAsync($"/api/clients/{clientId}", UpdateBody("DE123456780", "DE")));
        checker.Lookups.Should().HaveCount(2);
        checker.Lookups[1].Should().Be(new VatNumberLookup("DE", "123456780"));

        await SendAndWaitAsync(host, () => admin.PutAsJsonAsync($"/api/clients/{clientId}", UpdateBody("123456780", "AT")));
        checker.Lookups.Should().HaveCount(3);
        checker.Lookups[2].Should().Be(new VatNumberLookup("AT", "123456780"));
    }

    /// <summary>A change that leaves nothing to check forgets the verdict and asks nothing: the number is removed, or the country has no register.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StaffRemovingTheNumberOrMovingToACountryWithNoRegister_ForgetsTheVerdictAndAsksNothing()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "DE", "DE123456789", VatCheckOutcome.Valid);
        var checker = new FakeVatNumberChecker();
        var host = VatTestSeeds.HostWith(factory, checker);
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);

        await SendAndWaitAsync(host, () => admin.PutAsJsonAsync($"/api/clients/{clientId}", UpdateBody("DE123456789", "US")));
        (await VatTestSeeds.ReadAsync(factory, clientId)).Status.Should().BeNull();

        await SendAndWaitAsync(host, () => admin.PutAsJsonAsync($"/api/clients/{clientId}", UpdateBody(null, "US")));
        var stored = await VatTestSeeds.ReadAsync(factory, clientId);
        stored.TaxId.Should().BeNull();
        stored.Status.Should().BeNull();
        checker.Lookups.Should().BeEmpty();
    }

    /// <summary>A number the service could not judge leaves the client unjudged, and the save itself still succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceDown_LeavesTheNumberUnjudgedAndTheSaveSucceeds()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, null, null);
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Unavailable));
        var host = VatTestSeeds.HostWith(factory, checker);
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);

        var response = await SendAndWaitAsync(host, () => admin.PutAsJsonAsync($"/api/clients/{clientId}", UpdateBody("DE123456789", "DE")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        checker.Lookups.Should().HaveCount(1);
        (await VatTestSeeds.ReadAsync(factory, clientId)).Should().Match<VatTestSeeds.StoredVat>(
            s => s.TaxId == "DE123456789" && s.Status == null);
    }

    /// <summary>A client enters their own VAT number in the portal: it is saved, checked, and read back with its status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientEnteringTheirNumberInThePortal_SavesAndChecksIt()
    {
        var (own, clientId, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "vat-portal");
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Valid, "Portal Co", null));
        var host = VatTestSeeds.HostWith(factory, checker);
        var portal = host.CreateClient();
        portal.DefaultRequestHeaders.Authorization = own.DefaultRequestHeaders.Authorization;

        var response = await SendAndWaitAsync(host, () => portal.PatchAsJsonAsync(
            "/api/clients/me", new { vatNumber = "fr 12 345678901", address = new { country = "FR" } }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("vatNumber").GetString().Should().Be("fr 12 345678901");
        (await VatTestSeeds.ReadAsync(factory, clientId)).Should().Match<VatTestSeeds.StoredVat>(
            s => s.Status == VatCheckOutcome.Valid && s.Name == "Portal Co");
        checker.Lookups.Should().Equal([new VatNumberLookup("FR", "12345678901")]);
        var profile = await portal.GetFromJsonAsync<JsonElement>("/api/clients/me");
        profile.GetProperty("vat").GetProperty("status").GetString().Should().Be("Valid");
        profile.GetProperty("vat").GetProperty("name").GetString().Should().Be("Portal Co");
    }

    /// <summary>A portal save that leaves the number out keeps it, and an empty one removes it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PortalSaveWithoutTheNumberKeepsIt_AndAnEmptyOneRemovesIt()
    {
        var (own, clientId, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "vat-portal-keep");
        var checker = new FakeVatNumberChecker();
        var host = VatTestSeeds.HostWith(factory, checker);
        var portal = host.CreateClient();
        portal.DefaultRequestHeaders.Authorization = own.DefaultRequestHeaders.Authorization;
        await SendAndWaitAsync(host, () => portal.PatchAsJsonAsync("/api/clients/me", new { vatNumber = "DE123456789", address = new { country = "DE" } }));

        await SendAndWaitAsync(host, () => portal.PatchAsJsonAsync("/api/clients/me", new { firstName = "Renamed" }));
        (await VatTestSeeds.ReadAsync(factory, clientId)).Should().Match<VatTestSeeds.StoredVat>(
            s => s.TaxId == "DE123456789" && s.Status == VatCheckOutcome.Valid);

        await SendAndWaitAsync(host, () => portal.PatchAsJsonAsync("/api/clients/me", new { vatNumber = string.Empty }));
        (await VatTestSeeds.ReadAsync(factory, clientId)).Should().Match<VatTestSeeds.StoredVat>(
            s => s.TaxId == null && s.Status == null);
    }
}
