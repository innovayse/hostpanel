using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/promotions</c> against Postgres: how often each promotion code is used.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetPromotionsUsageTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The ISO 4217 code of the non-base currency one seeded client is billed in.</summary>
    private const string ForeignCurrency = "XTS";

    /// <summary>The ISO 4217 numeric code of <see cref="ForeignCurrency"/>.</summary>
    private const string ForeignNumeric = "963";

    /// <summary>What one unit of <see cref="ForeignCurrency"/> is worth in the base currency.</summary>
    private const decimal ForeignRate = 0.5m;

    /// <summary>
    /// A code counts every service that carries it but earns only from the active ones, an annual service spread over twelve
    /// months; a service without a code is not a row.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsServicesAndSpreadsTheActiveOnesOverTheirCycle()
    {
        var marker = $"Promo{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        await BillingTestSeeds.EnsureCurrencyAsync(factory, ForeignCurrency, ForeignNumeric, ForeignRate);
        var foreignClientId = await BillingTestSeeds.SeedClientAsync(factory, ForeignCurrency);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = ProductGroup.Create($"PromoGroup-{marker}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();
            var product = Product.Create(
                group.Id, $"PromoProduct-{marker}", null, null, null, null, ProductType.SharedHosting, 10m, 100m, TestMoments.Seeded);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            await AddServiceAsync(db, clientId, product.Id, BillingCycle.Monthly, 10m, marker, suspend: false);
            await AddServiceAsync(db, clientId, product.Id, BillingCycle.Annual, 120m, marker, suspend: false);
            await AddServiceAsync(db, clientId, product.Id, BillingCycle.Monthly, 500m, marker, suspend: true);
            await AddServiceAsync(db, clientId, product.Id, BillingCycle.Monthly, 7m, null, suspend: false);
            await AddServiceAsync(db, foreignClientId, product.Id, BillingCycle.Monthly, 40m, marker, suspend: false);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/promotions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rows = body.GetProperty("rows").EnumerateArray().ToList();
        body.GetProperty("currency").GetString().Should().HaveLength(3);
        var row = rows.Single(r => r.GetProperty("code").GetString() == marker);
        row.GetProperty("services").GetInt32().Should().Be(4);
        row.GetProperty("activeServices").GetInt32().Should().Be(3);
        // 10 + 120 / 12 in the base currency, plus 40 of the foreign currency converted at its rate.
        row.GetProperty("monthlyRevenue").GetDecimal().Should().Be(20m + (40m * ForeignRate));
        rows.Should().OnlyContain(r => !string.IsNullOrEmpty(r.GetProperty("code").GetString()));
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/promotions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "promotions");

        var response = await client.GetAsync("/api/reports/promotions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Adds an active service with the given cycle, recurring amount and promotion code, optionally suspended.</summary>
    /// <param name="db">The data context to seed through.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="productId">The ordered product.</param>
    /// <param name="cycle">The billing cycle.</param>
    /// <param name="recurring">The recurring amount.</param>
    /// <param name="code">The promotion code, or <see langword="null"/> for none.</param>
    /// <param name="suspend">Whether to suspend the service after activating it.</param>
    /// <returns>A task that completes once the service is saved.</returns>
    private static async Task AddServiceAsync(
        AppDbContext db, int clientId, int productId, BillingCycle cycle, decimal recurring, string? code, bool suspend)
    {
        var service = ClientService.Create(clientId, productId, cycle, TestMoments.Seeded);
        await ServicesTestSeeds.AddActivatedAsync(db, service, "provisioning-ref", TestMoments.Seeded);
        service.ChangeBillingTerms(new ServiceBillingTerms(cycle, 1, 0m, recurring, null, null, code, null));
        if (suspend)
        {
            service.Suspend(TestMoments.Seeded, "non-payment", hostingAlreadyUpdated: true);
        }
    }
}
