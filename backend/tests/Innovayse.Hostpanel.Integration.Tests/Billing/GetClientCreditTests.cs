using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/clients/{id}/credit</c> against Postgres: staff read one client's balance and credit history, with the
/// reason given for each adjustment, which the client's own view leaves out.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientCreditTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff read the balance, the currency and every line with its description, newest first.</summary>
    [Fact]
    public async Task ReturnsTheBalanceAndTheHistoryWithTheReasons()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount = 30m, reason = "Promo" });
        await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Deduct", amount = 5m, reason = "Fix" });

        var response = await admin.GetAsync($"/api/clients/{clientId}/credit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var credit = await response.Content.ReadFromJsonAsync<JsonElement>();
        credit.GetProperty("clientId").GetInt32().Should().Be(clientId);
        credit.GetProperty("balance").GetDecimal().Should().Be(25m);
        credit.GetProperty("currency").GetString().Should().HaveLength(3);
        var items = credit.GetProperty("entries").GetProperty("items").EnumerateArray().ToList();
        items.Select(e => e.GetProperty("description").GetString()).Should().Equal("Fix", "Promo");
        items.Select(e => e.GetProperty("change").GetDecimal()).Should().Equal(-5m, 30m);
        items[0].GetProperty("balanceAfter").GetDecimal().Should().Be(25m);
    }

    /// <summary>The route id wins over a client id in the query string.</summary>
    [Fact]
    public async Task TheRouteIdWinsOverTheQuery()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var otherId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 3m);
        await BillingTestSeeds.GiveCreditAsync(factory, otherId, 99m);

        var credit = await admin.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}/credit?clientId={otherId}");

        credit.GetProperty("balance").GetDecimal().Should().Be(3m);
    }

    /// <summary>An id that names no client is refused with 404.</summary>
    [Fact]
    public async Task RefusesAnUnknownClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{BillingTestSeeds.NoSuchId}/credit");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>The route is staff's: a client reading it, even their own id, is refused.</summary>
    [Fact]
    public async Task IsClosedToClientsAndAnonymousCallers()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "clientcredit-forbidden");

        var byClient = await owner.GetAsync($"/api/clients/{clientId}/credit");
        var anonymous = await factory.CreateClient().GetAsync($"/api/clients/{clientId}/credit");

        byClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
