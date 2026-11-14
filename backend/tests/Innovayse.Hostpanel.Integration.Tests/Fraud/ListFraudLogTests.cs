using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary>
/// <c>GET /api/admin/fraud/log</c> against Postgres: the search on the e-mail and IP address (a <c>%</c> or <c>_</c>
/// in it matches itself), the kind, decision and reason filters, filters applied before paging, newest first, the
/// refusal of a bad paging window, search or filter, and the authorization boundary.
/// </summary>
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

    /// <summary>The search finds an entry by a part of its e-mail address or of its IP address, in any letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchMatchesTheEmailAndTheIpAddress()
    {
        var mix = await SeedMixAsync();
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var byEmail = await IdsAsync(admin, $"search={mix.Token.ToUpperInvariant()}");
        var byIp = await IdsAsync(admin, $"search={Uri.EscapeDataString(mix.RejectedIp)}");

        byEmail.Should().Equal(mix.Approved, mix.Rejected, mix.Review);
        byIp.Should().Equal(mix.Rejected);
    }

    /// <summary>A <c>%</c> or <c>_</c> in the search is text to find, not a wildcard that matches every entry.</summary>
    /// <param name="wildcard">The character typed after the token.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task SearchTreatsAWildcardAsText(string wildcard)
    {
        var mix = await SeedMixAsync();
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var found = await IdsAsync(admin, $"search={Uri.EscapeDataString(mix.Token + wildcard)}");

        found.Should().BeEmpty();
    }

    /// <summary>The kind filter keeps only the checks of that kind, and it narrows a search.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FiltersByKind()
    {
        var mix = await SeedMixAsync();
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var registrations = await IdsAsync(admin, $"search={mix.Token}&kind=Registration");
        var orders = await IdsAsync(admin, $"search={mix.Token}&kind=Order");

        registrations.Should().Equal(mix.Rejected);
        orders.Should().Equal(mix.Approved, mix.Review);
    }

    /// <summary>Filters are applied before paging: the total counts every match and each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var mix = await SeedMixAsync();
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var scope = $"/api/admin/fraud/log?pageSize=2&search={mix.Token}";

        var first = await admin.GetFromJsonAsync<JsonElement>($"{scope}&page=1");
        var second = await admin.GetFromJsonAsync<JsonElement>($"{scope}&page=2");
        var beyond = await admin.GetFromJsonAsync<JsonElement>($"{scope}&page=3");
        var orderOnly = await admin.GetFromJsonAsync<JsonElement>($"{scope}&page=1&kind=Order");

        IdsOf(first).Should().Equal(mix.Approved, mix.Rejected);
        first.GetProperty("totalCount").GetInt32().Should().Be(3);
        IdsOf(second).Should().Equal(mix.Review);
        second.GetProperty("totalCount").GetInt32().Should().Be(3);
        IdsOf(beyond).Should().BeEmpty();
        IdsOf(orderOnly).Should().Equal(mix.Approved, mix.Review);
        orderOnly.GetProperty("totalCount").GetInt32().Should().Be(2);
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

    /// <summary>A missing page, a page below one or a page size outside the limit is refused with 400 on the field, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pageSize")]
    [InlineData("page=1&pageSize=101", "pageSize")]
    public async Task PagingOutsideTheLimitsIsRefused(string queryString, string field)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/fraud/log?{queryString}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the field, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/fraud/log?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "search");
    }

    /// <summary>A kind, decision or reason that names nothing, by name or by number, is refused with 400 rather than listing every entry.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("kind=Nonsense", "kind")]
    [InlineData("kind=99", "kind")]
    [InlineData("decision=Nonsense", "decision")]
    [InlineData("decision=99", "decision")]
    [InlineData("reason=Nonsense", "reason")]
    [InlineData("reason=99", "reason")]
    public async Task RefusesAnUnknownFilter(string filter, string field)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/fraud/log?page=1&pageSize=100&{filter}");

        // The model binder refuses a value that is no member before the validator runs, so the check is that the field is named.
        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>Reading the log is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/fraud/log?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.GetAsync("/api/admin/fraud/log?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Writes three decisions of different kinds that share a token in their e-mail address.</summary>
    /// <returns>The token, the ids of the three entries and the IP address of the rejected one.</returns>
    private async Task<FraudLogMix> SeedMixAsync()
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        var review = await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Order, FraudDecision.Review, null, null, $"2001:db8:{token[..4]}::10", $"{token}-review@example.com", 60,
            [FraudReasonCode.DisposableEmail]));
        var rejectedIp = $"2001:db8:{token[..4]}::20";
        var rejected = await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Registration, FraudDecision.Rejected, null, null, rejectedIp, $"{token}-rejected@example.com", 100,
            [FraudReasonCode.BlockedIp]));
        var approved = await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Order, FraudDecision.Approved, null, null, null, $"{token}-approved@example.com", 0, []));
        return new FraudLogMix(token, review, rejected, approved, rejectedIp);
    }

    /// <summary>Reads the ids of the log rows a filter returns.</summary>
    /// <param name="admin">The admin caller.</param>
    /// <param name="filter">The query string filter.</param>
    /// <returns>The ids, newest first.</returns>
    private static async Task<List<int>> IdsAsync(HttpClient admin, string filter)
    {
        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/fraud/log?page=1&pageSize=100&{filter}");
        return IdsOf(page);
    }

    /// <summary>Reads the ids of the rows of a page.</summary>
    /// <param name="page">The page the API answered.</param>
    /// <returns>The ids, in the order the page holds them.</returns>
    private static List<int> IdsOf(JsonElement page)
    {
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
    }

    /// <summary>What <see cref="SeedMixAsync"/> wrote: the shared token and the three entries.</summary>
    /// <param name="Token">Lower-case text every e-mail address of the three entries contains.</param>
    /// <param name="Review">The order held for review.</param>
    /// <param name="Rejected">The registration that was rejected.</param>
    /// <param name="Approved">The order that was approved.</param>
    /// <param name="RejectedIp">The IP address the rejected registration came from.</param>
    private sealed record FraudLogMix(string Token, int Review, int Rejected, int Approved, string RejectedIp);
}
