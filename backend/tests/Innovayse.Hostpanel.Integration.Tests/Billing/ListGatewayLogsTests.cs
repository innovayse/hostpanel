using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/gateway-logs</c> against Postgres: the admin payment-gateway log, derived from
/// recorded invoice transactions and the open hosted-gateway sessions invoices still carry, searched over the gateway and
/// the debug text, narrowed by the outcome and a period given in days, and paged on the server.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListGatewayLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A recorded payment appears as a Payment entry carrying its gateway and reference.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesARecordedPayment()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var gateway = $"gw-{Guid.NewGuid():N}";
        var invoiceId = await SeedPaymentAsync(admin, gateway, "ref-pay-1", 12m);

        var entries = await EntriesAsync(admin, $"search={gateway}");

        entries.Should().ContainSingle();
        entries[0].GetProperty("gateway").GetString().Should().Be(gateway);
        entries[0].GetProperty("result").GetString().Should().Be(nameof(GatewayLogResult.Payment));
        entries[0].GetProperty("debugData").GetString().Should().Contain("ref-pay-1");
        entries[0].GetProperty("invoiceId").GetInt32().Should().Be(invoiceId);
    }

    /// <summary>An invoice with an unsettled gateway session appears as a Pending entry.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesAnOpenGatewaySessionAsPending()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var module = $"mod-{Guid.NewGuid():N}";
        var invoiceId = await SeedSessionAsync(admin, module, "order-77");

        var entries = await EntriesAsync(admin, $"search={module}");

        entries.Should().ContainSingle();
        entries[0].GetProperty("result").GetString().Should().Be(nameof(GatewayLogResult.Pending));
        entries[0].GetProperty("debugData").GetString().Should().Contain("order-77");
        entries[0].GetProperty("invoiceId").GetInt32().Should().Be(invoiceId);
    }

    /// <summary>The one search matches the gateway name and the debug text, in any letter case, and nothing else.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheGatewayAndTheDebugTextInAnyCase()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var tag = Guid.NewGuid().ToString("N");
        await SeedPaymentAsync(admin, $"gw-{tag}", "plain-ref", 8m);
        await SeedPaymentAsync(admin, "other-gateway", $"needle-{tag}", 8m);
        await SeedPaymentAsync(admin, "other-gateway", "unrelated-ref", 8m);

        var entries = await EntriesAsync(admin, $"search={tag.ToUpperInvariant()}");

        entries.Select(e => e.GetProperty("gateway").GetString()).Should().BeEquivalentTo([$"gw-{tag}", "other-gateway"]);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var tag = Guid.NewGuid().ToString("N");
        await SeedPaymentAsync(admin, $"gw-{tag}", $"{tag} 50% off", 3m);
        await SeedPaymentAsync(admin, $"gw-{tag}", $"{tag} 50x off", 3m);
        await SeedPaymentAsync(admin, $"gw-{tag}", $"{tag} a_c", 3m);
        await SeedPaymentAsync(admin, $"gw-{tag}", $"{tag} abc", 3m);

        var percent = await EntriesAsync(admin, $"search=ref%20{tag}%2050%25");
        var underscore = await EntriesAsync(admin, $"search=ref%20{tag}%20a_c");
        var bareWildcard = await EntriesAsync(admin, $"search=gw-{tag}%25%25");

        percent.Should().ContainSingle().Which.GetProperty("debugData").GetString().Should().Contain("50% off");
        underscore.Should().ContainSingle().Which.GetProperty("debugData").GetString().Should().Contain("a_c");
        bareWildcard.Should().BeEmpty();
    }

    /// <summary>The outcome filter keeps only the entries with that outcome.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResultFilterKeepsOnlyThatOutcome()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var tag = Guid.NewGuid().ToString("N");
        await SeedPaymentAsync(admin, $"gw-{tag}", $"needle-{tag}", 8m);
        await SeedSessionAsync(admin, $"gw-{tag}", $"order-{tag}");

        var all = await EntriesAsync(admin, $"search=gw-{tag}");
        var pending = await EntriesAsync(admin, $"search=gw-{tag}&result=Pending");
        var payment = await EntriesAsync(admin, $"search=gw-{tag}&result=Payment");
        var refund = await EntriesAsync(admin, $"search=gw-{tag}&result=Refund");

        all.Should().HaveCount(2);
        pending.Should().ContainSingle().Which.GetProperty("result").GetString().Should().Be(nameof(GatewayLogResult.Pending));
        payment.Should().ContainSingle().Which.GetProperty("result").GetString().Should().Be(nameof(GatewayLogResult.Payment));
        refund.Should().BeEmpty();
    }

    /// <summary>The period, given as bare days, keeps the whole of its first and last day and nothing outside them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PeriodKeepsTheWholeLastDay()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var gateway = $"gw-{Guid.NewGuid():N}";
        var day = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        await SeedPaymentAsync(admin, gateway, "before", 3m, day.AddSeconds(-1));
        await SeedPaymentAsync(admin, gateway, "first-instant", 3m, day);
        await SeedPaymentAsync(admin, gateway, "last-second", 3m, day.AddDays(1).AddSeconds(-1));
        await SeedPaymentAsync(admin, gateway, "after", 3m, day.AddDays(1));

        var oneDay = await EntriesAsync(admin, $"search={gateway}&from=2026-03-10&to=2026-03-10");
        var fromOnly = await EntriesAsync(admin, $"search={gateway}&from=2026-03-10");
        var toOnly = await EntriesAsync(admin, $"search={gateway}&to=2026-03-09");

        oneDay.Select(e => e.GetProperty("debugData").GetString()).Should().HaveCount(2)
            .And.Contain(d => d!.Contains("first-instant", StringComparison.Ordinal))
            .And.Contain(d => d!.Contains("last-second", StringComparison.Ordinal));
        fromOnly.Should().HaveCount(3);
        toOnly.Should().ContainSingle();
    }

    /// <summary>Paging splits the log, newest first, and reports the page number, the page count and the total across pages.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListPagesTheLog()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var gateway = $"gw-{Guid.NewGuid():N}";
        var day = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        await SeedPaymentAsync(admin, gateway, "p-1", 3m, day);
        await SeedPaymentAsync(admin, gateway, "p-2", 3m, day.AddMinutes(1));
        await SeedPaymentAsync(admin, gateway, "p-3", 3m, day.AddMinutes(2));

        var response = await admin.GetAsync($"/api/gateway-logs?search={gateway}&page=2&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        body.RootElement.GetProperty("items")[0].GetProperty("debugData").GetString().Should().Contain("p-1");
        body.RootElement.GetProperty("pageNumber").GetInt32().Should().Be(2);
        body.RootElement.GetProperty("totalPages").GetInt32().Should().Be(2);
    }

    /// <summary>A page below one, no page, or a page size outside the limit is refused with 400 on the page and page size, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pagesize")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    public async Task PagingOutsideTheLimitsIsRefused(string queryString, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/gateway-logs?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit, or a period that ends before it starts, is refused with 400 on that field.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("search=TOO_LONG", "search")]
    [InlineData("from=2026-03-11&to=2026-03-10", "to")]
    public async Task FilterValueOutOfRangeIsRefused(string filter, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var value = filter.Replace("TOO_LONG", new string('a', PagingLimits.MaxSearchLength + 1), StringComparison.Ordinal);

        var response = await admin.GetAsync($"/api/gateway-logs?page=1&pageSize=20&{value}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>
    /// A result that names no outcome, by name or by number, is refused with 400: the model binder turns it back before the
    /// validator runs, so the refusal is the validation code without a field of the validator's own.
    /// </summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("result=Sideways")]
    [InlineData("result=99")]
    public async Task UnknownResultIsRefused(string filter)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/gateway-logs?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>The log is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/gateway-logs?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The log is not for client callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAClientCaller()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, factory, "gw-log-client");

        var response = await client.GetAsync("/api/gateway-logs?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Reads the entries of one page of the log, narrowed by the filters given.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="filters">The filters as a query string; the page is the first, of a hundred rows.</param>
    /// <returns>The entry objects of the page, detached from the disposed document.</returns>
    private static async Task<List<JsonElement>> EntriesAsync(HttpClient admin, string filters)
    {
        var page = await PagedListReads.PageAsync(admin, $"/api/gateway-logs?page=1&pageSize=100&{filters}");
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    /// <summary>Records a manual payment of <paramref name="amount"/> through <paramref name="gateway"/> on a fresh invoice.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="gateway">The gateway name to record the payment under.</param>
    /// <param name="reference">The gateway transaction reference.</param>
    /// <param name="amount">The invoice total and payment amount.</param>
    /// <param name="date">The moment of the payment; <see cref="TestMoments.Seeded"/> when not given.</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedPaymentAsync(HttpClient admin, string gateway, string reference, decimal amount, DateTimeOffset? date = null)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, amount);
        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = date ?? TestMoments.Seeded,
            gateway,
            transactionId = reference,
            amount,
            fees = 0m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return invoiceId;
    }

    /// <summary>Starts a hosted-gateway session directly on a fresh unpaid invoice.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="module">The payment plugin id of the session.</param>
    /// <param name="orderId">The gateway-side order id.</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedSessionAsync(HttpClient admin, string module, string orderId)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 5m);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        invoice.SetGatewaySession(module, orderId, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoiceId;
    }
}
