using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Pins the one thing about <see cref="ProductPrice"/> persistence that is not visible from the
/// domain: <see cref="ProductPrice.ProductId"/> is never set in memory (<see cref="Product.SetPrice"/>
/// leaves it 0), so the foreign key in <c>ProductConfiguration</c>, over the <c>_prices</c> backing
/// field, is what stamps it at <c>SaveChanges</c>. If that mapping were lost the insert would fail on
/// the FK, or worse, a price would silently belong to product 0.
/// </summary>
/// <remarks>
/// Reads directly against <see cref="AppDbContext"/>; every query here uses the
/// <c>.Include(p =&gt; p.Prices)</c> shape so the FK assertion still exercises a real reload.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProductPricePersistenceTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reloads a product with its prices, with a single query.</summary>
    /// <param name="db">The data context to read from.</param>
    /// <param name="productId">The product's primary key.</param>
    /// <returns>The product with its prices loaded, or <see langword="null"/> if not found.</returns>
    private static async Task<Product?> ReloadAsync(AppDbContext db, int productId) =>
        await db.Products.Include(p => p.Prices).FirstOrDefaultAsync(p => p.Id == productId);

    /// <summary>A price set on a new product is saved with that product's id and read back from the database.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetPriceThenSavePersistsTheProductId()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var group = ProductGroup.Create($"prices-{Guid.NewGuid():N}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(group.Id, "Priced", null, null, null, null, ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
            product.SetPrice("usd", BillingCycle.Monthly, 2.99m);
            product.SetPrice("AMD", BillingCycle.Annual, 12000m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        using var read = factory.Services.CreateScope();
        var readDb = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await ReloadAsync(readDb, productId);

        loaded.Should().NotBeNull();
        loaded!.Prices.Should().HaveCount(2);
        loaded.Prices.Should().OnlyContain(p => p.ProductId == productId);
        loaded.PriceFor("USD", BillingCycle.Monthly).Should().Be(2.99m);
        loaded.PriceFor("amd", BillingCycle.Annual).Should().Be(12000m);
        loaded.PriceFor("AMD", BillingCycle.Monthly).Should().BeNull();
    }

    /// <summary>Replacing a price updates the row rather than adding a second one for the same currency and cycle.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SetPriceTwiceUpdatesTheRow()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var group = ProductGroup.Create($"reprice-{Guid.NewGuid():N}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(group.Id, "Repriced", null, null, null, null, ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
            product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await ReloadAsync(db, productId);
            product!.SetPrice("USD", BillingCycle.Monthly, 3.49m);
            await db.SaveChangesAsync();
        }

        using var read = factory.Services.CreateScope();
        var readDb = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await ReloadAsync(readDb, productId);

        loaded!.Prices.Should().ContainSingle();
        loaded.PriceFor("USD", BillingCycle.Monthly).Should().Be(3.49m);
    }
}
