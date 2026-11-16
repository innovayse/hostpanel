using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <c>POST /api/clients/{id}/vat-check</c>: staff run the VAT number check of a client. The VAT number port is a fake here;
/// the adapters have their own tests against replies shaped like the services'.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CheckClientVatTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Runs the check as an admin on a host whose port is the fake.</summary>
    /// <param name="checker">The fake port.</param>
    /// <param name="clientId">The client to check.</param>
    /// <returns>The route's answer.</returns>
    private async Task<HttpResponseMessage> CheckAsync(FakeVatNumberChecker checker, int clientId)
    {
        var host = VatTestSeeds.HostWith(factory, checker);
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        return await admin.PostAsync($"/api/clients/{clientId}/vat-check", content: null);
    }

    /// <summary>A valid answer is recorded with its date, name and address, and the client page reads it back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ValidAnswer_IsRecordedWithTheNameAndAddress()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "DE", "DE123456789");
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Valid, "Acme GmbH", "Main St 1, Berlin"));
        var host = VatTestSeeds.HostWith(factory, checker);
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);

        var response = await admin.PostAsync($"/api/clients/{clientId}/vat-check", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await VatTestSeeds.ReadAsync(factory, clientId);
        stored.Status.Should().Be(VatCheckOutcome.Valid);
        stored.CheckedAt.Should().BeCloseTo(TestMoments.Present(), TimeSpan.FromMinutes(2));
        stored.Name.Should().Be("Acme GmbH");
        stored.Address.Should().Be("Main St 1, Berlin");
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
        var vat = page.GetProperty("vat");
        vat.GetProperty("status").GetString().Should().Be("Valid");
        vat.GetProperty("name").GetString().Should().Be("Acme GmbH");
        vat.GetProperty("checkedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    /// <summary>The number is sent to the port without its prefix, spaces or punctuation, with the client's country.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheNumberIsSentNormalised()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "gr", "el 123.456.789");
        var checker = new FakeVatNumberChecker();

        var response = await CheckAsync(checker, clientId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        checker.Lookups.Should().ContainSingle().Which.Should().Be(new VatNumberLookup("GR", "123456789"));
    }

    /// <summary>An invalid answer replaces an earlier valid one and clears the name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidAnswer_ReplacesAnEarlierValidOne()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "FR", "FR12345678901", VatCheckOutcome.Valid);
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Invalid));

        var response = await CheckAsync(checker, clientId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await VatTestSeeds.ReadAsync(factory, clientId);
        stored.Status.Should().Be(VatCheckOutcome.Invalid);
        stored.Name.Should().BeNull();
        stored.CheckedAt.Should().BeOnOrAfter(VatTestSeeds.FreshCheck);
    }

    /// <summary>
    /// An answer that is no verdict is 503 and changes nothing: the client keeps the status, date and name it had, so a
    /// service that is down never turns a number invalid.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnavailableAnswer_Is503AndKeepsThePreviousStatus()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "NL", "NL123456789B01", VatCheckOutcome.Valid);
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Unavailable));

        var response = await CheckAsync(checker, clientId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.VAT_SERVICE_UNAVAILABLE);
        var stored = await VatTestSeeds.ReadAsync(factory, clientId);
        stored.Status.Should().Be(VatCheckOutcome.Valid);
        stored.CheckedAt.Should().Be(VatTestSeeds.FreshCheck);
        stored.Name.Should().Be("Seeded Name");
    }

    /// <summary>An unavailable answer for a number never judged leaves it unjudged, not invalid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnavailableAnswer_ForANeverCheckedNumber_LeavesItUnjudged()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "NL", "NL123456789B01");
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Unavailable));

        var response = await CheckAsync(checker, clientId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await VatTestSeeds.ReadAsync(factory, clientId)).Status.Should().BeNull();
    }

    /// <summary>A client with no number is refused before the service is asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientWithNoNumber_Is400()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "DE", null);
        var checker = new FakeVatNumberChecker();

        var response = await CheckAsync(checker, clientId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_VAT_NUMBER_MISSING);
        checker.Lookups.Should().BeEmpty();
    }

    /// <summary>A country with no register to ask is refused before the service is asked, and a missing country likewise.</summary>
    /// <param name="country">The client's country code, or null for none.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("US")]
    [InlineData(null)]
    public async Task CountryWithNoRegister_Is400(string? country)
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, country, "123456789");
        var checker = new FakeVatNumberChecker();

        var response = await CheckAsync(checker, clientId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_VAT_COUNTRY_NOT_SUPPORTED);
        checker.Lookups.Should().BeEmpty();
    }

    /// <summary>A number that is nothing but its prefix is invalid without asking the service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NumberThatIsOnlyAPrefix_IsInvalidWithoutAskingTheService()
    {
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "DE", "DE");
        var checker = new FakeVatNumberChecker();

        var response = await CheckAsync(checker, clientId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await VatTestSeeds.ReadAsync(factory, clientId)).Status.Should().Be(VatCheckOutcome.Invalid);
        checker.Lookups.Should().BeEmpty();
    }

    /// <summary>An id that names no client is 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownClient_Is404()
    {
        var response = await CheckAsync(new FakeVatNumberChecker(), int.MaxValue);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Without a session the route is 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutASession_Is401()
    {
        var response = await factory.CreateClient().PostAsync("/api/clients/1/vat-check", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
