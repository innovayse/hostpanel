using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/clients/{clientId}/billable-items</c> against Postgres: creating a one-off charge
/// for a client, which then appears in their uninvoiced list.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateBillableItemTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a billable item, then listing it, round-trips the amount.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenListRoundTripsTheBillableItem()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
        {
            description = "Consulting hour",
            amount = 120m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var itemId = await created.Content.ReadFromJsonAsync<int>();

        var list = await admin.GetAsync($"/api/clients/{clientId}/billable-items");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var uninvoiced = body.RootElement.GetProperty("uninvoicedItems");
        uninvoiced.EnumerateArray().Should().Contain(
            item => item.GetProperty("id").GetInt32() == itemId);
    }

    /// <summary>
    /// A one-time item accepts the day it is invoiced, sent as the admin form sends it (a bare ISO
    /// day), and keeps it, so the daily run can invoice it on that day.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateKeepsTheInvoiceDayOfAOneTimeItem()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
        {
            description = "Setup fee",
            amount = 50m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
            nextDueDate = "2030-01-15",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var itemId = await created.Content.ReadFromJsonAsync<int>();

        var list = await admin.GetAsync($"/api/clients/{clientId}/billable-items");
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var item = body.RootElement.GetProperty("uninvoicedItems").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetInt32() == itemId);
        item.GetProperty("nextDueDate").GetDateTimeOffset().Date.Should().Be(new DateTime(2030, 1, 15));
    }

    /// <summary>Creating a billable item is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/clients/1/billable-items", new
        {
            description = "Consulting hour",
            amount = 120m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A billable item for a client that does not exist answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnUnknownClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{BillingTestSeeds.NoSuchId}/billable-items", new
        {
            description = "Consulting hour",
            amount = 120m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }
}
