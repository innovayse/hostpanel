using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <see cref="GetProductsByIdsQuery"/> through the real <see cref="IMessageBus"/>: the query other
/// modules dispatch to order, price or provision a product without reading the Products tables.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductsByIdsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A known product comes back with its stored prices; an unknown id is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_KnownAndUnknownIds_ReturnsOnlyTheKnownProductAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(admin);

        var result = await InvokeAsync(new GetProductsByIdsQuery([productId, int.MaxValue]));

        result.IsSuccess.Should().BeTrue();
        var product = result.Value!.Should().ContainSingle().Subject;
        product.Id.Should().Be(productId);
        product.Name.Should().Be("Featured");
        var price = product.Prices.Should().ContainSingle().Subject;
        price.Monthly.Should().Be(1m);
        price.Annual.Should().Be(10m);
    }

    /// <summary>An empty id list answers an empty list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoIds_ReturnsEmptyAsync()
    {
        var result = await InvokeAsync(new GetProductsByIdsQuery([]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<IReadOnlyList<ProductDto>>> InvokeAsync(GetProductsByIdsQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(query);
    }
}
