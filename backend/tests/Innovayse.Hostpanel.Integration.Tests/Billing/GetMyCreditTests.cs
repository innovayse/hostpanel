using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/credit</c> against Postgres: the signed-in client reads their own balance, the top-up limits and a page of
/// the history; the account comes from the token, and the staff's notes never reach the client.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyCreditTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Adds credit to a client as staff, so the history has a line.</summary>
    /// <param name="admin">The admin session.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="amount">The credit to add.</param>
    /// <param name="reason">The staff's note.</param>
    /// <returns>A task that completes once the adjustment is saved.</returns>
    private static async Task AddAsync(HttpClient admin, int clientId, decimal amount, string reason)
    {
        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount, reason });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>The credit carries the balance, its currency and the limits of one top-up.</summary>
    [Fact]
    public async Task ReportsTheBalanceTheCurrencyAndTheLimits()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-basic");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 12.5m);

        var credit = await CreditTestSeeds.MyCreditAsync(owner);

        credit.GetProperty("balance").GetDecimal().Should().Be(12.5m);
        credit.GetProperty("currency").GetString().Should().HaveLength(3);
        credit.GetProperty("topUpMinimum").GetDecimal().Should().BePositive();
        credit.GetProperty("topUpMaximum").GetDecimal().Should().BeGreaterThan(credit.GetProperty("topUpMinimum").GetDecimal());
    }

    /// <summary>Credit older than the history is a balance without lines: nothing is invented for it.</summary>
    [Fact]
    public async Task CreditOlderThanTheHistory_HasNoLines()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-legacy");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 7m);

        var credit = await CreditTestSeeds.MyCreditAsync(owner);

        credit.GetProperty("balance").GetDecimal().Should().Be(7m);
        credit.GetProperty("entries").GetProperty("items").GetArrayLength().Should().Be(0);
        credit.GetProperty("entries").GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    /// <summary>The history lists the newest line first, with the type, the signed change and the balance it left, and pages.</summary>
    [Fact]
    public async Task ListsTheHistoryNewestFirstAndPages()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-history");
        await AddAsync(admin, clientId, 10m, "one");
        await AddAsync(admin, clientId, 20m, "two");
        await AddAsync(admin, clientId, 30m, "three");

        var first = await CreditTestSeeds.MyCreditAsync(owner, "?page=1&pageSize=2");
        var second = await CreditTestSeeds.MyCreditAsync(owner, "?page=2&pageSize=2");

        var page = first.GetProperty("entries");
        page.GetProperty("totalCount").GetInt32().Should().Be(3);
        page.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("balanceAfter").GetDecimal()).Should().Equal(60m, 30m);
        var newest = page.GetProperty("items")[0];
        newest.GetProperty("movement").GetString().Should().Be(nameof(CreditMovementType.AdminAdd));
        newest.GetProperty("change").GetDecimal().Should().Be(30m);
        second.GetProperty("entries").GetProperty("items").EnumerateArray().Select(e => e.GetProperty("balanceAfter").GetDecimal())
            .Should().Equal(10m);
    }

    /// <summary>The staff's note is not written for the client: no line the client reads carries a description.</summary>
    [Fact]
    public async Task NeverShowsTheStaffNote()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-note");
        await AddAsync(admin, clientId, 10m, "Internal: this client is a friend of the owner");

        var response = await owner.GetAsync("/api/me/credit");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Internal");
        body.Should().NotContain("description");
    }

    /// <summary>A client reads only their own account: another client's lines and balance are not in it.</summary>
    [Fact]
    public async Task ReadsOnlyTheCallersOwnAccount()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (mine, myId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-mine");
        var (_, otherId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-other");
        await AddAsync(admin, myId, 10m, "mine");
        await AddAsync(admin, otherId, 99m, "theirs");

        var credit = await CreditTestSeeds.MyCreditAsync(mine);

        credit.GetProperty("balance").GetDecimal().Should().Be(10m);
        credit.GetProperty("entries").GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    /// <summary>A client id in the query is ignored: the account is the token's.</summary>
    [Fact]
    public async Task IgnoresAClientIdInTheQuery()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (mine, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-queryid");
        var (_, otherId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-queryother");
        await AddAsync(admin, otherId, 99m, "theirs");

        var credit = await CreditTestSeeds.MyCreditAsync(mine, $"?clientId={otherId}");

        credit.GetProperty("balance").GetDecimal().Should().Be(0m);
    }

    /// <summary>The route needs a signed-in client, and an administrator without a client record has no credit of their own.</summary>
    [Fact]
    public async Task NeedsASignedInClientWithAnAccount()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/me/credit");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var byAdmin = await admin.GetAsync("/api/me/credit");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        byAdmin.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    /// <summary>An oversized page is clamped rather than refused, so the table never asks for the whole ledger at once.</summary>
    [Fact]
    public async Task ClampsAnOversizedPage()
    {
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "mycredit-clamp");

        var credit = await CreditTestSeeds.MyCreditAsync(owner, "?page=0&pageSize=100000");

        credit.GetProperty("entries").GetProperty("pageNumber").GetInt32().Should().Be(1);
        credit.GetProperty("entries").GetProperty("pageSize").GetInt32().Should().BeLessThan(1000);
        credit.ValueKind.Should().Be(JsonValueKind.Object);
    }
}
