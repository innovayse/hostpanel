using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing</c> against Postgres: an admin raises an invoice for a client and the
/// created row round-trips its line item and totals.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating an invoice as the admin, then reading it back, round-trips the line item and totals.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGetRoundTripsTheInvoice()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync("/api/billing", new
        {
            clientId,
            dueDate = DateTimeOffset.UtcNow.AddDays(14),
            items = new[] { new { description = "Hosting", unitPrice = 9.99m, quantity = 1 } },
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoiceId = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("id").GetInt32().Should().Be(invoiceId);
        body.RootElement.GetProperty("amounts").GetProperty("subTotal").GetDecimal().Should().Be(9.99m);
    }

    /// <summary>
    /// Registering a client, then creating an invoice for them as the admin, answers 201 with the
    /// new invoice's id, which proves the round-trip through the client-creation Wolverine handler.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateInvoiceAsync_Returns201_AsAdminAsync()
    {
        var (_, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "create-invoice");

        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var dueDate = DateTimeOffset.UtcNow.AddDays(30);
        var createResponse = await admin.PostAsJsonAsync("/api/billing", new
        {
            clientId,
            dueDate,
            items = new[] { new { description = "Web Hosting", unitPrice = 9.99m, quantity = 1 } },
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoiceId = await createResponse.Content.ReadFromJsonAsync<int>();
        invoiceId.Should().BeGreaterThan(0);
    }

    /// <summary>Creating an invoice is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/billing", new
        {
            clientId = 1,
            dueDate = DateTimeOffset.UtcNow.AddDays(14),
            items = new[] { new { description = "Hosting", unitPrice = 9.99m, quantity = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An invoice for a client that does not exist answers 404 with <c>CLIENT_NOT_FOUND</c> and writes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateRefusesAnUnknownClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/billing", new
        {
            clientId = BillingTestSeeds.NoSuchId,
            dueDate = DateTimeOffset.UtcNow.AddDays(14),
            items = new[] { new { description = "Hosting", unitPrice = 9.99m, quantity = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }
}
