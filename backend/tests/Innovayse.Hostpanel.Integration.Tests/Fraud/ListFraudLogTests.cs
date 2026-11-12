using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary><c>GET /api/admin/fraud/log</c> against Postgres: the decision and reason filters, newest first and the authorization boundary.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListFraudLogTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The log filters by decision and by reason, and a reason is matched by its whole name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FiltersByDecisionAndReason()
    {
        var review = await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Order, FraudDecision.Review, null, null, "198.51.100.1", "a@example.com", 60, [FraudReasonCode.DisposableEmail]));
        var rejected = await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Registration, FraudDecision.Rejected, null, null, "198.51.100.2", "b@example.com", 100,
            [FraudReasonCode.BlockedIp, FraudReasonCode.BlockedEmailDomain]));
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var byDecision = await IdsAsync(admin, "decision=Rejected");
        var byReason = await IdsAsync(admin, "reason=BlockedEmailDomain");
        var byOtherReason = await IdsAsync(admin, "reason=DisposableEmail");
        var both = await IdsAsync(admin, "decision=Review&reason=BlockedIp");

        byDecision.Should().Contain(rejected).And.NotContain(review);
        byReason.Should().Contain(rejected).And.NotContain(review);
        byOtherReason.Should().Contain(review).And.NotContain(rejected);
        both.Should().BeEmpty();
    }

    /// <summary>The log is newest first and its rows carry the facts and reasons.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListsNewestFirstWithReasons()
    {
        var older = await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Order, FraudDecision.Approved, null, null, null, null, 0, []));
        var newer = await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Order, FraudDecision.Rejected, 3, 4, "198.51.100.3", "c@example.com", 95, [FraudReasonCode.OrderVelocity]));
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var page = await admin.GetFromJsonAsync<JsonElement>("/api/admin/fraud/log?page=1&pageSize=100");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("id").GetInt32()).ToList().IndexOf(newer).Should().BeLessThan(items.Select(i => i.GetProperty("id").GetInt32()).ToList().IndexOf(older));
        var row = items.Single(i => i.GetProperty("id").GetInt32() == newer);
        row.GetProperty("decision").GetString().Should().Be("Rejected");
        row.GetProperty("kind").GetString().Should().Be("Order");
        row.GetProperty("orderId").GetInt32().Should().Be(4);
        row.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()).Should().Equal("OrderVelocity");
    }

    /// <summary>An unknown decision or reason is refused when the query is bound.</summary>
    /// <param name="query">The query string.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("decision=Nonsense")]
    [InlineData("reason=Nonsense")]
    public async Task RefusesAnUnknownFilter(string query)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/fraud/log?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Reading the log is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/fraud/log");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.GetAsync("/api/admin/fraud/log");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Reads the ids of the log rows a filter returns.</summary>
    /// <param name="admin">The admin caller.</param>
    /// <param name="filter">The query string filter.</param>
    /// <returns>The ids, newest first.</returns>
    private static async Task<List<int>> IdsAsync(HttpClient admin, string filter)
    {
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/fraud/log?page=1&pageSize=100&{filter}");
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
    }
}
