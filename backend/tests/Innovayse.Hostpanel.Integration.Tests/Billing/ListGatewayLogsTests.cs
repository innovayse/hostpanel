using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/gateway-logs</c> against Postgres: the admin payment-gateway log, derived from
/// recorded invoice transactions and the open hosted-gateway sessions invoices still carry.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListGatewayLogsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Records a manual payment of <paramref name="amount"/> through <paramref name="gateway"/> on a fresh invoice.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="gateway">The gateway name to record the payment under.</param>
    /// <param name="reference">The gateway transaction reference.</param>
    /// <param name="amount">The invoice total and payment amount.</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedPaymentAsync(HttpClient admin, string gateway, string reference, decimal amount)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, amount);
        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
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

    /// <summary>Reads the entries of a list response.</summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>The entry objects of the page, detached from the disposed document.</returns>
    private static async Task<List<JsonElement>> EntriesAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    /// <summary>A recorded payment appears as a Payment entry carrying its gateway and reference.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesARecordedPayment()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var gateway = $"gw-{Guid.NewGuid():N}";
        var invoiceId = await SeedPaymentAsync(admin, gateway, "ref-pay-1", 12m);

        var entries = await EntriesAsync(await admin.GetAsync($"/api/gateway-logs?gateway={gateway}"));

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

        var entries = await EntriesAsync(await admin.GetAsync($"/api/gateway-logs?gateway={module}"));

        entries.Should().ContainSingle();
        entries[0].GetProperty("result").GetString().Should().Be(nameof(GatewayLogResult.Pending));
        entries[0].GetProperty("debugData").GetString().Should().Contain("order-77");
        entries[0].GetProperty("invoiceId").GetInt32().Should().Be(invoiceId);
    }

    /// <summary>The result and debug-text filters narrow the list, and the date range excludes entries outside it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListFiltersByResultDebugTextAndDateRange()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var tag = Guid.NewGuid().ToString("N");
        await SeedPaymentAsync(admin, $"gw-{tag}", $"needle-{tag}", 8m);
        await SeedSessionAsync(admin, $"gw-{tag}", $"order-{tag}");

        var all = await EntriesAsync(await admin.GetAsync($"/api/gateway-logs?gateway=gw-{tag}"));
        var pending = await EntriesAsync(await admin.GetAsync($"/api/gateway-logs?gateway=gw-{tag}&result=Pending"));
        var byText = await EntriesAsync(await admin.GetAsync($"/api/gateway-logs?debugData=NEEDLE-{tag}"));
        var past = await EntriesAsync(await admin.GetAsync(
            $"/api/gateway-logs?gateway=gw-{tag}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}"));

        all.Should().HaveCount(2);
        pending.Should().ContainSingle().Which.GetProperty("result").GetString().Should().Be(nameof(GatewayLogResult.Pending));
        byText.Should().ContainSingle().Which.GetProperty("result").GetString().Should().Be(nameof(GatewayLogResult.Payment));
        past.Should().BeEmpty();
    }

    /// <summary>Paging splits the log and reports the page number, the page count and the total across pages.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListPagesTheLog()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var gateway = $"gw-{Guid.NewGuid():N}";
        await SeedPaymentAsync(admin, gateway, "p-1", 3m);
        await SeedPaymentAsync(admin, gateway, "p-2", 3m);
        await SeedPaymentAsync(admin, gateway, "p-3", 3m);

        var response = await admin.GetAsync($"/api/gateway-logs?gateway={gateway}&page=2&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        body.RootElement.GetProperty("pageNumber").GetInt32().Should().Be(2);
        body.RootElement.GetProperty("totalPages").GetInt32().Should().Be(2);
    }

    /// <summary>A page size over the ceiling is refused by the validator with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnOversizedPage()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/gateway-logs?pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The log is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/gateway-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The log is not for client callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAClientCaller()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, factory, "gw-log-client");

        var response = await client.GetAsync("/api/gateway-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
