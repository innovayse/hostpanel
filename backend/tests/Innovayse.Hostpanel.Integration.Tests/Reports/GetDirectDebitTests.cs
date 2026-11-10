using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/direct-debit</c> against Postgres: every unpaid invoice on a direct-debit
/// payment method.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDirectDebitTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An unpaid invoice on a direct-debit payment method is listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ShowsAnUnpaidDirectDebitInvoice()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"Debit{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(5), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Renewal", 60m, 1);
            invoice.UpdateOptions(DateTimeOffset.UtcNow, invoice.DueDate, "Direct Debit", 0m);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/direct-debit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rows").EnumerateArray().Should().Contain(r => r.GetProperty("total").GetDecimal() == 60m);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/direct-debit");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "direct-debit");

        var response = await client.GetAsync("/api/reports/direct-debit");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
