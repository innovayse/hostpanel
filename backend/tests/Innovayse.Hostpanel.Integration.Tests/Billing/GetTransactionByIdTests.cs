using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/transactions/{id}</c> against Postgres: reading a single transaction, and the
/// <c>TRANSACTION_NOT_FOUND</c> refusal for an id that names no transaction.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetTransactionByIdTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads back the transaction they just created.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetByIdReturnsTheCreatedTransaction()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 12.34m);

        var response = await admin.GetAsync($"/api/transactions/{transactionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("id").GetInt32().Should().Be(transactionId);
        body.RootElement.GetProperty("amountIn").GetDecimal().Should().Be(12.34m);
    }

    /// <summary>Reading an id that names no transaction answers 404 with <c>TRANSACTION_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetUnknownTransactionRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/transactions/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TRANSACTION_NOT_FOUND);
    }

    /// <summary>Reading a transaction is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/transactions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
