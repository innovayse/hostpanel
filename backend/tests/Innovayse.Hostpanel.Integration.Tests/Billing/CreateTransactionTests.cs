using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/transactions</c> against Postgres: recording a manual transaction on a client's
/// ledger, and the round-trip through a subsequent read.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateTransactionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a transaction, then reading it back, round-trips its amounts.</summary>
    [Fact]
    public async Task CreateThenGetRoundTripsTheTransaction()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync("/api/transactions", new
        {
            clientId,
            date = DateTimeOffset.UtcNow,
            description = "Manual top-up",
            transactionId = $"txn-{Guid.NewGuid():N}",
            invoiceId = (int?)null,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 25.00m,
            amountOut = 0m,
            fees = 0m,
            addToCredit = false,
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var transactionId = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await admin.GetAsync($"/api/transactions/{transactionId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("id").GetInt32().Should().Be(transactionId);
        body.RootElement.GetProperty("amountIn").GetDecimal().Should().Be(25.00m);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == transactionId)).Source
            .Should().Be(TransactionSource.Manual);
    }

    /// <summary>Creating a transaction is not for anonymous callers.</summary>
    [Fact]
    public async Task CreateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/transactions", new
        {
            clientId = 1,
            date = DateTimeOffset.UtcNow,
            description = "Manual top-up",
            transactionId = "txn-x",
            invoiceId = (int?)null,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 25.00m,
            amountOut = 0m,
            fees = 0m,
            addToCredit = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A transaction for a client that does not exist answers 404 with <c>CLIENT_NOT_FOUND</c> and writes nothing.</summary>
    [Fact]
    public async Task CreateRefusesAnUnknownClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/transactions", new
        {
            clientId = BillingTestSeeds.NoSuchId,
            date = DateTimeOffset.UtcNow,
            description = "Manual top-up",
            transactionId = $"txn-{Guid.NewGuid():N}",
            invoiceId = (int?)null,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 25.00m,
            amountOut = 0m,
            fees = 0m,
            addToCredit = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>A transaction that settles an invoice that does not exist answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task CreateRefusesAnUnknownInvoice()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/transactions", new
        {
            clientId = clientId,
            date = DateTimeOffset.UtcNow,
            description = "Manual top-up",
            transactionId = $"txn-{Guid.NewGuid():N}",
            invoiceId = BillingTestSeeds.NoSuchId,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 25.00m,
            amountOut = 0m,
            fees = 0m,
            addToCredit = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }
}
