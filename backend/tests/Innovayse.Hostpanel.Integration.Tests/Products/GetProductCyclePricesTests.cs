using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductCyclePrices;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <see cref="GetProductCyclePricesQuery"/> through the real <see cref="IMessageBus"/>: every answer is
/// the one <see cref="Product.PriceFor"/> gives, since other modules price products only through it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductCyclePricesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Each lookup answers exactly what the entity resolves, priced, unpriced and any case alike; an unknown product is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_AnswersEqualTheEntitysOwnResolutionAsync()
    {
        int productId;
        using (var seed = factory.Services.CreateScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var group = ProductGroup.Create($"cycle-prices-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);
            var product = Product.Create(group.Id, "Priced", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
            product.SetPrice("USD", BillingCycle.Monthly, 3.5m);
            product.SetPrice("USD", BillingCycle.Annual, 35m);
            context.Products.Add(product);
            await context.SaveChangesAsync(CancellationToken.None);
            productId = product.Id;
        }

        ProductCyclePriceLookup[] lookups =
        [
            new(productId, "USD", BillingCycle.Monthly),
            new(productId, "usd", BillingCycle.Annual),
            new(productId, "EUR", BillingCycle.Monthly),
            new(int.MaxValue, "USD", BillingCycle.Monthly),
        ];

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<ProductCyclePriceDto>>>(
            new GetProductCyclePricesQuery(lookups));

        result.IsSuccess.Should().BeTrue();
        var entity = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .Products.AsNoTracking().Include(p => p.Prices).SingleAsync(p => p.Id == productId);
        result.Value.Should().Equal(
            lookups.Take(3).Select(l => new ProductCyclePriceDto(
                l.ProductId, l.CurrencyCode, l.Cycle, entity.PriceFor(l.CurrencyCode, l.Cycle))));
        result.Value!.Select(p => p.Amount).Should().Equal(3.5m, 35m, null);
    }

    /// <summary>An empty lookup list answers an empty list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoLookups_ReturnsEmptyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<IReadOnlyList<ProductCyclePriceDto>>>(
            new GetProductCyclePricesQuery([]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
