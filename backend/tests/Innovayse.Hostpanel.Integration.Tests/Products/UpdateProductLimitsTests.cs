using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>PUT /api/products/{id}</c> lowering a product's cap on live services below the number already taken: the
/// existing services are left alone, and the product is sold out until places free.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateProductLimitsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A cap lowered below the live services is accepted, keeps every service, leaves the product off the public
    /// catalogue and refuses a new order as sold out.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateProduct_CapBelowTheLiveServices_KeepsThemAndSellsOutAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: 5, onePerClient: false);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "lowered-cap");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ClientServices.AddRange(
                ClientService.Create(clientId, productId, BillingCycle.Monthly, TestMoments.Seeded),
                ClientService.Create(clientId, productId, BillingCycle.Monthly, TestMoments.Seeded),
                ClientService.Create(clientId, productId, BillingCycle.Monthly, TestMoments.Seeded));
            await db.SaveChangesAsync();
        }

        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var updated = await admin.PutAsJsonAsync(
            $"/api/products/{productId}", new { name = "Lowered", monthlyPrice = 19.99m, annualPrice = 199.9m, maxActiveServices = 2 });

        updated.StatusCode.Should().Be(HttpStatusCode.NoContent, await updated.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.ClientServices.CountAsync(s => s.ProductId == productId)).Should().Be(3);
        }

        using var catalogue = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync($"/api/public/products?productId={productId}&currency=USD"));
        catalogue.RootElement.EnumerateArray().Should().BeEmpty();

        var refused = await client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
    }
}
