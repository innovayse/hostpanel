using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>PUT /api/transactions/{id}</c> against Postgres: editing a transaction's amounts and
/// description, and the <c>TRANSACTION_NOT_FOUND</c> refusal for an id that names no transaction.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateTransactionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Editing a transaction's amount and description writes the new values.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateWritesTheNewAmountAndDescription()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 10m);

        var response = await admin.PutAsJsonAsync($"/api/transactions/{transactionId}", new
        {
            date = TestMoments.Seeded,
            description = "Edited",
            transactionId = "ext-ref",
            invoiceId = (int?)null,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 15m,
            amountOut = 0m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/transactions/{transactionId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amountIn").GetDecimal().Should().Be(15m);
        body.RootElement.GetProperty("description").GetString().Should().Be("Edited");
    }

    /// <summary>Updating an id that names no transaction answers 404 with <c>TRANSACTION_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownTransactionRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/transactions/{BillingTestSeeds.NoSuchId}", new
        {
            date = TestMoments.Seeded,
            description = "Edited",
            transactionId = "ext-ref",
            invoiceId = (int?)null,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 5m,
            amountOut = 0m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TRANSACTION_NOT_FOUND);
    }

    /// <summary>Updating a transaction is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/transactions/1", new
        {
            date = TestMoments.Seeded,
            description = "Edited",
            transactionId = "ext-ref",
            invoiceId = (int?)null,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 5m,
            amountOut = 0m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Linking a transaction to an invoice that does not exist answers 404 with <c>INVOICE_NOT_FOUND</c> and changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesAnUnknownInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 10m);

        var response = await admin.PutAsJsonAsync($"/api/transactions/{transactionId}", new
        {
            date = TestMoments.Seeded,
            description = "Edited",
            transactionId = "ext-ref",
            invoiceId = BillingTestSeeds.NoSuchId,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 15m,
            amountOut = 0m,
            fees = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        var fetched = await admin.GetAsync($"/api/transactions/{transactionId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amountIn").GetDecimal().Should().Be(10m);
    }
}
