using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProducts;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <see cref="GetOrderableProductsQuery"/> through the real <see cref="IMessageBus"/>: the product
/// facts the order flow prices its lines from, without reading the Products tables.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetOrderableProductsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A known product comes back with its status and its prices in the asked currency; an unknown id is left out.</summary>
    [Fact]
    public async Task HandleAsync_KnownAndUnknownIds_ReturnsTheKnownProductPricedInTheCurrencyAsync()
    {
        Product product;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var group = ProductGroup.Create($"orderable-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);

            product = Product.Create(group.Id, "Starter Hosting", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
            product.SetPrice("AMD", BillingCycle.Monthly, 4000m);
            context.Products.Add(product);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var result = await InvokeAsync(new GetOrderableProductsQuery([product.Id, int.MaxValue], "amd"));

        result.IsSuccess.Should().BeTrue();
        var orderable = result.Value!.Should().ContainSingle().Subject;
        orderable.Id.Should().Be(product.Id);
        orderable.Name.Should().Be("Starter Hosting");
        orderable.Status.Should().Be(product.Status);
        orderable.Price.CurrencyCode.Should().Be("AMD");
        orderable.Price.Monthly.Should().Be(4000m);
        orderable.Price.Annual.Should().BeNull("the product has no annual price in AMD");
        orderable.Price.Quarterly.Should().BeNull("a cycle that was never priced stays unsold");
        orderable.Price.SemiAnnual.Should().BeNull("a cycle that was never priced stays unsold");
        orderable.Price.Biennial.Should().BeNull("a cycle that was never priced stays unsold");
    }

    /// <summary>A product priced for the three newer cycles comes back with each amount under its own cycle.</summary>
    [Fact]
    public async Task HandleAsync_ProductPricedForTheNewCycles_ReturnsEachAmountUnderItsCycleAsync()
    {
        Product product;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var group = ProductGroup.Create($"orderable-cycles-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);

            product = Product.Create(group.Id, "Cycle Hosting", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
            product.SetPrice("USD", BillingCycle.Monthly, 3m);
            product.SetPrice("USD", BillingCycle.Quarterly, 8m);
            product.SetPrice("USD", BillingCycle.SemiAnnual, 15m);
            product.SetPrice("USD", BillingCycle.Biennial, 60m);
            context.Products.Add(product);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var result = await InvokeAsync(new GetOrderableProductsQuery([product.Id], "USD"));

        var price = result.Value!.Should().ContainSingle().Subject.Price;
        price.Monthly.Should().Be(3m);
        price.Annual.Should().BeNull();
        price.Quarterly.Should().Be(8m);
        price.SemiAnnual.Should().Be(15m);
        price.Biennial.Should().Be(60m);
        price.For(BillingCycle.Biennial).Should().Be(60m);
    }

    /// <summary>No ids answer an empty list.</summary>
    [Fact]
    public async Task HandleAsync_NoIds_ReturnsEmptyAsync()
    {
        var result = await InvokeAsync(new GetOrderableProductsQuery([], "USD"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<IReadOnlyList<OrderableProductDto>>> InvokeAsync(GetOrderableProductsQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<OrderableProductDto>>>(query);
    }
}
