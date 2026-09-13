namespace Innovayse.Integration.Tests.Products;

using FluentAssertions;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Pins the one thing about <see cref="ProductPrice"/> persistence that is not visible from the
/// domain: <see cref="ProductPrice.ProductId"/> is never set in memory — <see cref="Product.SetPrice"/>
/// leaves it 0 — so the foreign key in <c>ProductConfiguration</c>, over the <c>_prices</c> backing
/// field, is what stamps it at <c>SaveChanges</c>. If that mapping were lost the insert would fail on
/// the FK, or worse, a price would silently belong to product 0.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProductPricePersistenceTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A price set on a new product is saved with that product's id and read back through the repository.</summary>
    [Fact]
    public async Task SetPriceThenSavePersistsTheProductId()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groups = scope.ServiceProvider.GetRequiredService<IProductGroupRepository>();
            var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            var group = ProductGroup.Create($"prices-{Guid.NewGuid():N}", null);
            groups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(group.Id, "Priced", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
            product.SetPrice("usd", BillingCycle.Monthly, 2.99m);
            product.SetPrice("AMD", BillingCycle.Annual, 12000m);
            products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        using var read = factory.Services.CreateScope();
        var repo = read.ServiceProvider.GetRequiredService<IProductRepository>();
        var loaded = await repo.FindByIdAsync(productId, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.Prices.Should().HaveCount(2);
        loaded.Prices.Should().OnlyContain(p => p.ProductId == productId);
        loaded.PriceFor("USD", BillingCycle.Monthly).Should().Be(2.99m);
        loaded.PriceFor("amd", BillingCycle.Annual).Should().Be(12000m);
        loaded.PriceFor("AMD", BillingCycle.Monthly).Should().BeNull();
    }

    /// <summary>Replacing a price updates the row rather than adding a second one for the same currency and cycle.</summary>
    [Fact]
    public async Task SetPriceTwiceUpdatesTheRow()
    {
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groups = scope.ServiceProvider.GetRequiredService<IProductGroupRepository>();
            var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            var group = ProductGroup.Create($"reprice-{Guid.NewGuid():N}", null);
            groups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(group.Id, "Repriced", null, null, null, null, ProductType.SharedHosting, 0m, 0m);
            product.SetPrice("USD", BillingCycle.Monthly, 2.99m);
            products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var product = await products.FindByIdAsync(productId, CancellationToken.None);
            product!.SetPrice("USD", BillingCycle.Monthly, 3.49m);
            await db.SaveChangesAsync();
        }

        using var read = factory.Services.CreateScope();
        var repo = read.ServiceProvider.GetRequiredService<IProductRepository>();
        var loaded = await repo.FindByIdAsync(productId, CancellationToken.None);

        loaded!.Prices.Should().ContainSingle();
        loaded.PriceFor("USD", BillingCycle.Monthly).Should().Be(3.49m);
    }
}
