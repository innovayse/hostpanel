using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductSummaries;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <see cref="GetProductSummariesQuery"/> through the real <see cref="IMessageBus"/>: the query other
/// modules dispatch to show a product's name and prices without reading the Products tables.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductSummariesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A known product comes back with its name and both cycle prices; an unknown id is left out.</summary>
    [Fact]
    public async Task HandleAsync_KnownAndUnknownIds_ReturnsOnlyTheKnownProductAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(admin);

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<ProductSummaryDto>>>(
            new GetProductSummariesQuery([productId, int.MaxValue]));

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value!.Should().ContainSingle().Subject;
        summary.Id.Should().Be(productId);
        summary.Name.Should().Be("Featured");
        summary.MonthlyPrice.Should().Be(1m);
        summary.AnnualPrice.Should().Be(10m);
        summary.FromPrice.Should().Be(0.83m);
    }

    /// <summary>The from price is the lowest monthly equivalent across every priced cycle, quarterly, semi-annual and biennial included.</summary>
    [Fact]
    public async Task HandleAsync_ProductPricedOnlyOnLongCycles_FromPriceIsTheLowestMonthlyEquivalentAsync()
    {
        var productId = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(
            factory, (BillingCycle.Quarterly, 30m), (BillingCycle.SemiAnnual, 48m), (BillingCycle.Biennial, 120m));

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<ProductSummaryDto>>>(
            new GetProductSummariesQuery([productId]));

        var summary = result.Value!.Should().ContainSingle().Subject;
        summary.MonthlyPrice.Should().BeNull();
        summary.AnnualPrice.Should().BeNull();
        summary.FromPrice.Should().Be(5m);
    }

    /// <summary>An empty id list answers an empty list without touching the database.</summary>
    [Fact]
    public async Task HandleAsync_NoIds_ReturnsEmptyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<IReadOnlyList<ProductSummaryDto>>>(
            new GetProductSummariesQuery([]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>A product with no price in the caller's currency is still answered, by name, with no prices.</summary>
    [Fact]
    public async Task HandleAsync_ProductWithNoPriceInTheCurrency_IsStillNamedAsync()
    {
        int productId;
        using (var seed = factory.Services.CreateScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var group = ProductGroup.Create($"unpriced-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);
            var product = Product.Create(group.Id, "Unpriced", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
            context.Products.Add(product);
            await context.SaveChangesAsync(CancellationToken.None);
            productId = product.Id;
        }

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<ProductSummaryDto>>>(
            new GetProductSummariesQuery([productId]));

        var summary = result.Value!.Should().ContainSingle().Subject;
        summary.Name.Should().Be("Unpriced");
        summary.MonthlyPrice.Should().BeNull();
        summary.AnnualPrice.Should().BeNull();
        summary.FromPrice.Should().BeNull();
    }
}
