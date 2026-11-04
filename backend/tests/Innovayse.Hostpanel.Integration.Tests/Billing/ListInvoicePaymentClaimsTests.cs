using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/payment-claims</c> against Postgres: the admin list of invoice payment claims, filtered by status.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListInvoicePaymentClaimsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds a claim for an invoice, left open or ended as held.</summary>
    /// <param name="clientId">The client whose method was charged.</param>
    /// <param name="invoiceId">The invoice the charge is for.</param>
    /// <param name="held">Whether the claim ends as held; otherwise it stays open.</param>
    /// <returns>The new claim's primary key.</returns>
    private async Task<int> SeedClaimAsync(int clientId, int invoiceId, bool held)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;
        var claim = InvoicePaymentClaim.Open(
            invoiceId, clientId, "claims-gateway", "pm-1", Guid.NewGuid().ToString("N"), 1250, "USD", now);
        if (held)
        {
            claim.MarkHeld("tx-held-1", now);
        }

        context.InvoicePaymentClaims.Add(claim);
        await context.SaveChangesAsync(CancellationToken.None);
        return claim.Id;
    }

    /// <summary>Reads the ids on the page a request returned.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="url">The list address.</param>
    /// <returns>The parsed body; the caller disposes it.</returns>
    private static async Task<JsonDocument> ReadAsync(HttpClient admin, string url)
    {
        var response = await admin.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Listing claims is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/payment-claims");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client token is refused: the list is for staff.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ClientToken_Returns403()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "claims-list");

        var response = await client.GetAsync("/api/billing/payment-claims");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A held claim is listed with its invoice, client name, amount in whole units, currency and gateway.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_HeldClaim_IsReturnedWithItsDetails()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);
        var claimId = await SeedClaimAsync(clientId, invoiceId, held: true);

        using var body = await ReadAsync(admin, "/api/billing/payment-claims?status=Held&page=1&pageSize=100");

        var row = body.RootElement.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == claimId);
        row.GetProperty("invoiceId").GetInt32().Should().Be(invoiceId);
        row.GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
        row.GetProperty("amount").GetDecimal().Should().Be(12.50m);
        row.GetProperty("currency").GetString().Should().Be("USD");
        row.GetProperty("gatewayModuleId").GetString().Should().Be("claims-gateway");
        row.GetProperty("transactionId").GetString().Should().Be("tx-held-1");
    }

    /// <summary>The status filter leaves out claims in other statuses, and no filter lists them all.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_StatusFilter_LeavesOutOtherStatuses()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        // One blocking claim per invoice (the database refuses a second), so each claim gets its own invoice.
        var heldInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);
        var openInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);
        var heldId = await SeedClaimAsync(clientId, heldInvoiceId, held: true);
        var openId = await SeedClaimAsync(clientId, openInvoiceId, held: false);

        using var held = await ReadAsync(admin, "/api/billing/payment-claims?status=Held&page=1&pageSize=100");
        using var all = await ReadAsync(admin, "/api/billing/payment-claims?page=1&pageSize=100");

        var heldIds = held.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
        heldIds.Should().Contain(heldId).And.NotContain(openId);
        var allIds = all.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
        allIds.Should().Contain([heldId, openId]);
    }

    /// <summary>Pages are cut to the page size and report the total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_PageSize_CutsThePage()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        // One blocking claim per invoice (the database refuses a second), so each claim gets its own invoice.
        var firstInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);
        var secondInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);
        await SeedClaimAsync(clientId, firstInvoiceId, held: true);
        await SeedClaimAsync(clientId, secondInvoiceId, held: true);

        using var body = await ReadAsync(admin, "/api/billing/payment-claims?status=Held&page=1&pageSize=1");

        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        body.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>A status filter that names no claim status is refused instead of silently ignored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_UnknownStatus_Returns400()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/billing/payment-claims?status=NotAStatus");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
