namespace Innovayse.Integration.Tests.Billing;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// The currency administration endpoints against the migrated schema: the seeded USD/AMD pair,
/// creating a third currency, the bulk price recompute, and the anonymous storefront list.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CurrenciesEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The seeded AMD rate: 1 AMD = 0.0025641 USD, so $2.99 is 1166 ֏ at zero decimals.</summary>
    private const decimal ExpectedAmdMonthly = 1166m;

    /// <summary>USD's rate once AMD is the base: 1 / 0.0025641, which is 390 to within a hundredth.</summary>
    private const decimal ExpectedUsdRateInAmd = 390m;

    /// <summary>How far a re-expressed rate may sit from its round figure at numeric(18,8).</summary>
    private const decimal RateTolerance = 0.01m;

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>The migration seeded USD as the base and AMD beside it; the admin list shows both, with rates.</summary>
    [Fact]
    public async Task AdminListShowsTheSeededPairWithRates()
    {
        var client = await AdminClientAsync();

        var response = await client.GetAsync("/api/admin/currencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("code").GetString()).Should().Contain(["USD", "AMD"]);
        rows.Single(r => r.GetProperty("code").GetString() == "USD").GetProperty("isBase").GetBoolean().Should().BeTrue();
        rows.Single(r => r.GetProperty("code").GetString() == "AMD").GetProperty("rateToBase").GetDecimal().Should().Be(0.00256410m);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task AdminListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/currencies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>PUT on a new code creates the row, and PUT again rewrites it in place.</summary>
    [Fact]
    public async Task PutCreatesThenRewritesACurrency()
    {
        var client = await AdminClientAsync();

        var created = await client.PutAsJsonAsync("/api/admin/currencies/EUR",
            new { numeric = "978", prefix = "€", suffix = "", decimals = 2, rateToBase = 1.1m, isEnabled = true });
        created.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rewritten = await client.PutAsJsonAsync("/api/admin/currencies/eur",
            new { numeric = "978", prefix = "€", suffix = "", decimals = 2, rateToBase = 1.2m, isEnabled = false });
        rewritten.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/currencies");
        var eur = list!.Single(r => r.GetProperty("code").GetString() == "EUR");
        eur.GetProperty("rateToBase").GetDecimal().Should().Be(1.2m);
        eur.GetProperty("isEnabled").GetBoolean().Should().BeFalse();
        eur.GetProperty("isBase").GetBoolean().Should().BeFalse();
    }

    /// <summary>"Update product prices" for AMD writes an AMD row beside a product's USD one and reports the count.</summary>
    [Fact]
    public async Task UpdateProductPricesWritesTheConvertedRow()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groups = scope.ServiceProvider.GetRequiredService<IProductGroupRepository>();
            var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            var group = ProductGroup.Create($"currencies-{Guid.NewGuid():N}", null);
            groups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
            product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
            products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        var client = await AdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/admin/currencies/update-product-prices",
            new { currencies = new[] { "AMD" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<int>()).Should().BeGreaterThanOrEqualTo(1);

        using var read = factory.Services.CreateScope();
        var repo = read.ServiceProvider.GetRequiredService<IProductRepository>();
        var loaded = await repo.FindByIdAsync(productId, CancellationToken.None);
        loaded!.PriceFor("AMD", BillingCycle.Monthly).Should().Be(ExpectedAmdMonthly);
        loaded.PriceFor("USD", BillingCycle.Monthly).Should().Be(2.99m);
    }

    /// <summary>Recomputing the base's own prices is refused.</summary>
    [Fact]
    public async Task UpdateProductPricesRefusesTheBase()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/currencies/update-product-prices",
            new { currencies = new[] { "USD" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Making AMD the base against Postgres: exactly one row is the base afterwards, it is AMD,
    /// and USD's rate is re-expressed as 1 / 0.0025641 ≈ 390. The base is put back to USD at the
    /// end because the fixture is shared and the other cases read the seeded pair.
    /// </summary>
    [Fact]
    public async Task MakeBaseSwitchesTheBaseAndReexpressesTheOtherRate()
    {
        var client = await AdminClientAsync();
        try
        {
            var response = await client.PostAsync("/api/admin/currencies/AMD/make-base", content: null);
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/currencies");
            var bases = list!.Where(r => r.GetProperty("isBase").GetBoolean()).ToList();
            bases.Should().ContainSingle();
            bases.Single().GetProperty("code").GetString().Should().Be("AMD");
            list.Single(r => r.GetProperty("code").GetString() == "USD")
                .GetProperty("rateToBase").GetDecimal().Should().BeApproximately(ExpectedUsdRateInAmd, RateTolerance);
        }
        finally
        {
            var restored = await client.PostAsync("/api/admin/currencies/USD/make-base", content: null);
            restored.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    /// <summary>The storefront list is anonymous, lists only enabled rows, and carries no rate.</summary>
    [Fact]
    public async Task PublicListIsAnonymousEnabledOnlyAndRateless()
    {
        var admin = await AdminClientAsync();
        var disabled = await admin.PutAsJsonAsync("/api/admin/currencies/GBP",
            new { numeric = "826", prefix = "£", suffix = "", decimals = 2, rateToBase = 1.27m, isEnabled = false });
        disabled.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await factory.CreateClient().GetAsync("/api/currencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.EnumerateArray().ToList();
        var codes = rows.Select(r => r.GetProperty("code").GetString()).ToList();
        codes.Should().Contain(["USD", "AMD"]).And.NotContain("GBP");
        codes.First().Should().Be("USD");
        foreach (var row in rows)
        {
            row.EnumerateObject().Select(p => p.Name).Should()
                .NotContain("rateToBase")
                .And.Contain(["prefix", "suffix", "decimals", "isBase"]);
        }
    }
}
