using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>DELETE /api/clients/{clientId}/billable-items/{id}</c> against Postgres: removing an
/// uninvoiced billable item, and the <c>BILLABLE_ITEM_NOT_FOUND</c> refusal for an id that names
/// no billable item.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteBillableItemTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting an uninvoiced billable item removes it from the uninvoiced list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRemovesTheBillableItem()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
        {
            description = "Consulting hour",
            amount = 120m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });
        var itemId = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.DeleteAsync($"/api/clients/{clientId}/billable-items/{itemId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await admin.GetAsync($"/api/clients/{clientId}/billable-items");
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("uninvoicedItems").EnumerateArray()
            .Should().NotContain(item => item.GetProperty("id").GetInt32() == itemId);
    }

    /// <summary>Deleting an id that names no billable item answers 404 with <c>BILLABLE_ITEM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteUnknownBillableItemRefusesNotFound()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"/api/clients/{clientId}/billable-items/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.BILLABLE_ITEM_NOT_FOUND);
    }

    /// <summary>Deleting a billable item is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/clients/1/billable-items/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
