using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>POST /api/admin/currencies/update-product-prices</c> against Postgres: recomputing product
/// prices in a target currency, and the refusals for the base currency and an unconfigured code.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateProductPricesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The seeded AMD rate: 1 AMD = 0.0025641 USD, so $2.99 is 1166 ֏ at zero decimals.</summary>
    private const decimal ExpectedAmdMonthly = 1166m;

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>"Update product prices" for AMD writes an AMD row beside a product's USD one and reports the count.</summary>
    [Fact]
    public async Task UpdateProductPricesWritesTheConvertedRow()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var group = ProductGroup.Create($"currencies-{Guid.NewGuid():N}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
            product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        var client = await AdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/admin/currencies/update-product-prices",
            new { currencies = new[] { "AMD" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<int>()).Should().BeGreaterThanOrEqualTo(1);

        using var read = factory.Services.CreateScope();
        var readDb = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await readDb.Products
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == productId);
        loaded!.PriceFor("AMD", BillingCycle.Monthly).Should().Be(ExpectedAmdMonthly);
        loaded.PriceFor("USD", BillingCycle.Monthly).Should().Be(2.99m);
    }

    /// <summary>
    /// A cycle the base does not price is dropped from the target too, since a figure kept there would
    /// keep it on sale in that currency alone.
    /// </summary>
    [Fact]
    public async Task UpdateProductPricesDropsACycleTheBaseDoesNotPrice()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var group = ProductGroup.Create($"currencies-{Guid.NewGuid():N}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(group.Id, "Monthly only", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
            product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
            product.SetPrice("AMD", BillingCycle.Annual, 99_999m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        var client = await AdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/admin/currencies/update-product-prices",
            new { currencies = new[] { "AMD" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var read = factory.Services.CreateScope();
        var readDb = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await readDb.Products
            .Include(p => p.Prices)
            .FirstAsync(p => p.Id == productId);
        loaded.PriceFor("AMD", BillingCycle.Monthly).Should().Be(ExpectedAmdMonthly);
        loaded.PriceFor("AMD", BillingCycle.Annual).Should().BeNull();
    }

    /// <summary>
    /// Recomputing converts every cycle the base prices, the newer ones included, and keeps each
    /// converted amount under its own cycle even when a cycle in between is not priced: the
    /// monthly and biennial amounts must not swap places or be left behind.
    /// </summary>
    [Fact]
    public async Task UpdateProductPricesConvertsEveryPricedCycleUnderItsOwnCycle()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var group = ProductGroup.Create($"currencies-{Guid.NewGuid():N}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(group.Id, "Five cycles", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
            product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
            product.SetPrice("USD", BillingCycle.Quarterly, 10m);
            product.SetPrice("USD", BillingCycle.Biennial, 20m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        var client = await AdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/admin/currencies/update-product-prices",
            new { currencies = new[] { "AMD" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var read = factory.Services.CreateScope();
        var readDb = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await readDb.Products
            .Include(p => p.Prices)
            .FirstAsync(p => p.Id == productId);
        loaded.PriceFor("AMD", BillingCycle.Monthly).Should().Be(ExpectedAmdMonthly);
        loaded.PriceFor("AMD", BillingCycle.Quarterly).Should().Be(3900m);
        loaded.PriceFor("AMD", BillingCycle.Biennial).Should().Be(7800m);
        loaded.PriceFor("AMD", BillingCycle.Annual).Should().BeNull();
        loaded.PriceFor("AMD", BillingCycle.SemiAnnual).Should().BeNull();
        loaded.PriceFor("USD", BillingCycle.Quarterly).Should().Be(10m);
    }

    /// <summary>Recomputing the base's own prices is refused.</summary>
    [Fact]
    public async Task UpdateProductPricesRefusesTheBase()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/currencies/update-product-prices",
            new { currencies = new[] { "USD" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertCodeAsync(response, HostpanelErrorCodes.CURRENCY_BASE_NOT_RECOMPUTED);
    }

    /// <summary>A code that names no configured currency is refused as missing, not as a bad field.</summary>
    [Fact]
    public async Task UpdateProductPricesRefusesAnUnconfiguredCurrency()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/currencies/update-product-prices",
            new { currencies = new[] { "ZZZ" } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertCodeAsync(response, HostpanelErrorCodes.CURRENCY_NOT_FOUND);
    }

    /// <summary>A currency listed twice, in any case, is refused by the validator before anything is written.</summary>
    [Fact]
    public async Task UpdateProductPricesRefusesADuplicateCurrency()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/currencies/update-product-prices",
            new { currencies = new[] { "AMD", "amd" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Recomputing prices is not for anonymous callers.</summary>
    [Fact]
    public async Task UpdateProductPricesRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/admin/currencies/update-product-prices", new { currencies = new[] { "AMD" } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Asserts the refusal carries the expected error code, which is what a client branches on.
    /// </summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    private static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }
}
